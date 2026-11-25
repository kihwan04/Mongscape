using System;

namespace Muhanok.Domain
{
    // The calibrated standing pose the classifier measures against, in pose-frame units: X and Y as in PoseFrame
    // (0..1 of the picture width and height), the body scale in picture heights (torso for the full body, shoulder width
    // for the upper body) and the knee drop (knee below hip at rest, picture heights; NaN when the knees were not seen).
    public readonly struct PoseBaseline
    {
        public readonly bool Valid; public readonly PlayStyle Style;
        public readonly float CentreX, ShoulderY, HipY, Scale, KneeDrop, NoseY, Aspect;
        public PoseBaseline(PlayStyle style, float centreX, float shoulderY, float hipY, float scale, float kneeDrop, float noseY, float aspect)
        {
            Style = style; CentreX = centreX; ShoulderY = shoulderY; HipY = hipY; Scale = scale; KneeDrop = kneeDrop; NoseY = noseY;
            Aspect = aspect > 0 && !float.IsNaN(aspect) ? aspect : 4f / 3f;
            Valid = scale > 0 && !float.IsNaN(scale) && !float.IsNaN(shoulderY) && !float.IsNaN(centreX);
        }
    }

    // The line a lesson's movement has to cross, drawn over both stick figures of the follow-along comparison
    // (exercise plan 10-3 B). A horizontal line sits at At (picture height share); a vertical one is a pair At / Other
    // (picture width shares, either side of the body).
    public readonly struct TargetLine
    {
        public readonly bool Shown, Vertical; public readonly float At, Other; public readonly string Label;
        public TargetLine(bool vertical, float at, float other, string label) { Shown = true; Vertical = vertical; At = at; Other = other; Label = label; }
    }

    // Joint comparison for a lesson missed three times: a reference stick figure doing the lesson's movement next to the
    // player's own joints, both with the line the classifier really uses (the same MotionTuning thresholds and baseline).
    public static class JointDemo
    {
        public const float LoopSeconds = 1.6f;
        public const string Title = "내 관절 비교", ReferenceLabel = "시범", MineLabel = "나";
        // Lessons with a body line to compare against.
        public static bool Compares(TutorialStep step) => step == TutorialStep.Jump || step == TutorialStep.Crouch || step == TutorialStep.HighKnee || step == TutorialStep.Lane;

        // The reference figure standing still, measured like a calibration (square picture).
        public static PoseBaseline ReferenceBaseline(PlayStyle style)
        {
            var f = Standing(null);
            float sy = Mid(f, PoseJoint.LeftShoulder, PoseJoint.RightShoulder, false), hy = Mid(f, PoseJoint.LeftHip, PoseJoint.RightHip, false);
            float scale = style == PlayStyle.UpperBody ? f.Points[PoseJoint.RightShoulder].X - f.Points[PoseJoint.LeftShoulder].X : hy - sy;
            float centre = style == PlayStyle.UpperBody ? Mid(f, PoseJoint.LeftShoulder, PoseJoint.RightShoulder, true) : Mid(f, PoseJoint.LeftHip, PoseJoint.RightHip, true);
            return new PoseBaseline(style, centre, sy, hy, scale, f.Points[PoseJoint.LeftKnee].Y - f.Points[PoseJoint.LeftHip].Y, f.Points[PoseJoint.Nose].Y, f.Aspect);
        }

        // The classifier's line for the lesson from a baseline; default (not shown) for lessons without one.
        public static TargetLine Target(TutorialStep step, PoseBaseline b, MotionTuning tuning)
        {
            if (!b.Valid) return default;
            tuning ??= new MotionTuning();
            // Sideways thresholds are in body units of picture height; X is a share of the picture width.
            float across = b.Scale / b.Aspect;
            if (b.Style == PlayStyle.UpperBody)
            {
                float nose = float.IsNaN(b.NoseY) ? b.ShoulderY - b.Scale : b.NoseY;
                switch (step)
                {
                    case TutorialStep.Jump: return new TargetLine(false, nose - tuning.UpperHandsAboveNose * b.Scale, 0, "손목을 선 위로");
                    case TutorialStep.Crouch: return new TargetLine(false, b.ShoulderY + tuning.UpperCrouchDrop * b.Scale, 0, "어깨를 선 아래로");
                    case TutorialStep.HighKnee: return new TargetLine(false, nose, 0, "손을 올렸다 선 아래로");
                    case TutorialStep.Lane: return new TargetLine(true, b.CentreX - tuning.UpperLaneEnter * across, b.CentreX + tuning.UpperLaneEnter * across, "어깨를 선 밖으로");
                    default: return default;
                }
            }
            switch (step)
            {
                case TutorialStep.Jump: return new TargetLine(false, b.HipY - tuning.JumpRise * b.Scale, 0, "엉덩이를 선 위로");
                case TutorialStep.Crouch: return new TargetLine(false, b.ShoulderY + tuning.CrouchDrop * b.Scale, 0, "어깨를 선 아래로");
                case TutorialStep.HighKnee:
                    return float.IsNaN(b.KneeDrop) ? default : new TargetLine(false, b.HipY + b.KneeDrop - tuning.HighKneeLift * b.Scale, 0, "무릎을 선 위로");
                case TutorialStep.Lane: return new TargetLine(true, b.CentreX - tuning.LaneEnter * across, b.CentreX + tuning.LaneEnter * across, "엉덩이를 선 밖으로");
                default: return default;
            }
        }

        // Whether a frame is past the line (the comparison lights the player's line green then).
        public static bool Crossed(TutorialStep step, PoseBaseline b, MotionTuning tuning, PoseFrame frame)
        {
            var line = Target(step, b, tuning);
            if (!line.Shown || frame == null || !frame.HasPose) return false;
            bool upper = b.Style == PlayStyle.UpperBody;
            switch (step)
            {
                case TutorialStep.Jump: return upper ? Math.Max(Y(frame, PoseJoint.LeftWrist), Y(frame, PoseJoint.RightWrist)) < line.At : Mid(frame, PoseJoint.LeftHip, PoseJoint.RightHip, false) < line.At;
                case TutorialStep.Crouch: return Mid(frame, PoseJoint.LeftShoulder, PoseJoint.RightShoulder, false) > line.At;
                case TutorialStep.HighKnee: return upper ? Math.Max(Y(frame, PoseJoint.LeftWrist), Y(frame, PoseJoint.RightWrist)) > line.At : Math.Min(Y(frame, PoseJoint.LeftKnee), Y(frame, PoseJoint.RightKnee)) < line.At;
                case TutorialStep.Lane:
                    float x = upper ? Mid(frame, PoseJoint.LeftShoulder, PoseJoint.RightShoulder, true) : Mid(frame, PoseJoint.LeftHip, PoseJoint.RightHip, true);
                    return x < line.At || x > line.Other;
                default: return false;
            }
        }

        // The reference figure at time t (seconds, looping): the lesson's movement done clearly past the line.
        public static PoseFrame Reference(TutorialStep step, PlayStyle style, double t, PoseFrame into = null)
        {
            var f = Standing(into);
            float phase = (float)(((t % LoopSeconds) + LoopSeconds) % LoopSeconds / LoopSeconds);
            float wave = (float)Math.Sin(phase * Math.PI); // 0 -> 1 -> 0 over the loop
            float half = phase < .5f ? phase * 2 : phase * 2 - 1; // each half of the loop, 0..1
            int side = phase < .5f ? PoseJoint.LeftKnee : PoseJoint.RightKnee;
            bool upper = style == PlayStyle.UpperBody;
            switch (step)
            {
                case TutorialStep.Jump: if (upper) Raise(f, wave); else Shift(f, 0, -.09f * wave, false); break;
                case TutorialStep.Crouch: if (upper) Shift(f, 0, .13f * wave, true); else Squat(f, wave); break;
                case TutorialStep.HighKnee: if (upper) PullDown(f, wave); else Knee(f, side, (float)Math.Sin(half * Math.PI)); break;
                case TutorialStep.Lane:
                    float x = (float)Math.Sin(phase * 2 * Math.PI) * .2f;
                    if (upper) Lean(f, x); else Shift(f, x, 0, false);
                    break;
                case TutorialStep.Cadence:
                    if (upper) Swing(f, (float)Math.Sin(phase * 2 * Math.PI)); else Knee(f, side, .45f * (float)Math.Sin(half * Math.PI));
                    break;
            }
            return f;
        }

        // joint, x, y of the standing reference (square picture).
        private static readonly float[,] Rest =
        {
            { PoseJoint.Nose, .5f, .17f }, { PoseJoint.LeftShoulder, .43f, .28f }, { PoseJoint.RightShoulder, .57f, .28f },
            { PoseJoint.LeftElbow, .41f, .4f }, { PoseJoint.RightElbow, .59f, .4f }, { PoseJoint.LeftWrist, .4f, .51f }, { PoseJoint.RightWrist, .6f, .51f },
            { PoseJoint.LeftHip, .46f, .53f }, { PoseJoint.RightHip, .54f, .53f }, { PoseJoint.LeftKnee, .46f, .71f }, { PoseJoint.RightKnee, .54f, .71f },
            { PoseJoint.LeftAnkle, .46f, .89f }, { PoseJoint.RightAnkle, .54f, .89f },
        };
        private static PoseFrame Standing(PoseFrame into)
        {
            var f = into ?? new PoseFrame();
            Array.Clear(f.Points, 0, PoseFrame.Count);
            for (int i = 0; i < Rest.GetLength(0); i++) f.Points[(int)Rest[i, 0]] = new PoseLandmark { X = Rest[i, 1], Y = Rest[i, 2], Visibility = 1 };
            f.HasPose = true; f.Aspect = 1; return f;
        }
        private static float Y(PoseFrame f, int joint) => f.Points[joint].Y;
        private static float Mid(PoseFrame f, int a, int b, bool x) => x ? (f.Points[a].X + f.Points[b].X) * .5f : (f.Points[a].Y + f.Points[b].Y) * .5f;
        private static void Move(PoseFrame f, int joint, float dx, float dy) { f.Points[joint].X += dx; f.Points[joint].Y += dy; }
        private static void Shift(PoseFrame f, float dx, float dy, bool aboveHips)
        {
            for (int i = 0; i < PoseFrame.Count; i++)
            {
                if (f.Points[i].Visibility <= 0 || aboveHips && i >= PoseJoint.LeftHip) continue;
                Move(f, i, dx, dy);
            }
        }
        // Half squat: hips and everything above drop, the knees open outward, the arms come forward.
        private static void Squat(PoseFrame f, float w)
        {
            for (int i = 0; i <= PoseJoint.RightHip; i++) if (f.Points[i].Visibility > 0) Move(f, i, 0, .14f * w);
            Move(f, PoseJoint.LeftKnee, -.05f * w, .02f * w); Move(f, PoseJoint.RightKnee, .05f * w, .02f * w);
            Move(f, PoseJoint.LeftElbow, 0, -.06f * w); Move(f, PoseJoint.RightElbow, 0, -.06f * w);
            Move(f, PoseJoint.LeftWrist, .04f * w, -.12f * w); Move(f, PoseJoint.RightWrist, -.04f * w, -.12f * w);
        }
        // One knee up to hip height at w 1, its ankle tucked under it.
        private static void Knee(PoseFrame f, int knee, float w)
        {
            if (w <= 0) return;
            int ankle = knee == PoseJoint.LeftKnee ? PoseJoint.LeftAnkle : PoseJoint.RightAnkle;
            float lift = (f.Points[knee].Y - f.Points[PoseJoint.LeftHip].Y) * w;
            Move(f, knee, 0, -lift); Move(f, ankle, 0, -lift * .9f);
        }
        private static void Raise(PoseFrame f, float w)
        {
            Move(f, PoseJoint.LeftElbow, -.02f * w, -.26f * w); Move(f, PoseJoint.RightElbow, .02f * w, -.26f * w);
            Move(f, PoseJoint.LeftWrist, -.02f * w, -.48f * w); Move(f, PoseJoint.RightWrist, .02f * w, -.48f * w);
        }
        // Hands over the head, then pulled down below the nose (the upper body's stair move).
        private static void PullDown(PoseFrame f, float w)
        {
            Raise(f, 1 - w);
            Move(f, PoseJoint.LeftWrist, -.03f * w, -.2f * w); Move(f, PoseJoint.RightWrist, .03f * w, -.2f * w);
        }
        // The upper body tilts sideways over the hips.
        private static void Lean(PoseFrame f, float x)
        {
            for (int i = 0; i < PoseJoint.LeftHip; i++) if (f.Points[i].Visibility > 0) Move(f, i, x * (.53f - f.Points[i].Y) / .25f, 0);
        }
        private static void Swing(PoseFrame f, float s)
        {
            float l = Math.Max(0, s), r = Math.Max(0, -s);
            Move(f, PoseJoint.LeftElbow, 0, -.08f * l); Move(f, PoseJoint.RightElbow, 0, -.08f * r);
            Move(f, PoseJoint.LeftWrist, 0, -.2f * l); Move(f, PoseJoint.RightWrist, 0, -.2f * r);
        }
    }
}
