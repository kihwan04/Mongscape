using System;
using Muhanok.Domain;
using Muhanok.Presentation;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class RunHudTests
    {
        [SetUp] public void Setup() { RouteSurface.UsePlan(null); }
        [TearDown] public void Cleanup() { RouteSurface.UsePlan(null); }
        private static RunSession Running()
        {
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            run.Workout.SetWeight("70"); run.Workout.SetGoal(180);
            return run;
        }
        [TestCase(PlayStyle.Keyboard, false)]
        [TestCase(PlayStyle.FullBody, true)]
        [TestCase(PlayStyle.UpperBody, true)]
        public void GoalAndBurnCounterShowOnlyForCameraStyles(PlayStyle style, bool expected)
        {
            var model = new RunHudModel(); model.Tick(Running(), null, style, PoseStatus.Tracking, false, 1);
            Assert.AreEqual(expected, model.ShowWorkout);
            if (expected) StringAssert.Contains("180 kcal", model.Workout);
        }
        [TestCase(0f, "플레이 0:00")] [TestCase(192.7f, "플레이 3:12")] [TestCase(-3f, "플레이 0:00")] [TestCase(3600f, "플레이 60:00")]
        public void PlayTimeIsMinutesAndSeconds(float seconds, string expected) { Assert.AreEqual(expected, RunHudModel.PlayTime(seconds)); }
        [Test] public void MissingWeightStillShowsExerciseCounter()
        {
            var run = Running(); run.Workout.SetWeight("");
            var model = new RunHudModel(); model.Tick(run, null, PlayStyle.FullBody, PoseStatus.Tracking, false, 1);
            Assert.IsTrue(model.ShowWorkout); Assert.AreEqual(65,run.Workout.WeightKg);
            StringAssert.Contains("0.0 kcal",model.BurnLabel);Assert.AreEqual(0,model.GoalProgress);
        }
        [Test] public void FractionalGoalAndBurnHaveExplicitLabelsAndKeyboardNeverShowsExerciseBurn()
        {
            var run=Running();run.Workout.SetGoal(80.5);run.Workout.TickActivity(60,true,true,true,0);
            var model=new RunHudModel();model.Tick(run,null,PlayStyle.FullBody,PoseStatus.Tracking,false,1);
            StringAssert.Contains("목표  80.5 kcal",model.GoalLabel);StringAssert.Contains("소모  4.9 kcal",model.BurnLabel);
            model.Tick(run,null,PlayStyle.Keyboard,PoseStatus.Off,false,2);
            Assert.IsFalse(model.ShowWorkout);Assert.AreEqual(0,model.GoalProgress);
        }
        [Test] public void ForkPreviewShowsTheLiveLaneThenCommittedDestinationAndHidesAtExit()
        {
            var run=Running();RouteSurface.UsePlan(new RoutePlan(new RunConfig{ExtendedRoutes=true},723));
            var model=new RunHudModel();run.Runner.SetStartZ(110);run.Runner.Command(MotionCommand.Right);
            model.Tick(run,null,PlayStyle.Keyboard,PoseStatus.Off,false,1);
            Assert.IsTrue(model.ShowFork);Assert.IsFalse(model.ForkCommitted);Assert.AreEqual(1,model.ForkLane);
            RouteSurface.ActivePlan.UpdateChoice(120,1);run.Runner.SetStartZ(132);
            model.Tick(run,null,PlayStyle.Keyboard,PoseStatus.Off,false,2);
            Assert.IsTrue(model.ForkCommitted);StringAssert.Contains("진입",model.ForkCaption);
            run.Runner.SetStartZ(144);model.Tick(run,null,PlayStyle.Keyboard,PoseStatus.Off,false,3);Assert.IsFalse(model.ShowFork);
        }
        [TestCase(true, true, true, true, true, true, HudNoticeKind.Tracking)]
        [TestCase(false, true, true, true, true, true, HudNoticeKind.Obstacle)]
        [TestCase(false, false, true, true, true, true, HudNoticeKind.Fork)]
        [TestCase(false, false, false, true, true, true, HudNoticeKind.Police)]
        [TestCase(false, false, false, false, true, true, HudNoticeKind.Combo)]
        [TestCase(false, false, false, false, false, false, HudNoticeKind.None)]
        public void OnlyTheMostUrgentNoticeWins(bool lost, bool obstacle, bool fork, bool police, bool combo, bool section, HudNoticeKind expected)
            => Assert.AreEqual(expected, RunHudModel.SelectNotice(lost, obstacle, fork, police, combo, section));
        [Test] public void BriefPoseLossDoesNotFlashAndRecoveryClearsWarning()
        {
            var run = Running(); var model = new RunHudModel();
            model.Tick(run, null, PlayStyle.FullBody, PoseStatus.Searching, false, 1);
            model.Tick(run, null, PlayStyle.FullBody, PoseStatus.Searching, false, 1.5f); Assert.IsFalse(model.ShowPoseRecovery); Assert.IsTrue(model.ShowCamera);
            model.Tick(run, null, PlayStyle.FullBody, PoseStatus.Searching, false, 1.8f); Assert.AreEqual(HudNoticeKind.Tracking, model.NoticeKind); Assert.IsTrue(model.ShowPoseRecovery);
            model.Tick(run, null, PlayStyle.FullBody, PoseStatus.Tracking, false, 2); Assert.IsFalse(model.ShowPoseRecovery); Assert.IsNull(model.Notice); Assert.IsTrue(model.ShowCamera, "The small webcam corner stays while tracking normally.");
        }
        // Style × setting × recognition: camera styles always show the corner webcam, switching to recovery after 0.75s of loss.
        [TestCase(PlayStyle.FullBody, true, PoseStatus.Tracking, true, false)]
        [TestCase(PlayStyle.UpperBody, true, PoseStatus.Tracking, true, false)]
        [TestCase(PlayStyle.FullBody, true, PoseStatus.Searching, true, true)]
        [TestCase(PlayStyle.UpperBody, true, PoseStatus.Searching, true, true)]
        [TestCase(PlayStyle.FullBody, false, PoseStatus.Tracking, false, false)]
        [TestCase(PlayStyle.UpperBody, false, PoseStatus.Searching, false, true)]
        [TestCase(PlayStyle.Keyboard, true, PoseStatus.Off, false, false)]
        [TestCase(PlayStyle.Keyboard, false, PoseStatus.Off, false, false)]
        public void InGameCameraFollowsStyleSettingAndTracking(PlayStyle style, bool enabled, PoseStatus pose, bool camera, bool recovery)
        {
            var run = Running(); var model = new RunHudModel { CameraEnabled = enabled };
            model.Tick(run, null, style, pose, false, 1); model.Tick(run, null, style, pose, false, 2);
            Assert.AreEqual(camera, model.ShowCamera); Assert.AreEqual(recovery, model.ShowPoseRecovery);
        }
        [Test] public void DoubleCoinItemMarksTheCoinCounter()
        {
            var run = Running(); var model = new RunHudModel();
            model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Off, false, 1); Assert.IsFalse(model.CoinBoost);
            run.Items.Activate(ItemKind.DoubleScore); model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Off, false, 2); Assert.IsTrue(model.CoinBoost);
            StringAssert.StartsWith("코인 2배", model.ItemLabels[(int)ItemKind.DoubleScore]);
        }
        [Test] public void InGameCameraHidesOutsideRunning()
        {
            var run = Running(); var model = new RunHudModel();
            model.Tick(run, null, PlayStyle.FullBody, PoseStatus.Tracking, false, 1); Assert.IsTrue(model.ShowCamera);
            run.Paused = true; model.Tick(run, null, PlayStyle.FullBody, PoseStatus.Tracking, false, 2); Assert.IsFalse(model.ShowCamera);
        }
        [Test] public void CueSearchIsBoundedAndPauseSkipsWork()
        {
            var run = Running(); var model = new RunHudModel();
            for (int i = 0; i < 600; i++) model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Off, false, i / 60f);
            Assert.That(model.CueScans, Is.InRange(85, 101), "Ten seconds at 60fps should need about 100 searches, not 600 or multiple OnGUI passes.");
            int before = model.CueScans; run.Paused = true;
            for (int i = 0; i < 60; i++) model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Off, false, 10 + i / 60f);
            Assert.AreEqual(before, model.CueScans); Assert.IsNull(model.Notice);
            run.Paused = false; model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Off, false, 11); Assert.AreEqual(before + 1, model.CueScans);
        }
        [Test] public void ComboAndPoliceMessagesExpireInsteadOfRemainingOnScreen()
        {
            var run = Running(); var model = new RunHudModel(); run.RecordDodge(); run.RecordDodge();
            model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Off, false, 1); Assert.AreEqual(HudNoticeKind.Combo, model.NoticeKind);
            model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Off, false, 2.2f); Assert.IsNull(model.Notice);
            model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Off, true, 3); Assert.AreEqual(HudNoticeKind.Police, model.NoticeKind);
            model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Off, true, 5.3f); Assert.IsNull(model.Notice);
        }
        [Test] public void SteadyHudSamplingDoesNotAllocateManagedMemory()
        {
            var run = Running(); var model = new RunHudModel();
            for (int i = 0; i < 60; i++) model.Tick(run, null, PlayStyle.FullBody, PoseStatus.Tracking, false, i);
            string label = model.Workout;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 3600; i++) model.Tick(run, null, PlayStyle.FullBody, PoseStatus.Tracking, false, 60 + i / 60f);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreSame(label, model.Workout); Assert.AreEqual(0, bytes, "Unchanged counters should not format strings or allocate each frame. This excludes Unity rendering and changing values.");
        }
        [Test] public void SirenRemainsVisibleWhenAForkInstructionTakesPriorityAndHidesWhenPausedOrCaught()
        {
            var run=Running();var model=new RunHudModel();
            RouteSurface.UsePlan(new RoutePlan(new RunConfig{ExtendedRoutes=true},723));run.Runner.SetStartZ(110);
            model.Tick(run,null,PlayStyle.Keyboard,PoseStatus.Off,true,1);
            Assert.IsTrue(model.ShowSiren);Assert.AreEqual(HudNoticeKind.Fork,model.NoticeKind);
            model.Tick(run,null,PlayStyle.Keyboard,PoseStatus.Off,true,5);Assert.IsTrue(model.ShowSiren);
            run.Paused=true;model.Tick(run,null,PlayStyle.Keyboard,PoseStatus.Off,true,6);Assert.IsFalse(model.ShowSiren);
            run.Paused=false;model.Tick(run,null,PlayStyle.Keyboard,PoseStatus.Off,true,7);Assert.IsTrue(model.ShowSiren);
            run.Hit(true);model.Tick(run,null,PlayStyle.Keyboard,PoseStatus.Off,true,8);Assert.IsFalse(model.ShowSiren);
        }
    }
}
