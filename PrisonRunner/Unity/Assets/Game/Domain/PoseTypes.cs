using System;

namespace Muhanok.Domain
{
    public struct PoseLandmark { public float X, Y, Visibility; }

    // BlazePose's 33 landmarks in the player's mirror view: X grows to the player's right, Y grows downward, both 0..1.
    public sealed class PoseFrame
    {
        public const int Count = 33;
        public readonly PoseLandmark[] Points = new PoseLandmark[Count];
        public float Aspect = 4f / 3f;
        public bool HasPose;
        public double Time;
        // Size of the tracked body region relative to the image height; used to scale the jitter filter.
        public float ObjectScale = .5f;
        public void CopyFrom(PoseFrame other)
        {
            Array.Copy(other.Points, Points, Count); Aspect = other.Aspect; HasPose = other.HasPose; Time = other.Time; ObjectScale = other.ObjectScale;
        }
    }

    public static class PoseJoint
    {
        public const int Nose = 0, LeftShoulder = 11, RightShoulder = 12, LeftElbow = 13, RightElbow = 14, LeftWrist = 15, RightWrist = 16;
        public const int LeftHip = 23, RightHip = 24, LeftKnee = 25, RightKnee = 26, LeftAnkle = 27, RightAnkle = 28;
        public static readonly int[,] Bones =
        {
            { 11, 12 }, { 11, 13 }, { 13, 15 }, { 12, 14 }, { 14, 16 }, { 11, 23 }, { 12, 24 }, { 23, 24 },
            { 23, 25 }, { 25, 27 }, { 24, 26 }, { 26, 28 }, { 0, 11 }, { 0, 12 },
        };
    }

    public enum PoseStatus { Off, Starting, NoCamera, NoModel, Searching, Calibrating, Tracking }

    public static class PoseStatusText
    {
        public static string Korean(PoseStatus status) =>
            status == PoseStatus.Off ? "카메라 꺼짐" : status == PoseStatus.Starting ? "카메라 준비 중" : status == PoseStatus.NoCamera ? "웹캠 없음"
            : status == PoseStatus.NoModel ? "포즈 모듈 없음" : status == PoseStatus.Searching ? "사람을 찾는 중" : status == PoseStatus.Calibrating ? "가만히 서 주세요 (자세 맞추는 중)" : "인식 완료";
    }

    // A run keeps the standing baseline of a calibration that finished at most a minute ago (result-screen 웹캠 보정)
    // when tracking has not been lost since; otherwise loading calibrates again.
    public static class CalibrationReuse
    {
        public const double MaxAgeSeconds = 60;
        public static bool Keep(double now, double calibratedAt, bool lostSince) => calibratedAt >= 0 && !lostSince && now >= calibratedAt && now - calibratedAt <= MaxAgeSeconds;
    }

    [Serializable]
    public sealed class PoseCameraOptions
    {
        public string DeviceName = "";
        public int Width = 640, Height = 480, Fps = 30;
        public bool FullLandmarkModel = true;
        public float DetectionThreshold = .55f;
        // Upper bound on pose inferences per second; frees GPU time on integrated graphics.
        public float MaxInferenceRate = 30f;
        // Landmark passes in flight at once while tracking (1 = one at a time, 2 = overlap the GPU readback wait).
        public int PipelineDepth = 2;
        public bool ConvertToSrgb = false;
    }

    // Thresholds are in body units: torso length (shoulder centre to hip centre) for the full body,
    // shoulder width for the upper body. Both are measured in image-height units, so the camera aspect does not matter.
    [Serializable]
    public sealed class MotionTuning
    {
        public float MinVisibility = .5f, UpperMinVisibility = .3f, Smoothing = .85f;
        // One Euro filter (MediaPipe pose defaults): steady at rest, responsive when moving.
        public float FilterMinCutoff = .05f, FilterBeta = 80f, FilterDerivateCutoff = 1f;
        // Short dropouts keep the last reading; a body that vanishes while ducking counts as ducking.
        public float LostGraceSeconds = .5f, DuckHoldSeconds = 1.5f;
        // Gesture conflicts: a lane must hold briefly, actions need confident joints, and ducking or
        // re-acquiring the body blocks lane/jump for a moment so one movement is not read as several.
        public float LaneHoldSeconds = .15f, ActionVisibility = .6f, RefractorySeconds = .3f, ReacquireLockSeconds = .25f;
        public int CalibrationFrames = 20;
        public float CalibrationStillness = .12f;
        // Lanes are real zones: about a third of a stride sideways enters one, so a lean or sway stays in the centre.
        // On foot only the hips count (a side step); leaning the shoulders alone never changes lane.
        public float LaneEnter = .65f, LaneExit = .38f;
        // In the cart the feet stay put: the shoulders tilting over the hips (sideways offset / torso, about 15 degrees) lean it.
        public float CartLeanEnter = .25f, CartLeanExit = .14f;
        public float CrouchDrop = .40f, CrouchConfirmSeconds = .20f, LandingGraceSeconds = .3f;
        // A take-off this soon after a crouch confirms cancels it; a rise this fast (body units/s) is a jump attempt that earns the landing grace.
        public float CrouchJumpCancelSeconds = .35f, JumpAttemptSpeed = .8f;
        public float JumpRise = .15f;
        public float HighKneeLift = .22f, HighKneeHoldSeconds = .35f, StepLift = .06f;
        public float UpperLaneEnter = .75f, UpperLaneExit = .42f, UpperCrouchDrop = .35f, UpperCrouchConfirmSeconds = .12f, UpperHandsAboveNose = .2f;
        public float ArmPumpAmplitude = .6f, ArmSwingAmplitude = .3f;
        public float LostSeconds = 2.5f, BaselineDrift = .01f;
    }

    public sealed class MotionReading
    {
        public int Lane;
        public bool JumpEdge, Crouch, HighKnee, Active;
        public void Clear() { Lane = 0; JumpEdge = Crouch = HighKnee = Active = false; }
    }
}
