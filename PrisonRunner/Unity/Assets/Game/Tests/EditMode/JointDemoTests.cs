using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    // Exercise plan 10-3 B: the follow-along joint comparison uses the classifier's own lines, and its reference figure
    // clearly crosses them while a standing body does not.
    public sealed class JointDemoTests
    {
        static readonly TutorialStep[] Steps = { TutorialStep.Jump, TutorialStep.Crouch, TutorialStep.HighKnee, TutorialStep.Lane };

        [Test] public void TheReferenceCrossesEachLineAndStandingDoesNot([Values(PlayStyle.FullBody, PlayStyle.UpperBody)] PlayStyle style)
        {
            var tuning = new MotionTuning(); var b = JointDemo.ReferenceBaseline(style);
            Assert.IsTrue(b.Valid);
            foreach (var step in Steps)
            {
                Assert.IsTrue(JointDemo.Target(step, b, tuning).Shown, step + " has a line");
                Assert.IsFalse(JointDemo.Crossed(step, b, tuning, JointDemo.Reference(step, style, 0)), step + ": the loop starts standing");
                bool crossed = false;
                for (int i = 0; i < 40 && !crossed; i++) crossed = JointDemo.Crossed(step, b, tuning, JointDemo.Reference(step, style, JointDemo.LoopSeconds * i / 40));
                Assert.IsTrue(crossed, style + " " + step + ": the demo crosses its line");
            }
        }

        [Test] public void LinesFollowTheTuningAndTheBaseline()
        {
            var b = new PoseBaseline(PlayStyle.FullBody, .5f, .3f, .55f, .25f, .18f, .18f, 4f / 3f);
            var tuning = new MotionTuning();
            Assert.AreEqual(.55f - tuning.JumpRise * .25f, JointDemo.Target(TutorialStep.Jump, b, tuning).At, 1e-6);
            Assert.AreEqual(.3f + tuning.CrouchDrop * .25f, JointDemo.Target(TutorialStep.Crouch, b, tuning).At, 1e-6);
            Assert.AreEqual(.55f + .18f - tuning.HighKneeLift * .25f, JointDemo.Target(TutorialStep.HighKnee, b, tuning).At, 1e-6);
            var lane = JointDemo.Target(TutorialStep.Lane, b, tuning);
            Assert.IsTrue(lane.Vertical); Assert.AreEqual(.5f + tuning.LaneEnter * .25f / (4f / 3f), lane.Other, 1e-6, "Sideways lines in picture-width units");
            Assert.IsFalse(JointDemo.Target(TutorialStep.Banana, b, tuning).Shown); Assert.IsFalse(JointDemo.Compares(TutorialStep.Cadence));
            Assert.IsFalse(JointDemo.Target(TutorialStep.Jump, default, tuning).Shown, "No calibration, no line");
            var noKnees = new PoseBaseline(PlayStyle.FullBody, .5f, .3f, .55f, .25f, float.NaN, .18f, 1);
            Assert.IsFalse(JointDemo.Target(TutorialStep.HighKnee, noKnees, tuning).Shown);
            Assert.IsFalse(JointDemo.Crossed(TutorialStep.Jump, b, tuning, null));
        }
    }
}
