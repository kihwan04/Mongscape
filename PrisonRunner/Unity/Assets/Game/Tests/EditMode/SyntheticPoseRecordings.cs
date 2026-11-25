using System;
using System.Collections.Generic;
using Muhanok.Domain;

namespace Muhanok.Tests
{
    // Builds the synthetic seed recordings in Tools/QA/PoseRecordings/synthetic: a scripted stick body with joint jitter,
    // running bounce and one knee up per step, so the scorecard has something to run on before the team records real people.
    // These are marked "source=synthetic" and their scores say nothing about real recognition accuracy.
    // Regenerate with the test SyntheticSeedRecordingsMatchTheGenerator (set MONGSCAPE_WRITE_POSE_SEEDS=1).
    public static class SyntheticPoseRecordings
    {
        public const double Fps = 30;

        // Offsets in image units (the picture is 1 high); the standing torso is .27 high.
        private sealed class Body
        {
            public float Dx, Dy, Shoulder, Hip, KneeL, KneeR, AnkleL, AnkleR;
        }

        private sealed class Script
        {
            public readonly List<PoseLabel> Labels = new List<PoseLabel>();
            public readonly List<Func<double, Body>> Parts = new List<Func<double, Body>>();
            public readonly List<double> Starts = new List<double>();
            public double End;
            public Script Add(double seconds, Func<double, Body> body, PoseAction? label = null, int count = 1, double labelFrom = 0, double labelTo = -1)
            {
                Starts.Add(End); Parts.Add(body);
                if (label.HasValue) Labels.Add(new PoseLabel { Action = label.Value, Start = End + labelFrom, End = End + (labelTo < 0 ? seconds : labelTo), Count = count });
                End += seconds; return this;
            }
            public Script Context(PoseAction action, double from, double to) { Labels.Add(new PoseLabel { Action = action, Start = from, End = to }); return this; }
            public Body At(double t)
            {
                for (int i = Parts.Count - 1; i >= 0; i--) if (t >= Starts[i]) return Parts[i](t - Starts[i]);
                return new Body();
            }
        }

        public static readonly string[] Names = { "synthetic-paces", "synthetic-heavy-sprint", "synthetic-actions", "synthetic-run-and-jump", "synthetic-stairs" };

        public static void Build(string name, out PoseRecording recording, out List<PoseLabel> labels)
        {
            var script = new Script();
            int seed = 1;
            switch (name)
            {
                case "synthetic-paces":
                    seed = 11;
                    script.Add(2.5, Stand)
                        .Add(10, Pace(90, .028f, .004f), PoseAction.Walk)
                        .Add(10, Pace(130, .045f, .012f), PoseAction.Run)
                        .Add(10, Pace(170, .06f, .02f), PoseAction.Sprint)
                        .Add(5, Stand, PoseAction.Stand);
                    break;
                case "synthetic-heavy-sprint":
                    // A sprinter whose hips bounce past the old jump line (0.15 torso) on every step, with one knee up.
                    seed = 23;
                    script.Add(2.5, Stand).Add(30, Pace(175, .07f, .045f), PoseAction.Sprint).Add(3, Stand, PoseAction.Stand);
                    break;
                case "synthetic-actions":
                    seed = 37;
                    script.Add(2.5, Stand);
                    for (int i = 0; i < 5; i++) script.Add(1.2, Jump, PoseAction.Jump, 1, .1, .8).Add(1.3, Stand);
                    for (int i = 0; i < 4; i++) script.Add(1.4, Crouch, PoseAction.Crouch, 1, .1, 1.2).Add(1.2, Stand);
                    for (int i = 0; i < 3; i++) script.Add(1.6, Side(-1), PoseAction.SideLeft, 1, 0, .8).Add(1.2, Stand).Add(1.6, Side(1), PoseAction.SideRight, 1, 0, .8).Add(1.2, Stand);
                    break;
                case "synthetic-run-and-jump":
                    // Running at about 140 steps a minute with a real jump every 4 s: the higher jump line while running must keep real jumps.
                    seed = 41;
                    script.Add(2.5, Stand);
                    for (int i = 0; i < 6; i++) script.Add(3, Pace(140, .05f, .018f), PoseAction.Run).Add(1.2, Jump, PoseAction.Jump, 1, .1, .8);
                    script.Add(3, Stand);
                    break;
                case "synthetic-stairs":
                    // On a stair chunk: alternating high knees at about 110 a minute, counted as knee steps and as steps.
                    seed = 53;
                    script.Add(2.5, Stand).Add(10, Pace(110, .1f, .012f), PoseAction.Knee, 19, .3, 10).Add(3, Stand);
                    script.Context(PoseAction.Stairs, 2.5, 12.5);
                    break;
                default: throw new ArgumentException("Unknown synthetic recording " + name);
            }
            var random = new Random(seed);
            recording = new PoseRecording();
            recording.Meta["source"] = PoseRecording.SourceSynthetic;
            recording.Meta["style"] = PlayStyle.FullBody.ToString();
            recording.Meta["camera"] = "640x480@30";
            recording.Meta["model"] = "synthetic";
            recording.Meta["person"] = "script";
            recording.Meta["note"] = "합성 녹화: 스크립트로 만든 몸. 실제 인식 정확도가 아님";
            for (int f = 0; f * (1 / Fps) < script.End; f++)
            {
                double t = f / Fps;
                recording.Frames.Add(Frame(script.At(t), t, random));
            }
            labels = script.Labels;
        }

        private static Body Stand(double t) => new Body();

        // Running in place at the given steps a minute: each step lifts one knee (and its ankle) and bounces the whole body up.
        private static Func<double, Body> Pace(double stepsPerMinute, float knee, float bounce) => t =>
        {
            double period = 60 / stepsPerMinute, phase = t / period;
            int step = (int)Math.Floor(phase);
            float lift = (float)Math.Sin(Math.PI * (phase - step));
            // The body rises while the knee comes up, lowest between steps.
            var body = new Body { Dy = -bounce * lift };
            if (step % 2 == 0) { body.KneeL = knee * lift; body.AnkleL = knee * .8f * lift; }
            else { body.KneeR = knee * lift; body.AnkleR = knee * .8f * lift; }
            return body;
        };

        // A dip of 0.2 s, then 0.35 s in the air (hips up about a third of the torso), then landing.
        private static Body Jump(double t)
        {
            if (t < .1 || t >= .85) return new Body();
            if (t < .3) return new Body { Dy = .03f };
            if (t < .65) { float up = (float)Math.Sin(Math.PI * (t - .3) / .35); return new Body { Dy = -.09f * up, AnkleL = .07f * up, AnkleR = .07f * up, KneeL = .02f * up, KneeR = .02f * up }; }
            return new Body { Dy = .02f };
        }

        // Shoulders down well past the crouch line for 1.1 s.
        private static Body Crouch(double t)
        {
            if (t < .1 || t > 1.2) return new Body();
            float d = (float)Math.Min(1, Math.Min((t - .1) / .15, (1.2 - t) / .15));
            return new Body { Shoulder = .13f * d, Hip = .05f * d };
        }

        // A side step into the zone (0.25 s), held, and back.
        private static Func<double, Body> Side(int dir) => t =>
        {
            float d = (float)Math.Max(0, Math.Min(1, Math.Min(t / .25, (1.4 - t) / .25)));
            return new Body { Dx = dir * .15f * d };
        };

        private static PoseFrame Frame(Body b, double time, Random random)
        {
            var frame = new PoseFrame { HasPose = true, Time = time, Aspect = 4f / 3f, ObjectScale = .55f };
            float Noise() { double u1 = 1 - random.NextDouble(), u2 = random.NextDouble(); return (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2) * .003); }
            void Set(int i, float x, float y, float v = .95f) =>
                frame.Points[i] = new PoseLandmark { X = x + b.Dx + Noise(), Y = y + Noise(), Z = 0, Visibility = Math.Min(1f, v + (float)(random.NextDouble() * .04 - .02)) };
            for (int i = 0; i < PoseFrame.Count; i++) Set(i, .5f, .5f, .2f);
            float sy = .35f + b.Dy + b.Shoulder, hy = .62f + b.Dy + b.Hip;
            Set(0, .5f, .25f + b.Dy + b.Shoulder); Set(11, .44f, sy); Set(12, .56f, sy);
            Set(13, .42f, sy + .13f); Set(14, .58f, sy + .13f); Set(15, .41f, sy + .25f); Set(16, .59f, sy + .25f);
            Set(23, .46f, hy); Set(24, .54f, hy);
            Set(25, .46f, .78f + b.Dy + b.Hip * .5f - b.KneeL); Set(26, .54f, .78f + b.Dy + b.Hip * .5f - b.KneeR);
            Set(27, .46f, .93f + b.Dy - b.AnkleL, .9f); Set(28, .54f, .93f + b.Dy - b.AnkleR, .9f);
            return frame;
        }
    }
}
