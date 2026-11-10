using System;
using System.Collections.Generic;
using System.IO;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class BodyTrackingAndExhibitionTests
    {
        // Synthetic standing body in normalised image coordinates; offsets move parts of it.
        // Lean tilts the upper body sideways: shoulders, head and arms move, hips and feet stay.
        // Roll tilts the shoulder line: the +X shoulder drops by Roll and the other one rises by Roll, the centre stays.
        private sealed class Body { public float Dx, Dy, Lean, Roll, Shoulder, Hip, KneeL, KneeR, Ankle, WristL = .6f, WristR = .6f; public bool Legs = true; }
        private static PoseFrame Frame(Body b, double time)
        {
            var frame = new PoseFrame { HasPose = true, Time = time, Aspect = 4f / 3f };
            void Set(int i, float x, float y, float v = .95f) => frame.Points[i] = new PoseLandmark { X = x + b.Dx, Y = y, Visibility = v };
            for (int i = 0; i < PoseFrame.Count; i++) Set(i, .5f, .5f, .2f);
            float sy = .35f + b.Dy + b.Shoulder, hy = .62f + b.Dy + b.Hip, legs = b.Legs ? .95f : .1f, l = b.Lean;
            Set(0, .5f + l, .25f + b.Dy + b.Shoulder); Set(11, .44f + l, sy - b.Roll); Set(12, .56f + l, sy + b.Roll);
            Set(23, .46f, hy, legs); Set(24, .54f, hy, legs);
            Set(25, .46f, .78f + b.Dy + b.Hip * .5f - b.KneeL, legs); Set(26, .54f, .78f + b.Dy + b.Hip * .5f - b.KneeR, legs);
            Set(27, .46f, .93f + b.Dy - b.Ankle, legs); Set(28, .54f, .93f + b.Dy - b.Ankle, legs);
            Set(15, .41f + l, b.WristL + b.Dy); Set(16, .59f + l, b.WristR + b.Dy);
            return frame;
        }
        private static List<string> Play(PlayStyle style, Func<double, Body> script, double seconds, Func<double, bool> cart = null)
        {
            var classifier = new MotionClassifier(new MotionTuning()); classifier.SetStyle(style);
            for (int i = 0; i < 30; i++) classifier.Update(Frame(new Body { Legs = style == PlayStyle.FullBody }, i / 30.0));
            Assert.AreEqual(PoseStatus.Tracking, classifier.Status);
            var events = new List<string>(); int lane = 0; bool crouch = false, knee = false;
            for (double t = 0; t < seconds; t += 1 / 30.0)
            {
                var body = script(t); body.Legs = style == PlayStyle.FullBody;
                classifier.SetCart(cart != null && cart(t));
                var r = classifier.Update(Frame(body, 1 + t));
                if (r.Lane != lane) { events.Add("lane" + r.Lane); lane = r.Lane; }
                if (r.JumpEdge) events.Add("jump");
                if (r.Crouch != crouch) { events.Add(r.Crouch ? "crouch" : "stand"); crouch = r.Crouch; }
                if (r.HighKnee != knee) { if (r.HighKnee) events.Add("knee"); knee = r.HighKnee; }
            }
            return events;
        }
        private static bool In(double t, double a, double b) => t >= a && t < b;

        [Test] public void FullBodyStepsSelectLanesAndReturnToCentre()
        {
            CollectionAssert.AreEqual(new[] { "lane-1", "lane0" }, Play(PlayStyle.FullBody, t => new Body { Dx = In(t, .2, .8) ? -.14f : 0 }, 1.5));
            CollectionAssert.AreEqual(new[] { "lane-1", "lane1", "lane0" }, Play(PlayStyle.FullBody, t => new Body { Dx = In(t, .2, .6) ? -.14f : In(t, .6, 1) ? .14f : 0 }, 1.5));
        }
        [Test] public void OnFootOnlyASideStepChangesLaneNotALean()
        {
            // Shoulders well over to the left (about 20 degrees) with the hips in place: still the centre lane.
            Assert.IsEmpty(Play(PlayStyle.FullBody, t => new Body { Lean = In(t, .2, .8) ? -.09f : 0 }, 1.5));
        }
        [Test] public void InTheCartOnlyALeanTiltsItNotASideStep()
        {
            Func<double, bool> riding = t => true;
            CollectionAssert.AreEqual(new[] { "lane-1", "lane0" }, Play(PlayStyle.FullBody, t => new Body { Lean = In(t, .2, .8) ? -.07f : 0 }, 1.5, riding));
            CollectionAssert.AreEqual(new[] { "lane1", "lane0" }, Play(PlayStyle.FullBody, t => new Body { Lean = In(t, .2, .8) ? .07f : 0 }, 1.5, riding));
            // A whole-body step into the side zone keeps the shoulders over the hips: the cart stays upright.
            Assert.IsEmpty(Play(PlayStyle.FullBody, t => new Body { Dx = In(t, .2, .8) ? -.14f : 0 }, 1.5, riding));
            // A small sway is not a lean.
            Assert.IsEmpty(Play(PlayStyle.FullBody, t => new Body { Lean = In(t, .2, .8) ? -.03f : 0 }, 1.5, riding));
            // Ducking in the cart with a little sideways wobble stays a duck.
            CollectionAssert.AreEqual(new[] { "crouch", "stand" }, Play(PlayStyle.FullBody, t => { bool duck = In(t, .2, 1.2); return new Body { Shoulder = duck ? .1f : 0, Hip = duck ? .04f : 0, Lean = duck ? .03f : 0 }; }, 2, riding));
        }
        [Test] public void BoardingAndLeavingTheCartRereadsTheZone()
        {
            // Standing in the left zone on foot, then boarding upright: the cart reads centre at once, and stepping back on foot reads left again.
            CollectionAssert.AreEqual(new[] { "lane-1", "lane0", "lane-1" }, Play(PlayStyle.FullBody, t => new Body { Dx = t > .2 ? -.14f : 0 }, 2, t => In(t, .7, 1.4)));
            // Leaning in the cart, then leaving it upright: back on foot the hips are centred.
            CollectionAssert.AreEqual(new[] { "lane1", "lane0" }, Play(PlayStyle.FullBody, t => new Body { Lean = .07f }, 1.5, t => t < .7));
        }
        [Test] public void UpperBodyLeansTheSameOnFootAndInTheCart()
        {
            CollectionAssert.AreEqual(new[] { "lane-1", "lane0" }, Play(PlayStyle.UpperBody, t => new Body { Dx = In(t, .2, .8) ? -.10f : 0 }, 1.3, t => true));
        }
        // Half the height change between the shoulders for a shoulder line tilted this many degrees (shoulders .16 apart in aspect units).
        private static float Roll(double degrees) => (float)(.12 * 4 / 3 * Math.Tan(degrees * Math.PI / 180) / 2);
        [Test] public void UpperBodyTiltingTheShouldersChangesLaneOnFootAndInTheCart()
        {
            CollectionAssert.AreEqual(new[] { "lane1", "lane0" }, Play(PlayStyle.UpperBody, t => new Body { Roll = In(t, .2, .8) ? Roll(15) : 0 }, 1.3));
            CollectionAssert.AreEqual(new[] { "lane-1", "lane0" }, Play(PlayStyle.UpperBody, t => new Body { Roll = In(t, .2, .8) ? -Roll(15) : 0 }, 1.3, t => true));
        }
        [Test] public void UpperBodySmallShoulderTiltStaysInTheCentre()
        {
            Assert.IsEmpty(Play(PlayStyle.UpperBody, t => new Body { Roll = In(t, .2, .8) ? Roll(8) : 0 }, 1.3));
        }
        [Test] public void UpperBodyShoulderTiltWhileRaisingOneHandIsNotALean()
        {
            Assert.IsEmpty(Play(PlayStyle.UpperBody, t => new Body { Roll = In(t, .2, .8) ? Roll(15) : 0, WristL = In(t, .15, .85) ? .2f : .6f }, 1.3));
        }
        [Test] public void UpperBodyShoulderTiltFromBigArmPumpsIsNotALean()
        {
            // The shoulder on the side of the raised arm lifts with each pump.
            var events = Play(PlayStyle.UpperBody, t => { int s = (int)(t / .3) % 2; bool on = In(t, .2, 1.8); return new Body { WristL = on ? (s == 0 ? .4f : .62f) : .6f, WristR = on ? (s == 1 ? .4f : .62f) : .6f, Roll = on ? (s == 0 ? Roll(15) : -Roll(15)) : 0 }; }, 2.0);
            Assert.IsFalse(events.Exists(e => e.StartsWith("lane")), string.Join(",", events));
        }
        [Test] public void UpperBodyCalibratedShoulderTiltIsTheCentre()
        {
            var classifier = new MotionClassifier(new MotionTuning()); classifier.SetStyle(PlayStyle.UpperBody);
            for (int i = 0; i < 30; i++) classifier.Update(Frame(new Body { Legs = false, Roll = Roll(15) }, i / 30.0));
            Assert.AreEqual(PoseStatus.Tracking, classifier.Status);
            for (double t = 0; t < 1; t += 1 / 30.0) Assert.AreEqual(0, classifier.Update(Frame(new Body { Legs = false, Roll = Roll(15) }, 1 + t)).Lane, "The calibrated posture is upright");
            int lane = 0; for (double t = 1; t < 2; t += 1 / 30.0) lane = classifier.Update(Frame(new Body { Legs = false }, 1 + t)).Lane;
            Assert.AreEqual(-1, lane, "Straightening a calibrated tilt is a lean the other way");
        }
        [Test] public void SmallShiftsStayInTheCentreZone()
        {
            // A third of a torso (the old lane line) is a sway, not a move into the side zone.
            Assert.IsEmpty(Play(PlayStyle.FullBody, t => new Body { Dx = In(t, .2, .8) ? -.08f : 0 }, 1.5));
            Assert.IsEmpty(Play(PlayStyle.UpperBody, t => new Body { Dx = In(t, .2, .8) ? -.06f : 0 }, 1.3));
        }
        [Test] public void DipBeforeJumpIsNotASlideAndRepeatedJumpsAllCount()
        {
            CollectionAssert.AreEqual(new[] { "jump" }, Play(PlayStyle.FullBody, t => new Body { Dy = In(t, .2, .3) ? .085f : In(t, .3, .6) ? -.06f : 0, Ankle = In(t, .3, .6) ? .06f : 0 }, 1.5));
            var three = Play(PlayStyle.FullBody, t => { double p = (t - .2) % .5; bool up = t > .2 && t < 1.6 && p < .28; return new Body { Dy = up ? -.06f : 0, Ankle = up ? .06f : 0 }; }, 1.8);
            Assert.AreEqual(3, three.FindAll(e => e == "jump").Count);
            Assert.IsFalse(three.Contains("crouch"));
        }
        [Test] public void KneeBendBeforeJumpIsNotACrouch()
        {
            // 0.25 s knee bend (shoulders down a third of the torso), then take-off: one jump, no crouch.
            CollectionAssert.AreEqual(new[] { "jump" }, Play(PlayStyle.FullBody, t => new Body { Dy = In(t, .2, .45) ? .09f : In(t, .45, .75) ? -.06f : 0, Ankle = In(t, .45, .75) ? .06f : 0 }, 1.5));
            // A deeper wind-up that already confirmed as a crouch is cancelled by the take-off, which still counts.
            CollectionAssert.AreEqual(new[] { "crouch", "stand", "jump" }, Play(PlayStyle.FullBody, t => new Body { Dy = In(t, .2, .55) ? .13f : In(t, .55, .85) ? -.06f : 0, Ankle = In(t, .55, .85) ? .06f : 0 }, 1.5));
        }
        [Test] public void LowHopLandingIsNotACrouch()
        {
            // Hips rise 0.10 torso (under the jump line), then the landing bends well past the crouch line for 0.3 s.
            Assert.IsEmpty(Play(PlayStyle.FullBody, t => new Body { Dy = In(t, .2, .45) ? -.027f : In(t, .45, .75) ? .13f : 0 }, 1.5));
        }
        [Test] public void RealCrouchHeldHalfASecondCountsOnce()
        {
            // Shoulders down half a torso for 0.5 s.
            CollectionAssert.AreEqual(new[] { "crouch", "stand" }, Play(PlayStyle.FullBody, t => new Body { Dy = In(t, .2, .7) ? .135f : 0 }, 1.5));
        }
        [Test] public void HeldCrouchAndAlternatingKneesAreHeldActions()
        {
            CollectionAssert.AreEqual(new[] { "crouch", "stand" }, Play(PlayStyle.FullBody, t => new Body { Shoulder = In(t, .2, 1.2) ? .12f : 0, Hip = In(t, .2, 1.2) ? .04f : 0 }, 1.6));
            CollectionAssert.AreEqual(new[] { "knee" }, Play(PlayStyle.FullBody, t => { int s = (int)((t - .2) / .3) % 2; bool on = In(t, .2, 1.6); return new Body { KneeL = on && s == 0 ? .09f : 0, KneeR = on && s == 1 ? .09f : 0 }; }, 1.8));
        }
        [Test] public void StandingStillAndJoggingInPlaceTriggerNothing()
        {
            Assert.IsEmpty(Play(PlayStyle.FullBody, t => new Body(), 3));
            Assert.IsEmpty(Play(PlayStyle.FullBody, t => { int s = (int)(t / .3) % 2; return new Body { KneeL = s == 0 ? .025f : 0, KneeR = s == 1 ? .025f : 0 }; }, 3));
        }
        [Test] public void UpperBodyUsesLeanHandsDuckAndArmPumps()
        {
            CollectionAssert.AreEqual(new[] { "lane-1", "lane0" }, Play(PlayStyle.UpperBody, t => new Body { Dx = In(t, .2, .8) ? -.10f : 0 }, 1.3));
            CollectionAssert.AreEqual(new[] { "jump" }, Play(PlayStyle.UpperBody, t => new Body { WristL = In(t, .2, .6) ? .08f : .6f, WristR = In(t, .2, .6) ? .08f : .6f }, 1.2));
            CollectionAssert.AreEqual(new[] { "crouch", "stand" }, Play(PlayStyle.UpperBody, t => new Body { Shoulder = In(t, .2, 1) ? .07f : 0 }, 1.4));
            CollectionAssert.AreEqual(new[] { "knee" }, Play(PlayStyle.UpperBody, t => { int s = (int)(t / .3) % 2; bool on = In(t, .2, 1.8); return new Body { WristL = on ? (s == 0 ? .4f : .62f) : .6f, WristR = on ? (s == 1 ? .4f : .62f) : .6f }; }, 2.0));
        }
        [Test] public void ShortDropoutKeepsTrackingAndVanishingWhileDuckingCountsAsDuck()
        {
            var classifier = new MotionClassifier(new MotionTuning()); classifier.SetStyle(PlayStyle.UpperBody);
            for (int i = 0; i < 30; i++) classifier.Update(Frame(new Body { Legs = false }, i / 30.0));
            classifier.Update(new PoseFrame { HasPose = false, Time = 1.1 });
            Assert.AreEqual(PoseStatus.Tracking, classifier.Status, "A brief dropout must not cancel tracking");
            classifier.Update(Frame(new Body { Legs = false }, 1.2));
            // Start ducking, then the body leaves the camera view mid-duck.
            for (int i = 0; i < 4; i++) classifier.Update(Frame(new Body { Legs = false, Shoulder = .04f }, 1.3 + i / 30.0));
            var reading = classifier.Update(new PoseFrame { HasPose = false, Time = 1.5 });
            Assert.IsTrue(reading.Crouch); Assert.AreEqual(PoseStatus.Tracking, classifier.Status);
            reading = classifier.Update(new PoseFrame { HasPose = false, Time = 3.5 });
            Assert.IsFalse(reading.Crouch); Assert.AreEqual(PoseStatus.Searching, classifier.Status);
        }
        [Test] public void SmootherSuppressesJitterButFollowsRealMoves()
        {
            var smoother = new LandmarkSmoother(); var tuning = new MotionTuning();
            var random = new Random(3); double raw = 0, smooth = 0; float last = 0;
            for (int i = 0; i < 90; i++)
            {
                float dx = (float)(random.NextDouble() - .5) * .01f;
                var frame = Frame(new Body { Dx = dx }, i / 30.0); smoother.Apply(frame, tuning);
                if (i > 15) { raw += dx * dx; smooth += Math.Pow(frame.Points[11].X - .44f, 2); }
            }
            Assert.Less(smooth, raw * .5, "Standing jitter should at least halve");
            for (int i = 0; i < 15; i++) { var frame = Frame(new Body { Dx = -.1f }, 3 + i / 30.0); smoother.Apply(frame, tuning); last = frame.Points[11].X; }
            Assert.Less(Math.Abs(last - .34f), .01f, "A real step must arrive within half a second");
        }
        [Test] public void NoisyDucksDoNotLeakIntoLaneOrJump()
        {
            var random = new Random(11);
            float Noise() => (float)(random.NextDouble() - .5f) * .06f;
            // Upper body: ducking sways the shoulders and drops the nose under the raised-hands line.
            var upper = Play(PlayStyle.UpperBody, t => { bool duck = In(t, .2, 1.2); return new Body { Shoulder = duck ? .09f : 0, Dx = duck ? Noise() : 0, WristL = duck ? .45f : .6f, WristR = duck ? .45f : .6f }; }, 2);
            CollectionAssert.AreEqual(new[] { "crouch", "stand" }, upper);
            var full = Play(PlayStyle.FullBody, t => { bool duck = In(t, .2, 1.2); return new Body { Shoulder = duck ? .1f : 0, Hip = duck ? .04f : 0, Dx = duck ? Noise() : 0 }; }, 2);
            CollectionAssert.AreEqual(new[] { "crouch", "stand" }, full);
        }
        [Test] public void OneFrameSwayDoesNotChangeLane()
        {
            Assert.IsEmpty(Play(PlayStyle.FullBody, t => new Body { Dx = In(t, .5, .55) ? -.16f : 0 }, 1.2));
        }
        // Time of the first lane change while the body follows the script (30 fps), or -1.
        private static double FirstLaneChange(MotionTuning tuning, Func<double, Body> script, double seconds)
        {
            var classifier = new MotionClassifier(tuning); classifier.SetStyle(PlayStyle.FullBody);
            for (int i = 0; i < 30; i++) classifier.Update(Frame(new Body(), i / 30.0));
            for (double t = 0; t < seconds; t += 1 / 30.0) if (classifier.Update(Frame(script(t), 1 + t)).Lane != 0) return t;
            return -1;
        }
        [Test] public void AMovingSideStepIsReadAheadOfTheBody()
        {
            // A side step over 0.3 s into the left zone.
            Func<double, Body> step = t => new Body { Dx = -.14f * (float)Math.Max(0, Math.Min(1, (t - .2) / .3)) };
            double predicted = FirstLaneChange(new MotionTuning(), step, 1.5);
            double plain = FirstLaneChange(new MotionTuning { LanePredictSeconds = 0 }, step, 1.5);
            Assert.Greater(predicted, 0); Assert.Greater(plain, 0);
            Assert.Less(predicted, plain - .02, "Reading the lane ahead must change it sooner");
        }
        [Test] public void AFastStepThatStopsShortOfTheZoneKeepsTheCentre()
        {
            // About 0.6 torso lengths aside (the zone starts at 0.65) in 0.1 s, then standing there.
            Assert.IsEmpty(Play(PlayStyle.FullBody, t => new Body { Dx = .12f * (float)Math.Max(0, Math.Min(1, (t - .2) / .1)) }, 1.5));
        }
        [Test] public void SettingsPresetsChangeGraphicsOnlyAndClampKeepsValuesSafe()
        {
            var settings = new UserSettings { MusicVolume = .2f, LaneSensitivity = 1.2f };
            settings.ApplyPreset(QualityPreset.Low);
            Assert.AreEqual(.7f, settings.RenderScale, .001f); Assert.AreEqual(ShadowSetting.Off, settings.Shadows); Assert.IsFalse(settings.AmbientOcclusion); Assert.AreEqual(30, settings.FrameCap);
            Assert.AreEqual(.2f, settings.MusicVolume, .001f); Assert.AreEqual(1.2f, settings.LaneSensitivity, .001f);
            settings.RenderScale = 3; settings.FrameCap = 45; settings.LaneSensitivity = 9; settings.CameraDevice = null; settings.Clamp();
            Assert.AreEqual(1f, settings.RenderScale); Assert.AreEqual(60, settings.FrameCap); Assert.AreEqual(1.4f, settings.LaneSensitivity, .001f); Assert.AreEqual("", settings.CameraDevice);
        }
        [Test] public void LostPoseRecalibratesAfterTimeout()
        {
            var classifier = new MotionClassifier(new MotionTuning()); classifier.SetStyle(PlayStyle.FullBody);
            for (int i = 0; i < 30; i++) classifier.Update(Frame(new Body(), i / 30.0));
            Assert.AreEqual(PoseStatus.Tracking, classifier.Status);
            classifier.Update(new PoseFrame { HasPose = false, Time = 2 }); Assert.AreEqual(PoseStatus.Tracking, classifier.Status, "Grace period");
            classifier.Update(new PoseFrame { HasPose = false, Time = 2.6 }); Assert.AreEqual(PoseStatus.Searching, classifier.Status);
            classifier.Update(new PoseFrame { HasPose = false, Time = 6 });
            classifier.Update(Frame(new Body(), 6.1)); Assert.AreEqual(PoseStatus.Calibrating, classifier.Status);
        }
        // Camera stand-in for PoseTracker: one scripted frame per Tick time.
        private sealed class FakeEstimator : IPoseEstimator
        {
            public Func<double, PoseFrame> Script;
            private double now, taken = -1;
            public PoseStatus Status => PoseStatus.Tracking;
            public string Message => "";
            public UnityEngine.Texture CameraTexture => null;
            public string Diagnostics => "";
            public void Start() { }
            public void Stop() { }
            public void Dispose() { }
            public void Tick(double time) { now = time; }
            public bool TryTakeFrame(PoseFrame target) { if (now == taken) return false; taken = now; target.CopyFrom(Script(now)); return true; }
        }
        [Test] public void CalibrationIsKeptForAMinuteUnlessTrackingWasLost()
        {
            Assert.IsTrue(CalibrationReuse.Keep(10, 5, false), "Just calibrated: the run skips its loading calibration");
            Assert.IsTrue(CalibrationReuse.Keep(65, 5, false));
            Assert.IsFalse(CalibrationReuse.Keep(65.1, 5, false), "More than 60 s ago");
            Assert.IsFalse(CalibrationReuse.Keep(10, 5, true), "Tracking was lost since");
            Assert.IsFalse(CalibrationReuse.Keep(10, -1, false), "Never calibrated");
        }
        [Test] public void TrackerSkipsRecalibrationRightAfterCalibratingAndNotAfterLosingThePlayer()
        {
            var previous = PoseEstimatorRegistry.Factory; bool present = true;
            var camera = new FakeEstimator { Script = t => present ? Frame(new Body(), t) : new PoseFrame { HasPose = false, Time = t } };
            PoseEstimatorRegistry.Factory = _ => camera;
            var tracker = new PoseTracker(new PoseGameInput(), new PoseCameraOptions(), new MotionTuning(), () => 0, () => false);
            try
            {
                tracker.SetStyle(PlayStyle.FullBody);
                double t = 0; void Run(double seconds) { for (double end = t + seconds; t < end; t += 1 / 30.0) tracker.Tick(t, false); }
                Assert.IsFalse(tracker.CalibrationFresh(t), "Not calibrated yet");
                Run(1.5); Assert.AreEqual(PoseStatus.Tracking, tracker.Status);
                Assert.IsTrue(tracker.CalibrationFresh(t), "Just calibrated: PrepareRun keeps it");
                Assert.IsFalse(tracker.CalibrationFresh(tracker.CalibratedAt + 61), "A minute later it calibrates again");
                present = false; Run(1); present = true; Run(1);
                Assert.AreEqual(PoseStatus.Tracking, tracker.Status); Assert.IsFalse(tracker.CalibrationFresh(t), "Tracking was lost since the calibration");
                tracker.Recalibrate(); Assert.IsFalse(tracker.CalibrationFresh(t));
                Run(1.5); Assert.IsTrue(tracker.CalibrationFresh(t), "A new calibration is fresh again");
                tracker.SetStyle(PlayStyle.UpperBody); Assert.IsFalse(tracker.CalibrationFresh(t), "Another play style needs its own baseline");
            }
            finally { tracker.Dispose(); PoseEstimatorRegistry.Factory = previous; }
        }
        [Test] public void PoseCommandsStepTowardTheZoneAndUseAbsoluteLeanInTheCart()
        {
            var output = new PoseGameInput(); int lane = 0; bool cart = false;
            var source = new PoseCommandSource(output, () => lane, () => cart); source.Reset(0);
            source.Feed(new MotionReading { Lane = 1 }, 0);
            Assert.IsTrue(output.TryRead(out var command)); Assert.AreEqual(MotionCommand.Right, command); lane = 1;
            source.Feed(new MotionReading { Lane = -1 }, .1);
            Assert.IsTrue(output.TryRead(out command)); Assert.AreEqual(MotionCommand.Left, command);
            Assert.IsTrue(output.TryRead(out command)); Assert.AreEqual(MotionCommand.Left, command); lane = -1;
            cart = true; source.Feed(new MotionReading { Lane = 0 }, .2);
            Assert.IsTrue(output.TryRead(out command)); Assert.AreEqual(MotionCommand.Center, command);
            source.Feed(new MotionReading { Lane = 0, Crouch = true }, .3); source.Feed(new MotionReading { Lane = 0, Crouch = true }, .35);
            Assert.IsTrue(output.TryRead(out command)); Assert.AreEqual(MotionCommand.Crouch, command); Assert.IsFalse(output.TryRead(out _));
        }
        [Test] public void BufferedJumpRunsOnLandingOnlyWhenEnabled()
        {
            var runner = new RunnerController(new RunConfig { InputBufferSeconds = .2f });
            runner.Command(MotionCommand.Jump); for (int i = 0; i < 13; i++) runner.Tick(.05f, 0);
            Assert.Greater(runner.Y, 0f); runner.Command(MotionCommand.Jump);
            for (int i = 0; i < 4 && runner.Y > 0; i++) runner.Tick(.05f, 0);
            for (int i = 0; i < 2; i++) runner.Tick(.05f, 0);
            Assert.Greater(runner.Y, .3f, "The jump pressed just before landing must start on landing");
        }
        [Test] public void IntervalRouteKeepsAuthoredFirstLapThenAlternatesRestAndSprint()
        {
            var config = new RunConfig { ExtendedRoutes = false, IntervalMode = true };
            var plan = new RoutePlan(config, 5);
            for (int i = 0; i < RoutePlan.AuthoredLap.Length; i++) Assert.AreEqual(RoutePlan.AuthoredLap[i], plan.TypeAt(i));
            int seq = RoutePlan.AuthoredLap.Length; Assert.AreEqual(0, plan.TypeAt(seq));
            int rest = 0; while (plan.TypeAt(seq + 1 + rest) == 1 || plan.TypeAt(seq + 1 + rest) == 2) rest++;
            Assert.AreEqual((int)Math.Round(config.RestSeconds * config.RestSpeed / RouteSurface.Length), rest);
            for (int i = 0; i < 80; i++)
            {
                int a = plan.TypeAt(i), b = plan.TypeAt(i + 1);
                Assert.AreEqual(RouteSurface.EntryHeight(a) + RouteSurface.Rise(a), RouteSurface.EntryHeight(b), .001f, "Height break between " + a + " and " + b);
                int same = 0; for (int j = i; j < i + 6; j++) if (plan.TypeAt(j) == a) same++;
                Assert.LessOrEqual(same, 3, "More copies of type " + a + " than the chunk pool keeps warm");
            }
            Assert.AreEqual(1, plan.CycleAt(seq));
        }
        [Test] public void IntervalSpeedRestsInTheCartAndRisesEachSprint()
        {
            var config = new RunConfig { ExtendedRoutes = false, IntervalMode = true };
            try
            {
                RouteSurface.UsePlan(new RoutePlan(config, 1));
                var run = new RunSession(config);
                run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
                Assert.AreEqual(config.SprintSpeed, run.TargetSpeed, .001f);
                run.Runner.SetStartZ(30); Assert.IsTrue(run.Resting); Assert.AreEqual(config.RestSpeed, run.TargetSpeed, .001f);
                run.Runner.SetStartZ(RoutePlan.AuthoredLap.Length * RouteSurface.Length + 1); Assert.AreEqual(config.SprintSpeed + config.SprintSpeedStep, run.TargetSpeed, .001f);
            }
            finally { RouteSurface.UsePlan(null); }
        }
        [Test] public void ItemsDoubleCoinsShieldOneHitAndExpire()
        {
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            Assert.AreEqual("코인 2배", ItemCatalog.Get(ItemKind.DoubleScore).Name);
            run.CollectCoin(); Assert.AreEqual(1, run.Score.Coins);
            // Picked up in the same frame as the item, before the next tick, already doubles.
            run.Items.Activate(ItemKind.DoubleScore); run.CollectCoin(); Assert.AreEqual(3, run.Score.Coins);
            run.Tick(.01f); run.Score.CollectCoin(); Assert.AreEqual(5, run.Score.Coins);
            run.Tick(ItemCatalog.Get(ItemKind.DoubleScore).Seconds); run.CollectCoin(); Assert.AreEqual(6, run.Score.Coins);
            run.Items.Activate(ItemKind.Shield);
            Assert.IsFalse(run.Hit(true)); Assert.AreEqual(GameFlowState.Running, run.Flow.State); Assert.AreEqual(1, run.ShieldSaves);
            Assert.IsTrue(run.Hit(true)); Assert.AreEqual(GameFlowState.Caught, run.Flow.State);
            var items = new PowerUpState(); items.Activate(ItemKind.Magnet); items.Tick(100); Assert.IsFalse(items.IsActive(ItemKind.Magnet));
        }
        [Test] public void ShieldBlocksOneSlipButNotABrokenRail()
        {
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            run.Items.Activate(ItemKind.Shield);
            Assert.IsFalse(run.Slip(true)); Assert.AreEqual(1, run.ShieldSaves); Assert.IsFalse(run.Items.IsActive(ItemKind.Shield));
            Assert.IsFalse(run.Runner.Slowing); Assert.AreEqual(0, run.BananaHits); Assert.AreEqual(0, run.Chase.Pressure);
            // Without the shield the next two peels count, and the second starts the pursuit as before.
            Assert.IsTrue(run.Slip(true)); Assert.IsTrue(run.Runner.Slowing); Assert.AreEqual(1, run.BananaHits);
            Assert.IsTrue(run.Slip(true)); Assert.IsTrue(run.BananaPursuit);
            run.Reset(); Assert.AreEqual(GameFlowState.Running, run.Flow.State);
            run.Items.Activate(ItemKind.Shield);
            run.Slip(true); run.Slip(true); Assert.AreEqual(1, run.BananaHits); Assert.IsFalse(run.BananaPursuit, "The shielded first peel must not count towards the pursuit.");
            run.Items.Activate(ItemKind.Shield);
            Assert.IsTrue(run.Fail(CaptureReason.BrokenRail)); Assert.AreEqual(GameFlowState.Caught, run.Flow.State); Assert.AreEqual(CaptureReason.BrokenRail, run.Reason);
            Assert.IsTrue(run.Items.IsActive(ItemKind.Shield), "A broken rail ignores the shield and leaves it unused.");
        }
        [Test] public void WalletUnlocksItemsAndDeveloperModeOpensEverything()
        {
            var wallet = new PlayerWallet(); Assert.IsEmpty(wallet.Available());
            wallet.Deposit(40); Assert.IsTrue(wallet.TryUnlock(ItemKind.Magnet)); Assert.AreEqual(10, wallet.Coins);
            Assert.IsFalse(wallet.TryUnlock(ItemKind.Shield)); CollectionAssert.AreEqual(new[] { ItemKind.Magnet }, wallet.Available());
            wallet.UnlockAll = true; Assert.AreEqual(ItemCatalog.All.Length, wallet.Available().Count);
        }
        [Test] public void LeaderboardRanksByScoreOverallAndPerDay()
        {
            var today = DateTime.Now; var yesterday = today.AddDays(-1);
            var board = new Leaderboard();
            const PlayStyle full = PlayStyle.FullBody; const int mode = (int)full;
            var old = new RankingEntry { Name = "Old", Distance = 300, Score = 1200, ScoreVersion = RunScore.Version, Mode = mode, Ticks = yesterday.Ticks };
            var a = new RankingEntry { Name = "A", Distance = 300, Score = 900, ScoreVersion = RunScore.Version, Mode = mode, Ticks = today.Ticks };
            var b = new RankingEntry { Name = "B", Distance = 500, Score = 500, ScoreVersion = RunScore.Version, Mode = mode, Ticks = today.Ticks + 1 };
            var tie = new RankingEntry { Name = "Tie", Distance = 900, Score = 900, ScoreVersion = RunScore.Version, Mode = mode, Ticks = today.Ticks + 2 };
            Assert.AreEqual(1, board.Add(old)); Assert.AreEqual(2, board.Add(a)); Assert.AreEqual(3, board.Add(b)); Assert.AreEqual(3, board.Add(tie));
            Assert.AreEqual(2, board.RankOf(a, false, full), "Score decides the rank, not distance; a tie goes to the earlier run");
            Assert.AreEqual(4, board.RankOf(b, false, full));
            Assert.AreEqual(1, board.RankOf(a, true, full), "Yesterday's run does not count for today");
            CollectionAssert.AreEqual(new[] { "A", "Tie", "B" }, board.View(true, today, full).ConvertAll(e => e.Name));
            board.ClearDay(Leaderboard.DayKey(today));
            CollectionAssert.AreEqual(new[] { "Old" }, board.View(false, today, full).ConvertAll(e => e.Name));
        }
        [Test] public void OlderScoreVersionsAreKeptButNotRanked()
        {
            var today = DateTime.Now;
            var board = new Leaderboard();
            const PlayStyle keys = PlayStyle.Keyboard;
            var legacy = new RankingEntry { Name = "Legacy", Distance = 5000, Score = 50000, Style = "키보드", Ticks = today.Ticks };
            var current = new RankingEntry { Name = "Now", Distance = 100, Score = 120, ScoreVersion = RunScore.Version, Mode = (int)keys, Ticks = today.Ticks + 1 };
            board.Load(new[] { legacy, current });
            Assert.AreEqual(1, legacy.ScoreVersion, "Records saved before the version field load as version 1");
            Assert.AreEqual(2, board.All.Count, "Older records are not deleted");
            CollectionAssert.AreEqual(new[] { "Now" }, board.View(false, today, keys).ConvertAll(e => e.Name));
            CollectionAssert.AreEqual(new[] { "Now" }, board.View(true, today, keys).ConvertAll(e => e.Name));
            Assert.AreEqual(0, board.RankOf(legacy, false, keys)); Assert.AreEqual(1, board.RankOf(current, false, keys));
            Assert.AreEqual(0, board.Add(new RankingEntry { Name = "Old", Score = 99999, ScoreVersion = 1, Mode = (int)keys, Ticks = today.Ticks + 2 }));
            Assert.AreEqual(1, board.Add(new RankingEntry { Name = "Best", Score = 121, ScoreVersion = RunScore.Version, Mode = (int)keys, Ticks = today.Ticks + 3 }));
        }
        [Test] public void BestScoreIsPerPlayStyleAndIgnoresOlderVersions()
        {
            var board = new Leaderboard(); long t = DateTime.Now.Ticks;
            board.Add(new RankingEntry { Name = "A", Score = 900, ScoreVersion = RunScore.Version, Style = "전신", Ticks = t });
            board.Add(new RankingEntry { Name = "B", Score = 1500, ScoreVersion = RunScore.Version, Style = "전신", Ticks = t + 1 });
            board.Add(new RankingEntry { Name = "A", Score = 700, ScoreVersion = RunScore.Version, Style = "키보드", Ticks = t + 2 });
            board.Add(new RankingEntry { Name = "A", Score = 99999, ScoreVersion = 1, Style = "전신", Ticks = t + 3 });
            Assert.AreEqual(1500, board.BestScore("전신")); Assert.AreEqual(900, board.BestScore("전신", "A"));
            Assert.AreEqual(700, board.BestScore("키보드")); Assert.AreEqual(0, board.BestScore("상체"));
        }
        [Test] public void PlayerStorePersistsProfileWalletRecordsAndStats()
        {
            string root = Path.Combine(Path.GetTempPath(), "muhanok-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new PlayerProgressStore(root);
                Assert.IsFalse(store.Profile.HasName);
                store.SaveProfile(new PlayerProfile { Name = "탈옥왕", Weight = "60", Style = PlayStyle.UpperBody });
                long now = DateTime.Now.Ticks;
                Assert.AreEqual(1, store.Record(new RankingEntry { Name = "탈옥왕", Distance = 120, Score = 120, ScoreVersion = RunScore.Version, Mode = (int)PlayStyle.UpperBody, Kcal = 12.5, Ticks = now }, 12.5));
                Assert.AreEqual(1, store.Record(new RankingEntry { Name = "탈옥왕", Distance = 340, Score = 340, ScoreVersion = RunScore.Version, Mode = (int)PlayStyle.UpperBody, Kcal = 30, Ticks = now + 1 }, 30));
                store.DepositCoins(55); Assert.IsTrue(store.Unlock(ItemKind.Magnet));
                var reopened = new PlayerProgressStore(root);
                Assert.AreEqual("탈옥왕", reopened.Profile.Name); Assert.AreEqual(PlayStyle.UpperBody, reopened.Profile.Style);
                CollectionAssert.AreEqual(new[] { 340f, 120f }, new List<RankingEntry>(reopened.Board.All).ConvertAll(e => e.Distance));
                Assert.AreEqual(2, reopened.Stats.Runs); Assert.AreEqual(460, reopened.Stats.TotalDistance, .01f);
                Assert.AreEqual(42.5, reopened.Stats.TotalKcal, .01); Assert.GreaterOrEqual(reopened.Stats.BestDistance, 340);
                Assert.AreEqual(25, reopened.Wallet.Coins); Assert.IsTrue(reopened.Wallet.IsUnlocked(ItemKind.Magnet));
                reopened.ClearToday(); Assert.AreEqual(0, new PlayerProgressStore(root).Board.All.Count);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        [Test] public void AbandoningARunReturnsToTitleWithoutAResult()
        {
            var flow = new GameFlowController();
            flow.Transition(GameFlowState.TitleIdle); flow.Transition(GameFlowState.LoadingIntro); flow.Transition(GameFlowState.Running);
            Assert.IsTrue(flow.Transition(GameFlowState.Retry)); Assert.IsTrue(flow.Transition(GameFlowState.TitleIdle));
        }
    }
}
