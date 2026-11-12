using Muhanok.Application;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class RunRulesTests
    {
        private static RunSession Running()
        {
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.IntroTimeline);
            run.Flow.Transition(GameFlowState.FirstPersonTakeover); run.Flow.Transition(GameFlowState.Running); return run;
        }
        [Test] public void FlowRejectsSkippedStates()
        {
            var flow = new GameFlowController(); Assert.IsFalse(flow.Transition(GameFlowState.Running));
            Assert.IsTrue(flow.Transition(GameFlowState.TitleIdle)); Assert.IsFalse(flow.Transition(GameFlowState.Result));
        }
        [Test] public void LaneIsBoundedAndArrivesAtAnchor()
        {
            var runner = new RunnerController(new RunConfig());
            for (int i = 0; i < 5; i++) runner.Command(MotionCommand.Left);
            runner.Tick(.1f, 0); Assert.That(runner.X, Is.GreaterThan(-2.2f).And.LessThan(0));
            runner.Tick(.1f, 0); Assert.AreEqual(-2.2f, runner.X, .0001f);
            for (int i = 0; i < 5; i++) runner.Command(MotionCommand.Right);
            runner.Tick(.2f, 0); Assert.AreEqual(2.2f, runner.X, .0001f);
        }
        [Test] public void MidLaneRetargetIsContinuous()
        {
            var runner = new RunnerController(new RunConfig()); runner.Command(MotionCommand.Left); runner.Tick(.08f, 0);
            float x = runner.X; runner.Command(MotionCommand.Right); Assert.AreEqual(x, runner.X);
            runner.Tick(.2f, 0); Assert.AreEqual(0, runner.X);
        }
        [Test] public void JumpAndActionsExpire()
        {
            var runner = new RunnerController(new RunConfig()); runner.Command(MotionCommand.Jump); runner.Tick(.1f, 0); Assert.Greater(runner.Y, .5f);
            for (int i = 0; i < 40; i++) runner.Tick(.05f, 0); Assert.AreEqual(0, runner.Y);
            runner.Command(MotionCommand.Crouch); runner.Command(MotionCommand.HighKnee); Assert.IsTrue(runner.Crouching && runner.HighKnee);
            runner.Tick(1.1f, 0); Assert.IsFalse(runner.Crouching || runner.HighKnee);
        }
        [TestCase(0f,9f)] [TestCase(120f,10f)] [TestCase(480f,13f)] [TestCase(960f,17f)] [TestCase(5000f,17f)]
        public void SpeedRampsWithDistanceAndStaysCapped(float distance,float expected)
        {
            var config=new RunConfig();var director=new DifficultyDirector();
            Assert.AreEqual(expected,director.Speed(distance,config),.001f);
            Assert.AreEqual(expected,director.Speed(distance,config,true),.001f);
        }
        [TestCase(-50f,0f)] [TestCase(0f,0f)] [TestCase(150f,0f)] [TestCase(1075f,.5f)] [TestCase(2000f,1f)] [TestCase(100000f,1f)]
        public void DifficultyProgressWaitsThenEasesToFull(float distance,float expected)
        {
            Assert.AreEqual(expected,new DifficultyDirector().Progress(distance,new RunConfig()),.001f);
        }
        [Test] public void DifficultyProgressRisesMonotonically()
        {
            var director=new DifficultyDirector();var config=new RunConfig();float last=0;
            for(float m=0;m<=2500;m+=25){float p=director.Progress(m,config);Assert.GreaterOrEqual(p,last);last=p;}
        }
        [Test] public void DifficultyProgressHandlesAFullDistanceAtOrBeforeTheGrace()
        {
            var director=new DifficultyDirector();var config=new RunConfig{DifficultyGraceDistance=300,DifficultyFullDistance=300};
            Assert.AreEqual(0,director.Progress(299,config));Assert.AreEqual(1,director.Progress(300,config));
        }
        [Test] public void HazardOddsGoFromStartToMax()
        {
            var director=new DifficultyDirector();var config=new RunConfig();
            var start=director.Odds(0,config);
            Assert.AreEqual(.25f,start.Row,.001f);Assert.AreEqual(.72f,start.Blocker,.001f);Assert.AreEqual(.3f,start.DoubleBlocker,.001f);Assert.AreEqual(.3f,start.Banana,.001f);
            var max=director.Odds(5000,config);
            Assert.AreEqual(.9f,max.Row,.001f);Assert.AreEqual(.72f,max.Blocker,.001f);Assert.AreEqual(.85f,max.DoubleBlocker,.001f);Assert.AreEqual(.82f,max.Banana,.001f);
        }
        [Test] public void PauseSlipAndRetryPreserveOrResetSpeedCorrectly()
        {
            var run=Running();run.Score.Advance(480f);Assert.AreEqual(13f,run.CurrentSpeed,.001f);
            run.Slip();Assert.AreEqual(6.5f,run.CurrentSpeed,.001f);
            run.Paused=true;float distance=run.Score.Distance;run.Tick(10);
            Assert.AreEqual(0f,run.CurrentSpeed);Assert.AreEqual(distance,run.Score.Distance);
            run.Paused=false;run.Reset();Assert.AreEqual(9f,run.CurrentSpeed,.001f);
        }
        [Test] public void GroundedJumpCancelsSlideButDoesNotAllowDoubleJump()
        {
            var runner=new RunnerController(new RunConfig());runner.Command(MotionCommand.Crouch);
            runner.Command(MotionCommand.Jump);runner.Command(MotionCommand.Crouch);
            Assert.IsFalse(runner.Crouching,"A held slide key must not override the jump before the movement tick");runner.Tick(.2f,0);
            runner.Command(MotionCommand.Jump);runner.Tick(.2f,0);
            Assert.That(runner.Y,Is.InRange(1.79f,1.8f));
            runner.Command(MotionCommand.Crouch);Assert.IsFalse(runner.Crouching);
        }
        [Test] public void ChaseIsClampedAndCaughtIsSticky()
        {
            var chase = new ChaseSystem(); chase.Hit(-10); Assert.AreEqual(0, chase.Pressure);
            chase.Hit(2); Assert.AreEqual(1, chase.Pressure); chase.Recover(1); Assert.IsTrue(chase.IsCaught);
            chase.Reset(); Assert.IsFalse(chase.IsCaught);
        }
        [TestCase(false)] [TestCase(true)] public void OneSolidCollisionEndsTheRunAndFreezesDistance(bool heavy)
        {
            var run=Running();run.Tick(.2f);float z=run.Runner.Z;
            Assert.IsTrue(run.Hit(heavy));Assert.AreEqual(GameFlowState.Caught,run.Flow.State);
            run.Tick(3);Assert.AreEqual(z,run.Runner.Z);Assert.IsFalse(run.Hit(heavy));
        }
        [Test] public void FloorSlipSlowsWithoutKillingAndRecoversWhilePauseFreezesTimer()
        {
            var run=Running();Assert.IsTrue(run.Slip());float before=run.Runner.Z;
            run.Tick(.5f);Assert.AreEqual(GameFlowState.Running,run.Flow.State);
            Assert.That(run.Runner.Z-before,Is.InRange(2.2f,2.3f));Assert.AreEqual(0,run.Chase.Pressure);
            float remaining=run.Runner.SlowRemaining;run.Paused=true;run.Tick(10);Assert.AreEqual(remaining,run.Runner.SlowRemaining);
            run.Paused=false;run.Tick(2);Assert.IsFalse(run.Runner.Slowing);Assert.AreEqual(1,run.Runner.SpeedMultiplier);
        }
        [Test] public void CartAllowsSteeringAndDuckButBlocksJumpAndReturnsToRunning()
        {
            var run=Running();run.Runner.SetStartZ(25);Assert.IsTrue(run.Runner.Riding);
            run.Runner.Command(MotionCommand.Left);run.Runner.Command(MotionCommand.Crouch);run.Runner.Command(MotionCommand.Jump);
            run.Tick(.2f);Assert.AreEqual(-2.2f,run.Runner.X,.0001f);Assert.AreEqual(0,run.Runner.Y);Assert.IsTrue(run.Runner.Crouching);
            Assert.IsFalse(run.Slip());run.Runner.SetStartZ(73);Assert.IsFalse(run.Runner.Riding);
            run.Tick(1);run.Runner.Command(MotionCommand.Jump);run.Tick(.1f);Assert.Greater(run.Runner.Y,0);
        }
        [Test] public void LoadingIntroTransitionsStraightIntoGameplay()
        {
            var flow=new GameFlowController();flow.Transition(GameFlowState.TitleIdle);
            Assert.IsTrue(flow.Transition(GameFlowState.LoadingIntro));Assert.IsFalse(flow.Transition(GameFlowState.IntroTimeline));
            Assert.IsTrue(flow.Transition(GameFlowState.Running));
        }
        [Test] public void SecondBananaStartsDelayedPursuitAndPauseAndRetryResetIt()
        {
            var run=Running();Assert.IsTrue(run.Slip(SlipSource.Banana));run.Tick(3);
            Assert.AreEqual(1,run.SlipStrikes);Assert.IsFalse(run.Runner.Slowing);Assert.Greater(run.Chase.Pressure,.4f);
            run.Runner.SetStartZ(80);Assert.IsTrue(run.Slip(SlipSource.Banana));run.Tick(.6f);
            Assert.AreEqual(GameFlowState.Running,run.Flow.State);Assert.IsTrue(run.SlipPursuit);Assert.IsTrue(run.Runner.Slowing);
            run.Paused=true;run.Tick(10);Assert.AreEqual(GameFlowState.Running,run.Flow.State);
            run.Paused=false;run.Tick(.61f);Assert.AreEqual(GameFlowState.Caught,run.Flow.State);Assert.AreEqual(CaptureReason.SlipPursuit,run.Reason);
            run.Reset();Assert.AreEqual(0,run.SlipStrikes);Assert.IsFalse(run.SlipPursuit);Assert.AreEqual(0,run.Chase.Pressure);
        }
        [Test] public void SingleStrikeClearsAfterEightSecondsAndRemovesItsPressure()
        {
            var run=Running();Assert.IsTrue(run.Slip(SlipSource.Banana));
            Assert.AreEqual(.45f,run.Chase.Pressure,.001f);Assert.AreEqual(run.Config.StrikeClearSeconds,run.StrikeClearRemaining,.01f);
            run.Tick(7.5f);Assert.AreEqual(1,run.SlipStrikes);Assert.AreEqual(.45f,run.Chase.Pressure,.001f);Assert.AreEqual(.5f,run.StrikeClearRemaining,.05f);
            run.Tick(.6f);Assert.AreEqual(0,run.SlipStrikes);Assert.AreEqual(0,run.Chase.Pressure,.001f);Assert.AreEqual(0,run.StrikeClearRemaining);
        }
        [Test] public void SecondStrikeWithinTheWindowStartsPursuitAndCatches()
        {
            var run=Running();run.Slip(SlipSource.Banana);run.Tick(7);run.Runner.SetStartZ(80);
            run.Slip(SlipSource.Banana);Assert.IsTrue(run.SlipPursuit);run.Tick(2);
            Assert.AreEqual(GameFlowState.Caught,run.Flow.State);Assert.AreEqual(CaptureReason.SlipPursuit,run.Reason);
        }
        [Test] public void StrikeAfterClearingCountsAsAFirstStrikeAgain()
        {
            var run=Running();run.Slip(SlipSource.Banana);run.Tick(8.5f);Assert.AreEqual(0,run.SlipStrikes);
            run.Tick(3);run.Runner.SetStartZ(80);run.Slip(SlipSource.Banana);
            Assert.AreEqual(1,run.SlipStrikes);Assert.IsFalse(run.SlipPursuit);Assert.AreEqual(GameFlowState.Running,run.Flow.State);
            run.Tick(.1f);Assert.AreEqual(GameFlowState.Running,run.Flow.State);
        }
        [Test] public void KneeMissCountsLikeABananaAndFloorSlipsDoNot()
        {
            var run=Running();run.Slip(SlipSource.Floor);Assert.AreEqual(0,run.SlipStrikes);
            Assert.IsTrue(run.Slip(SlipSource.KneeMiss));Assert.AreEqual(1,run.SlipStrikes);Assert.AreEqual(.45f,run.Chase.Pressure,.001f);
            run.Slip(SlipSource.Banana);Assert.AreEqual(2,run.SlipStrikes);Assert.IsTrue(run.SlipPursuit);
            run.Reset();run.Slip(SlipSource.Banana);run.Slip(SlipSource.KneeMiss);Assert.IsTrue(run.SlipPursuit);
        }
        [Test] public void ShieldAbsorbsAKneeMissWithoutAStrike()
        {
            var run=Running();run.Items.Activate(ItemKind.Shield);
            Assert.IsFalse(run.Slip(SlipSource.KneeMiss));Assert.AreEqual(0,run.SlipStrikes);Assert.AreEqual(1,run.ShieldSaves);
        }
        [Test] public void RepeatedPuddlesDoNotCountAsBananas()
        {
            var run=Running();for(int i=0;i<3;i++){run.Slip();run.Tick(.1f);}
            Assert.AreEqual(0,run.SlipStrikes);Assert.IsFalse(run.SlipPursuit);Assert.AreEqual(GameFlowState.Running,run.Flow.State);
        }
        [Test] public void CartLeanSwitchesAcrossCenterWithoutLeavingSingleTrack()
        {
            var run=Running();run.Runner.SetStartZ(35);run.Runner.Command(MotionCommand.Left);run.Tick(.2f);
            Assert.AreEqual(-1,run.Runner.CartLean,.001f);Assert.AreEqual(-.16f,run.Runner.WorldLaneOffset,.001f);
            run.Runner.Command(MotionCommand.Right);run.Tick(.2f);Assert.AreEqual(1,run.Runner.CartLean,.001f);
            run.Runner.Command(MotionCommand.Center);run.Tick(.2f);Assert.AreEqual(0,run.Runner.CartLean,.001f);
        }
        [Test] public void FullExerciseRowsRequireDistinctActions()
        {
            Assert.IsFalse(SafePatternValidator.Avoided(ObstacleKind.Stair,1.8f,false,false));
            Assert.IsFalse(SafePatternValidator.Avoided(ObstacleKind.Pipe,1.8f,false,true));
            foreach(var kind in new[]{ObstacleKind.Crate,ObstacleKind.Pipe,ObstacleKind.Stair})Assert.IsTrue(SafePatternValidator.IsActionRowSafe(7,kind));
            Assert.IsFalse(SafePatternValidator.IsActionRowSafe(7,ObstacleKind.Barrier));
        }
        [Test] public void ScoreWithoutExerciseIsTheRoundedDistance()
        {
            var score = new ScoreSystem(); score.Advance(10.5f); score.Advance(-4); score.CleanPass();
            Assert.AreEqual(11, score.Score, "Clean passes are recorded but do not score"); Assert.AreEqual(1, score.CleanPasses);
            score.Reset(); Assert.AreEqual(0, score.Score); Assert.AreEqual(0, score.CleanPasses);
        }
        [Test] public void ElapsedCountsPlayTimeButNotPauses()
        {
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            run.Tick(1.5f); Assert.AreEqual(1.5f, run.Elapsed, .0001f);
            run.Paused = true; run.Tick(10f); Assert.AreEqual(1.5f, run.Elapsed, .0001f);
            run.Paused = false; run.Tick(.5f); Assert.AreEqual(2f, run.Elapsed, .0001f);
            run.Reset(); Assert.AreEqual(0, run.Elapsed);
        }
        [TestCase(7, false)] [TestCase(3, true)] [TestCase(0, true)] [TestCase(5, true)]
        public void PatternLeavesAnEmptyLane(int mask, bool expected) { Assert.AreEqual(expected, SafePatternValidator.IsSafe(mask)); }
        [Test] public void CollectedCoinsCountAndResetIndependentlyOfDistance()
        {
            var score=new ScoreSystem(); score.Advance(12); score.CollectCoin(); score.CollectCoin();
            Assert.AreEqual(2,score.Coins); Assert.AreEqual(12,score.Score,"Coins do not score"); Assert.AreEqual(12,score.Distance);
            score.Reset(); Assert.AreEqual(0,score.Coins); Assert.AreEqual(0,score.Score);
        }
        [Test] public void EachActionMatchesItsHazard()
        {
            Assert.IsTrue(SafePatternValidator.Avoided(ObstacleKind.Crate, 1.1f, false, false));
            Assert.IsTrue(SafePatternValidator.Avoided(ObstacleKind.Pipe, 0, true, false));
            Assert.IsTrue(SafePatternValidator.Avoided(ObstacleKind.Stair, 0, false, true));
            Assert.IsFalse(SafePatternValidator.Avoided(ObstacleKind.Laser, 2, true, true));
        }
        [Test] public void RouterClearsTitleCommands()
        {
            var run = new RunSession(new RunConfig()); var keys = new PoseGameInput(); var pose = new PoseGameInput();
            keys.Buffer.Add(MotionCommand.Left); pose.Buffer.Add(MotionCommand.Jump); new InputRouter(run, keys, pose).Tick();
            Assert.IsFalse(keys.TryRead(out _)); Assert.IsFalse(pose.TryRead(out _)); Assert.AreEqual(0, run.Runner.Lane);
        }
        [Test] public void RetryClearsAllRunRules()
        {
            var run = Running(); run.Runner.Command(MotionCommand.Left); run.Tick(.8f); run.Hit(true); run.Reset();
            Assert.AreEqual(0, run.Score.Score); Assert.AreEqual(0, run.Chase.Pressure); Assert.AreEqual(0, run.Runner.Lane); Assert.AreEqual(2, run.Runner.Z);
        }
    }
}
