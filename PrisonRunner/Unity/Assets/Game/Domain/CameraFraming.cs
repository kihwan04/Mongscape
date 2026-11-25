namespace Muhanok.Domain
{
    // What is wrong with how the player stands in the camera view, most important first.
    public enum FramingIssue { Ok, NoBody, StepBack, HeadCut, StepCloser, MoveLeft, MoveRight }

    // Camera setup check before a run: the full body must show from the head to the ankles, about 2-3 m from a camera
    // at waist height; the upper body from the head to the waist. Reads one raw pose frame (mirror view, 0..1).
    public static class CameraFraming
    {
        // A joint counts when it is this confident and inside the picture by this margin.
        public const float Visible = .5f, Edge = .02f;
        // Head to ankle shorter than this share of the picture height: too far away.
        public const float MinBodyHeight = .45f;
        // The hips (upper body: shoulders) outside this middle band: off to one side.
        public const float SideBand = .25f;

        public static FramingIssue Check(PoseFrame frame, PlayStyle style)
        {
            if (frame == null || !frame.HasPose || style == PlayStyle.Keyboard) return style == PlayStyle.Keyboard ? FramingIssue.Ok : FramingIssue.NoBody;
            var p = frame.Points;
            if (!In(p[PoseJoint.LeftShoulder]) || !In(p[PoseJoint.RightShoulder])) return FramingIssue.NoBody;
            bool head = In(p[PoseJoint.Nose]);
            float centre;
            if (style == PlayStyle.FullBody)
            {
                bool hips = In(p[PoseJoint.LeftHip]) && In(p[PoseJoint.RightHip]);
                bool feet = hips && In(p[PoseJoint.LeftKnee]) && In(p[PoseJoint.RightKnee]) && In(p[PoseJoint.LeftAnkle]) && In(p[PoseJoint.RightAnkle]);
                // Feet out of the picture: the player stands too close (or the camera points too high); stepping back fixes both.
                if (!feet) return FramingIssue.StepBack;
                if (!head) return FramingIssue.HeadCut;
                float ankle = (p[PoseJoint.LeftAnkle].Y + p[PoseJoint.RightAnkle].Y) * .5f;
                if (ankle - p[PoseJoint.Nose].Y < MinBodyHeight) return FramingIssue.StepCloser;
                centre = (p[PoseJoint.LeftHip].X + p[PoseJoint.RightHip].X) * .5f;
            }
            else
            {
                if (!head) return FramingIssue.HeadCut;
                centre = (p[PoseJoint.LeftShoulder].X + p[PoseJoint.RightShoulder].X) * .5f;
            }
            // Mirror view: X grows to the player's right, so a body on the left of the picture moves right.
            if (centre < SideBand) return FramingIssue.MoveRight;
            if (centre > 1 - SideBand) return FramingIssue.MoveLeft;
            return FramingIssue.Ok;
        }

        public static string Hint(FramingIssue issue, PlayStyle style)
        {
            switch (issue)
            {
                case FramingIssue.NoBody: return "카메라 앞 2~3m에 서 주세요";
                case FramingIssue.StepBack: return "발끝까지 보이게 한 걸음 뒤로 가 주세요";
                case FramingIssue.HeadCut: return style == PlayStyle.FullBody ? "머리까지 보이게 카메라를 허리 높이에 두세요" : "머리까지 보이게 조금 뒤로 앉아 주세요";
                case FramingIssue.StepCloser: return "한 걸음 앞으로 와 주세요";
                case FramingIssue.MoveLeft: return "왼쪽으로 조금 옮겨 주세요";
                case FramingIssue.MoveRight: return "오른쪽으로 조금 옮겨 주세요";
                default: return null;
            }
        }

        private static bool In(PoseLandmark p) => p.Visibility >= Visible && p.X >= Edge && p.X <= 1 - Edge && p.Y >= Edge && p.Y <= 1 - Edge;
    }

    // Keeps the shown hint steady: a new issue replaces the current one only after holding for HoldSeconds.
    public sealed class FramingGuide
    {
        public const double HoldSeconds = .4;
        private FramingIssue candidate;
        private double candidateSince = -1;
        public FramingIssue Issue { get; private set; } = FramingIssue.NoBody;
        public void Reset() { Issue = candidate = FramingIssue.NoBody; candidateSince = -1; }
        public FramingIssue Update(PoseFrame frame, PlayStyle style, double now)
        {
            var issue = CameraFraming.Check(frame, style);
            if (issue == Issue) { candidate = issue; candidateSince = now; return Issue; }
            if (issue != candidate || candidateSince < 0) { candidate = issue; candidateSince = now; }
            if (now - candidateSince >= HoldSeconds) Issue = issue;
            return Issue;
        }
    }
}
