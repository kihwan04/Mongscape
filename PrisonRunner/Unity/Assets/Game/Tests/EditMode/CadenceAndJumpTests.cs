using System;
using System.Collections.Generic;
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    // Steps a minute from the knees, the jump's knee symmetry and the per-person bounce line, camera framing.
    public sealed class CadenceAndJumpTests
    {
        // Same stick body as BodyTrackingAndExhibitionTests, with separate ankles (the standing torso is .27 image units).
        private sealed class Body { public float Dy, KneeL, KneeR, AnkleL, AnkleR, Dx; public bool Head = true, Feet = true; }
        private static PoseFrame Frame(Body b, double time)
        {
            var frame = new PoseFrame { HasPose = true, Time = time, Aspect = 4f / 3f };
            void Set(int i, float x, float y, float v = .95f) => frame.Points[i] = new PoseLandmark { X = x + b.Dx, Y = y, Visibility = v };
            for (int i = 0; i < PoseFrame.Count; i++) Set(i, .5f, .5f, .2f);
            float sy = .35f + b.Dy, hy = .62f + b.Dy;
            Set(0, .5f, .25f + b.Dy, b.Head ? .95f : .1f); Set(11, .44f, sy); Set(12, .56f, sy);
            Set(15, .41f, .6f + b.Dy); Set(16, .59f, .6f + b.Dy);
            Set(23, .46f, hy); Set(24, .54f, hy);
            Set(25, .46f, .78f + b.Dy - b.KneeL); Set(26, .54f, .78f + b.Dy - b.KneeR);
            float feet = b.Feet ? .95f : .1f;
            Set(27, .46f, .93f + b.Dy - b.AnkleL, feet); Set(28, .54f, .93f + b.Dy - b.AnkleR, feet);
            return frame;
        }
        private static MotionClassifier Calibrated(MotionTuning tuning = null)
        {
            var classifier = new MotionClassifier(tuning ?? new MotionTuning()); classifier.SetStyle(PlayStyle.FullBody);
            for (int i = 0; i < 30; i++) classifier.Update(Frame(new Body(), i / 30.0));
            Assert.IsTrue(classifier.Calibrated);
            return classifier;
        }
        // Running in place: one knee up per step, the body rising by bounce while it is up (a step needs the body to bob).
        private static Body Pace(double t, double stepsPerMinute, float knee = .045f, float bounce = .006f)
        {
            double phase = t * stepsPerMinute / 60; int step = (int)Math.Floor(phase);
            float lift = (float)Math.Sin(Math.PI * (phase - step));
            var body = new Body { Dy = -bounce * lift };
            if (step % 2 == 0) { body.KneeL = knee * lift; body.AnkleL = knee * .8f * lift; } else { body.KneeR = knee * lift; body.AnkleR = knee * .8f * lift; }
            return body;
        }
        private static int Run(MotionClassifier classifier, Func<double, Body> script, double from, double to)
        {
            int jumps = 0;
            for (double t = from; t < to; t += 1 / 30.0) if (classifier.Update(Frame(script(t), 1 + t)).JumpEdge) jumps++;
            return jumps;
        }

        [TestCase(90, CadenceStage.Walk)] [TestCase(125, CadenceStage.Run)] [TestCase(170, CadenceStage.Sprint)]
        public void StepsPerMinuteFollowTheAlternatingKneesOverThreeSeconds(double pace, CadenceStage stage)
        {
            var classifier = Calibrated();
            Run(classifier, t => Pace(t, pace), 0, 5);
            Assert.AreEqual(pace, classifier.StepsPerMinute, pace * .05, "A steady pace reads its own rate");
            Assert.AreEqual(stage, classifier.CadenceStage);
        }
        [Test] public void CadenceStagesSplitAtOneHundredAndOneHundredFifty()
        {
            var tuning = new MotionTuning();
            Assert.AreEqual(CadenceStage.Standing, Cadence.Stage(0, tuning));
            Assert.AreEqual(CadenceStage.Walk, Cadence.Stage(100, tuning));
            Assert.AreEqual(CadenceStage.Run, Cadence.Stage(101, tuning));
            Assert.AreEqual(CadenceStage.Run, Cadence.Stage(149, tuning));
            Assert.AreEqual(CadenceStage.Sprint, Cadence.Stage(150, tuning));
        }
        [Test] public void StoppingDropsTheCadenceToStanding()
        {
            var classifier = Calibrated();
            Run(classifier, t => Pace(t, 140), 0, 4);
            Run(classifier, t => new Body(), 4, 4.6);
            Assert.Greater(classifier.StepsPerMinute, 0f, "The rate falls off, it does not vanish at once");
            Assert.Less(classifier.StepsPerMinute, 140f);
            Run(classifier, t => new Body(), 4.6, 6);
            Assert.AreEqual(0f, classifier.StepsPerMinute); Assert.AreEqual(CadenceStage.Standing, classifier.CadenceStage);
        }
        [Test] public void KneesMovingInTheAirAreNotSteps()
        {
            var classifier = Calibrated();
            // Five jumps, each with the knees swapping twice while airborne.
            Run(classifier, t =>
            {
                double p = t % 1.0; bool air = p >= .3 && p < .7;
                var body = new Body { Dy = air ? -.09f : 0, AnkleL = air ? .06f : 0, AnkleR = air ? .06f : 0 };
                if (air) { if (p < .45) body.KneeL = .03f; else if (p >= .55) body.KneeR = .03f; }
                return body;
            }, 0, 5);
            Assert.AreEqual(0f, classifier.StepsPerMinute);
        }
        [Test] public void ARiseWithOneKneeUpIsARunningStepNotAJump()
        {
            // Hips and shoulders up a third of the torso (well past the jump line), feet off the ground, but one knee high.
            Func<double, Body> lopsided = t => t >= .2 && t < .6 ? new Body { Dy = -.09f, KneeL = .07f, AnkleL = .08f, AnkleR = .04f } : new Body();
            Assert.AreEqual(0, Run(Calibrated(), lopsided, 0, 1.2));
            Func<double, Body> level = t => t >= .2 && t < .6 ? new Body { Dy = -.09f, KneeL = .02f, KneeR = .02f, AnkleL = .06f, AnkleR = .06f } : new Body();
            Assert.AreEqual(1, Run(Calibrated(), level, 0, 1.2));
            // The old rule (no symmetry check) took the lopsided rise as a jump.
            Assert.AreEqual(1, Run(Calibrated(new MotionTuning { JumpKneeSymmetry = 99 }), lopsided, 0, 1.2));
        }
        [Test] public void WhileRunningTheJumpLineRisesWithThePlayersOwnBounce()
        {
            var tuning = new MotionTuning();
            var classifier = Calibrated(tuning);
            Assert.AreEqual(tuning.JumpRise, classifier.JumpThreshold, 1e-4, "Standing: the plain jump line");
            // Bouncing 0.15 torso a step (0.04 image units). The standing line drifts toward the running posture, so the
            // bounce reads as the rise above that.
            Run(classifier, t => Pace(t, 150, .06f, .04f), 0, 6);
            Assert.Greater(classifier.Bounce, .065f); Assert.Less(classifier.Bounce, .15f);
            Assert.AreEqual(Math.Max(tuning.JumpRise, classifier.Bounce * tuning.JumpBounceFactor), classifier.JumpThreshold, 1e-4);
            Assert.Greater(classifier.JumpThreshold, tuning.JumpRise);
            // A level-kneed hop that clears the plain line but stays inside the running one is no jump.
            Assert.AreEqual(0, Run(classifier, t => new Body { Dy = -.07f, AnkleL = .05f, AnkleR = .05f }, 6, 6.3), "Inside the running bounce");
            Assert.AreEqual(1, Run(Calibrated(tuning), t => t >= .2 && t < .5 ? new Body { Dy = -.046f, AnkleL = .05f, AnkleR = .05f } : new Body(), 0, 1), "A hop past the plain line standing is a jump");
            // A real jump while running still counts.
            var running = Calibrated(tuning);
            Run(running, t => Pace(t, 150, .06f, .04f), 0, 6);
            Assert.AreEqual(1, Run(running, t => t >= 6.1 && t < 6.45 ? new Body { Dy = -.12f, AnkleL = .08f, AnkleR = .08f } : Pace(t, 150, .06f, .04f), 6, 7.5));
        }
        [Test] public void AHeavySprintBouncePastTheOldLineIsNoJump()
        {
            // Bouncing 0.17 torso on every step, above the plain 0.15 jump line.
            Func<double, Body> sprint = t => Pace(t, 175, .07f, .045f);
            Assert.AreEqual(0, Run(Calibrated(), sprint, 0, 20));
            Assert.Greater(Run(Calibrated(new MotionTuning { JumpKneeSymmetry = 99, JumpBounceFactor = 0 }), sprint, 0, 20), 10, "The old rule fired on the bounce");
        }
        [Test] public void MovingDuringCalibrationIsReportedAndRestartsIt()
        {
            var classifier = new MotionClassifier(new MotionTuning()); classifier.SetStyle(PlayStyle.FullBody);
            for (int i = 0; i < 10; i++) classifier.Update(Frame(new Body(), i / 30.0));
            Assert.Greater(classifier.CalibrationProgress, 0f); Assert.IsFalse(classifier.CalibrationMoving);
            classifier.Update(Frame(new Body { Dx = .1f }, 10 / 30.0));
            Assert.IsTrue(classifier.CalibrationMoving); Assert.Less(classifier.CalibrationProgress, .1f);
            for (int i = 11; i < 40; i++) classifier.Update(Frame(new Body { Dx = .1f }, i / 30.0));
            Assert.IsTrue(classifier.Calibrated); Assert.IsFalse(classifier.CalibrationMoving);
        }

        [Test] public void CameraFramingAsksForTheWholeBody()
        {
            Assert.AreEqual(FramingIssue.Ok, CameraFraming.Check(Frame(new Body(), 0), PlayStyle.FullBody));
            Assert.AreEqual(FramingIssue.StepBack, CameraFraming.Check(Frame(new Body { Feet = false }, 0), PlayStyle.FullBody));
            Assert.AreEqual(FramingIssue.HeadCut, CameraFraming.Check(Frame(new Body { Head = false }, 0), PlayStyle.FullBody));
            Assert.AreEqual(FramingIssue.StepBack, CameraFraming.Check(Frame(new Body { Dy = .1f }, 0), PlayStyle.FullBody), "Ankles below the picture");
            Assert.AreEqual(FramingIssue.MoveRight, CameraFraming.Check(Frame(new Body { Dx = -.33f }, 0), PlayStyle.FullBody));
            Assert.AreEqual(FramingIssue.MoveLeft, CameraFraming.Check(Frame(new Body { Dx = .33f }, 0), PlayStyle.FullBody));
            Assert.AreEqual(FramingIssue.NoBody, CameraFraming.Check(new PoseFrame(), PlayStyle.FullBody));
            // The upper body only needs the head and shoulders; the keyboard needs nothing.
            Assert.AreEqual(FramingIssue.Ok, CameraFraming.Check(Frame(new Body { Feet = false }, 0), PlayStyle.UpperBody));
            Assert.AreEqual(FramingIssue.Ok, CameraFraming.Check(new PoseFrame(), PlayStyle.Keyboard));
            // Too far: a small figure.
            var far = Frame(new Body(), 0);
            for (int i = 0; i < PoseFrame.Count; i++) { var p = far.Points[i]; p.Y = .4f + (p.Y - .5f) * .4f; far.Points[i] = p; }
            Assert.AreEqual(FramingIssue.StepCloser, CameraFraming.Check(far, PlayStyle.FullBody));
            Assert.AreEqual("발끝까지 보이게 한 걸음 뒤로 가 주세요", CameraFraming.Hint(FramingIssue.StepBack, PlayStyle.FullBody));
        }
        [Test] public void FramingHintHoldsBeforeChanging()
        {
            var guide = new FramingGuide();
            guide.Update(Frame(new Body(), 0), PlayStyle.FullBody, 0);
            Assert.AreEqual(FramingIssue.NoBody, guide.Issue, "A new reading waits");
            guide.Update(Frame(new Body(), .5), PlayStyle.FullBody, .5);
            Assert.AreEqual(FramingIssue.Ok, guide.Issue);
            guide.Update(Frame(new Body { Feet = false }, .6), PlayStyle.FullBody, .6);
            guide.Update(Frame(new Body(), .7), PlayStyle.FullBody, .7);
            guide.Update(Frame(new Body { Feet = false }, .8), PlayStyle.FullBody, .8);
            Assert.AreEqual(FramingIssue.Ok, guide.Issue, "A flicker does not change it");
            guide.Update(Frame(new Body { Feet = false }, 1.3), PlayStyle.FullBody, 1.3);
            Assert.AreEqual(FramingIssue.StepBack, guide.Issue);
        }
    }
}
