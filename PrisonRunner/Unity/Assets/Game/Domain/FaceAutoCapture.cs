using System;
namespace Muhanok.Domain
{
    // Face login and sign-up press their capture button by themselves once a face has stayed inside the on-screen oval for
    // HoldSeconds. The face is read from the pose landmarks already tracked (nose and both eyes), so no extra model runs.
    // One attempt per stay: the next one needs the face to leave the oval for a moment, or RetrySeconds after the last
    // attempt finished, so a failed login does not hammer the server.
    public sealed class FaceAutoCapture
    {
        public const double HoldSeconds = 1.2, LeaveSeconds = .5, RetrySeconds = 4;
        // The guide oval over the video (see IdentityScreen.DrawFaceGuide): 60% of the video height, 3:4, centred.
        public const float OvalHeight = .6f, OvalWidthToHeight = .75f;
        // Eye-to-eye distance in video heights below which the face is too far away to capture (about 1.5 m on a typical webcam).
        public const float MinEyeSpan = .05f;
        private double since = -1, outSince = -1, finishedAt = -10;
        private bool armed = true;
        public double Progress { get; private set; }

        public static bool FaceInOval(PoseFrame f, double now)
        {
            if (f == null || !f.HasPose || now < f.Time || now - f.Time > .3) return false;
            var nose = f.Points[PoseJoint.Nose]; var l = f.Points[2]; var r = f.Points[5];
            if (nose.Visibility < .5f || l.Visibility < .5f || r.Visibility < .5f) return false;
            float aspect = Math.Max(.5f, f.Aspect);
            float dx = (l.X - r.X) * aspect, dy = l.Y - r.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < MinEyeSpan) return false;
            return Inside(nose, aspect) && Inside(l, aspect) && Inside(r, aspect);
        }
        // x and y normalised to the video; the oval's half axes in those units.
        private static bool Inside(PoseLandmark p, float aspect)
        {
            float b = OvalHeight * .5f, a = OvalHeight * OvalWidthToHeight * .5f / aspect;
            float u = (p.X - .5f) / a, v = (p.Y - .5f) / b;
            return u * u + v * v <= 1f;
        }

        // ready: the button could be pressed now (no request in flight). Returns true on the frame the capture should start.
        public bool Tick(bool inside, bool ready, double now)
        {
            // Any time the button is unavailable (a request in flight) pushes the retry time back.
            if (!ready) finishedAt = now;
            if (!inside)
            {
                since = -1; Progress = 0;
                if (outSince < 0) outSince = now;
                if (now - outSince >= LeaveSeconds) armed = true;
                return false;
            }
            outSince = -1;
            if (!armed && ready && now - finishedAt >= RetrySeconds) armed = true;
            if (!armed || !ready) { since = -1; Progress = 0; return false; }
            if (since < 0) since = now;
            Progress = Math.Min(1, (now - since) / HoldSeconds);
            if (Progress < 1) return false;
            armed = false; finishedAt = now; since = -1; Progress = 0;
            return true;
        }
        public void Reset() { since = outSince = -1; finishedAt = -10; armed = true; Progress = 0; }
    }
}
