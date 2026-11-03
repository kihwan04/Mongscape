using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class TutorialScriptTests
    {
        [Test]
        public void StepsFollowThePlayStyle()
        {
            CollectionAssert.AreEqual(new[] { TutorialStep.Calibrate, TutorialStep.Lane, TutorialStep.Jump, TutorialStep.Crouch, TutorialStep.HighKnee, TutorialStep.Rail, TutorialStep.Fork, TutorialStep.Done },
                new TutorialScript(PlayStyle.FullBody).Steps);
            CollectionAssert.AreEqual(new TutorialScript(PlayStyle.FullBody).Steps, new TutorialScript(PlayStyle.UpperBody).Steps);
            CollectionAssert.AreEqual(new[] { TutorialStep.Lane, TutorialStep.Jump, TutorialStep.Crouch, TutorialStep.HighKnee, TutorialStep.Rail, TutorialStep.Fork, TutorialStep.Done },
                new TutorialScript(PlayStyle.Keyboard).Steps, "Keyboard play needs no calibration");
            CollectionAssert.DoesNotContain(new TutorialScript(PlayStyle.Keyboard, false).Steps, TutorialStep.Fork, "A route without forks has no lean lesson");
            CollectionAssert.Contains(new TutorialScript(PlayStyle.Keyboard, false).Steps, TutorialStep.Rail, "Every lap layout rides the cart, so the broken rail stays");
        }

        [Test]
        public void EachStepAwaitsItsCommandAndPassCondition()
        {
            Assert.AreEqual(TutorialPass.Calibrated, TutorialScript.PassOf(TutorialStep.Calibrate)); Assert.IsNull(TutorialScript.AwaitedOf(TutorialStep.Calibrate));
            Assert.AreEqual(MotionCommand.Jump, TutorialScript.AwaitedOf(TutorialStep.Jump)); Assert.AreEqual(TutorialPass.ObstaclePassed, TutorialScript.PassOf(TutorialStep.Jump));
            Assert.AreEqual(MotionCommand.Crouch, TutorialScript.AwaitedOf(TutorialStep.Crouch));
            Assert.AreEqual(MotionCommand.HighKnee, TutorialScript.AwaitedOf(TutorialStep.HighKnee));
            Assert.AreEqual(TutorialPass.ForkChosen, TutorialScript.PassOf(TutorialStep.Fork));
            Assert.IsTrue(TutorialScript.Accepts(TutorialStep.Lane, MotionCommand.Right)); Assert.IsTrue(TutorialScript.Accepts(TutorialStep.Fork, MotionCommand.Left));
            Assert.AreEqual(TutorialPass.ObstaclePassed, TutorialScript.PassOf(TutorialStep.Rail)); Assert.AreEqual(TutorialCourse.RailLean, TutorialScript.AwaitedOf(TutorialStep.Rail));
            Assert.IsTrue(TutorialScript.Accepts(TutorialStep.Rail, TutorialCourse.RailLean));
            Assert.IsFalse(TutorialScript.Accepts(TutorialStep.Rail, TutorialCourse.RailLean == MotionCommand.Right ? MotionCommand.Left : MotionCommand.Right), "Leaning toward the gap is not the lesson");
            Assert.IsFalse(TutorialScript.Accepts(TutorialStep.Jump, MotionCommand.Crouch)); Assert.IsFalse(TutorialScript.Accepts(TutorialStep.Done, MotionCommand.Jump));
            foreach (var step in new TutorialScript(PlayStyle.UpperBody).Steps) Assert.IsNotEmpty(TutorialScript.Instruction(step, PlayStyle.UpperBody));
        }

        [Test]
        public void StepsAdvanceInOrderUntilDone()
        {
            var script = new TutorialScript(PlayStyle.Keyboard);
            Assert.AreEqual(TutorialStep.Lane, script.Current);
            Assert.IsTrue(script.Pass()); Assert.AreEqual(TutorialStep.Jump, script.Current);
            while (!script.Finished) script.Pass();
            Assert.AreEqual(TutorialStep.Done, script.Current); Assert.IsFalse(script.Pass(), "Done is final");
        }

        [Test]
        public void FailureRetriesTheSameStepAndCountsRetries()
        {
            var script = new TutorialScript(PlayStyle.FullBody); script.Pass(); script.Pass();
            Assert.AreEqual(TutorialStep.Jump, script.Current);
            script.Fail(); script.Fail();
            Assert.AreEqual(TutorialStep.Jump, script.Current); Assert.AreEqual(2, script.Retries); Assert.AreEqual(2, script.TotalRetries);
            script.Pass(); Assert.AreEqual(TutorialStep.Crouch, script.Current); Assert.AreEqual(0, script.Retries); Assert.AreEqual(2, script.TotalRetries);
            Assert.Less(TutorialCourse.RestartZ(TutorialStep.Jump), TutorialCourse.JumpChunk * RouteSurface.Length + 7, "Restart is in front of the step's obstacle");
            Assert.Greater(TutorialCourse.RestartZ(TutorialStep.Jump), TutorialCourse.LaneChunk * RouteSurface.Length + TutorialCourse.LaneBlockZ, "and past the previous one");
        }

        [Test]
        public void TutorialSessionNeverEndsInACapture()
        {
            var run = new RunSession(new RunConfig()); run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            run.IsTutorial = true;
            Assert.IsTrue(run.Hit(true)); Assert.IsTrue(run.Fail(CaptureReason.BrokenRail));
            Assert.AreEqual(GameFlowState.Running, run.Flow.State); Assert.AreEqual(2, run.TutorialMisses); Assert.AreEqual(0, run.Chase.Pressure);
            run.SpeedScale = 0; float z = run.Runner.Z; run.Tick(.5f); Assert.AreEqual(z, run.Runner.Z, "A held tutorial runner does not move");
            run.IsTutorial = false; Assert.AreEqual(0, run.TutorialMisses);
            Assert.IsTrue(run.Hit(true)); Assert.AreEqual(GameFlowState.Caught, run.Flow.State);
        }

        [Test]
        public void TutorialRouteHasAWarmupLapThenTheFork()
        {
            var plan = TutorialCourse.Plan(new RunConfig());
            for (int i = 0; i < TutorialCourse.WarmupLap.Length; i++) Assert.AreEqual(TutorialCourse.WarmupLap[i], plan.TypeAt(i));
            Assert.AreEqual(0, plan.TypeAt(TutorialCourse.ForkLapStart)); Assert.AreEqual(1, plan.TypeAt(TutorialCourse.ForkLapStart + 1));
            Assert.IsFalse(plan.ForkWindow(110)); Assert.IsFalse(plan.IsForkTrackChunk(4));
            plan.UpdateChoice(CartFork.Decision, 1); Assert.IsFalse(plan.ForkLocked(CartFork.Decision), "The warm-up lap has no fork");
            Assert.AreEqual(float.MaxValue, plan.PrefetchLimit(100));
            float lap = TutorialCourse.ForkLapStart * RouteSurface.Length;
            Assert.IsTrue(plan.ForkWindow(lap + 110)); Assert.IsTrue(plan.IsForkTrackChunk(TutorialCourse.ForkLapStart + CartFork.FirstChunk));
            plan.UpdateChoice(TutorialCourse.ForkDecisionZ, -1); Assert.IsTrue(plan.ForkLocked(TutorialCourse.ForkDecisionZ));
            Assert.AreEqual(-1, plan.ChoiceAt(TutorialCourse.ForkLapStart + CartFork.ConnectorChunk));
            Assert.AreEqual(TutorialHazard.LaneBlock, TutorialCourse.HazardAt(TutorialCourse.LaneChunk));
            Assert.AreEqual(TutorialHazard.StairRow, TutorialCourse.HazardAt(TutorialCourse.StairChunk)); Assert.AreEqual(7, plan.TypeAt(TutorialCourse.StairChunk));
            Assert.AreEqual(TutorialHazard.None, TutorialCourse.HazardAt(0));
            Assert.AreEqual(TutorialHazard.RailGap, TutorialCourse.HazardAt(TutorialCourse.RailChunk)); Assert.AreEqual(1, plan.TypeAt(TutorialCourse.RailChunk), "The broken rail is on a cart chunk");
            Assert.AreEqual(1, TutorialCourse.Plan(new RunConfig { ExtendedRoutes = false }).TypeAt(TutorialCourse.RailChunk), "also on the authored lap");
            Assert.Less(TutorialCourse.RestartZ(TutorialStep.Rail), TutorialCourse.RailGapZ - 14f, "A rail retry rides up to the gap again");
            Assert.Greater(TutorialCourse.RestartZ(TutorialStep.Fork), TutorialCourse.RailGapZ, "A fork retry starts past the rail lesson");
            Assert.Less(TutorialCourse.RestartZ(TutorialStep.Fork), TutorialCourse.ForkLapStart * RouteSurface.Length + CartFork.Start, "and before the fork window");
        }

        [Test]
        public void OfferedOncePerSessionToGuestsAndUnfinishedMembers()
        {
            Assert.IsTrue(TutorialScript.ShouldOffer(false, false, false)); Assert.IsTrue(TutorialScript.ShouldOffer(false, true, false));
            Assert.IsTrue(TutorialScript.ShouldOffer(true, false, false)); Assert.IsFalse(TutorialScript.ShouldOffer(true, true, false));
            Assert.IsFalse(TutorialScript.ShouldOffer(false, false, true));
        }
    }
}
