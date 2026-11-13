using Muhanok.Domain;
using Muhanok.Presentation;
using NUnit.Framework;

namespace Muhanok.Tests
{
    // The stair chunk's cue text, knee gauge and camera feel (data only, no pixels). The authored lap's chunk 4 is the prison stair (z 96..120).
    public sealed class StairPresentationTests
    {
        [SetUp] public void Setup() { RouteSurface.UsePlan(null); }
        [TearDown] public void Cleanup() { RouteSurface.UsePlan(null); }
        private static RunSession Running()
        {
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.IntroTimeline);
            run.Flow.Transition(GameFlowState.FirstPersonTakeover); run.Flow.Transition(GameFlowState.Running); return run;
        }

        [Test] public void CueTextFollowsThePlayStyle()
        {
            Assert.AreEqual("무릎 번갈아!", ActionCuePresenter.StairCue(PlayStyle.FullBody));
            Assert.AreEqual("팔 번갈아!", ActionCuePresenter.StairCue(PlayStyle.UpperBody));
            Assert.AreEqual("W 연타!", ActionCuePresenter.StairCue(PlayStyle.Keyboard));
        }
        [Test] public void RunnerCountsEveryAcceptedKneeStep()
        {
            var run = Running();
            Assert.AreEqual(0, run.Runner.KneeSteps);
            run.Runner.Command(MotionCommand.KneeStep); run.Runner.Command(MotionCommand.KneeStep);
            Assert.AreEqual(2, run.Runner.KneeSteps);
            run.Reset(); Assert.AreEqual(0, run.Runner.KneeSteps);
        }
        [Test] public void GaugeIsHiddenOffTheStairsAndWarnsWhenAStepIsNearlyLate()
        {
            var run = Running(); var model = new RunHudModel();
            run.Runner.SetStartZ(60f); run.Tick(.05f); model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Tracking, false, 1);
            Assert.IsFalse(model.ShowStair);
            run.Runner.SetStartZ(106f); run.Runner.Command(MotionCommand.KneeStep); run.Tick(.05f); model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Tracking, false, 2);
            Assert.IsTrue(model.ShowStair); Assert.Greater(model.KneeGauge, .9f); Assert.IsFalse(model.KneeWarning); Assert.Greater(model.StairProgress, 0f);
            // .55 s without a step: 1 - .55/.7 is about .21 of the gauge left, over the .7 urgency mark.
            for (int i = 0; i < 11; i++) run.Tick(.05f);
            model.Tick(run, null, PlayStyle.Keyboard, PoseStatus.Tracking, false, 3);
            Assert.IsTrue(model.KneeWarning); Assert.Less(model.KneeGauge, .3f);
        }
        [Test] public void CameraPitchesUpOnTheStairsAndEasesBack()
        {
            var fx = new StairCameraFx();
            for (int i = 0; i < 4; i++) fx.Tick(.05f, true, 0);
            Assert.Greater(fx.Pitch, 0f); Assert.Less(fx.Pitch, StairCameraFx.PitchDegrees);
            for (int i = 0; i < 20; i++) fx.Tick(.05f, true, 0);
            Assert.AreEqual(StairCameraFx.PitchDegrees, fx.Pitch, .001f);
            for (int i = 0; i < 20; i++) fx.Tick(.05f, false, 0);
            Assert.AreEqual(0f, fx.Pitch, .001f);
        }
        [Test] public void AKneeStepOnTheStairsBobsTheCameraBriefly()
        {
            var fx = new StairCameraFx();
            fx.Tick(.05f, true, 1);
            Assert.IsTrue(fx.Stepped);
            fx.Tick(.05f, true, 1);
            Assert.IsFalse(fx.Stepped); Assert.Greater(fx.Bob, 0f); Assert.LessOrEqual(fx.Bob, StairCameraFx.BobMetres + .0001f);
            for (int i = 0; i < 5; i++) fx.Tick(.05f, true, 1);
            Assert.AreEqual(0f, fx.Bob, .0001f);
            fx.Tick(.05f, false, 2); Assert.IsFalse(fx.Stepped, "Steps off the stairs make no footstep or bob");
        }
    }
}
