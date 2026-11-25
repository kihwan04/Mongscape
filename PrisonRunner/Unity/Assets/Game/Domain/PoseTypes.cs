using System;

namespace Muhanok.Domain
{
    // Z is the model's depth value as it comes out of the landmark network (relative to the hips, crop units); only the
    // pose recordings keep it, the classifier does not read it.
    public struct PoseLandmark { public float X, Y, Z, Visibility; }

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
        // The lane reads where the body will be this far ahead (seconds), from its sideways speed, to hide camera and
        // inference delay. The lead is capped (body units) so jitter or a sudden stop cannot cross a zone on its own.
        public float LanePredictSeconds = .08f, LanePredictMax = .2f;
        // In the cart the feet stay put: the shoulders tilting over the hips (sideways offset / torso, about 15 degrees) lean it.
        public float CartLeanEnter = .25f, CartLeanExit = .14f;
        public float CrouchDrop = .40f, CrouchConfirmSeconds = .20f, LandingGraceSeconds = .3f;
        // A take-off this soon after a crouch confirms cancels it; a rise this fast (body units/s) is a jump attempt that earns the landing grace.
        public float CrouchJumpCancelSeconds = .35f, JumpAttemptSpeed = .8f;
        public float JumpRise = .15f;
        // A jump lifts both knees together; running in place lifts one at a time. Above this knee lift difference
        // (body units) a rise is not a jump.
        public float JumpKneeSymmetry = .12f;
        // While running, the jump line rises to this many times the player's own running bounce (hip rise per step),
        // never below JumpRise. The bounce follows the last steps (BounceFollow per step).
        public float JumpBounceFactor = 2.5f, BounceFollow = .2f;
        // Right before a duck row the jump line rises this many times, so a running bounce or a dip never reads as a jump there.
        public float JumpDuckFactor = 1.5f;
        // Steps per minute (both knees, last 3 s): up to RunCadence is walking, from SprintCadence sprinting.
        public float RunCadence = 100f, SprintCadence = 150f;
        public float HighKneeLift = .20f, StepLift = .06f;
        // A full body step also needs the hips and the shoulders to rise and fall this much (body units) between knee swaps;
        // knees bent in turn or raised heels while standing still are not running in place (stairs excepted).
        public float StepBob = .012f;
        public float UpperLaneEnter = .75f, UpperLaneExit = .42f, UpperCrouchDrop = .35f, UpperCrouchConfirmSeconds = .12f, UpperHandsAboveNose = .2f;
        // The upper body also leans by tilting the shoulder line (degrees from the calibrated tilt); the exit follows UpperLaneExit/Enter.
        public float UpperLeanEnterDegrees = 12f;
        // Big movements (exercise plan 6): passing stays as easy as before, these only add the in-game reward and +1 MET.
        // Full body: a crouch with the knee angle at or under DeepKneeAngle (half squat) or the hips down DeepHipDrop torsos;
        // a jump whose hips and shoulders both stay over the jump line for BigJumpAirSeconds; a knee lifted to BigKneeHipRatio
        // of the way to hip height; a side step moving hips and shoulders both BigSideStepWidths shoulder widths.
        public float DeepKneeAngle = 105f, DeepHipDrop = .35f, BigJumpAirSeconds = .2f, BigKneeHipRatio = .85f, BigSideStepWidths = 1f;
        // Upper body: arms straight above the head (elbow angle), shoulders down by a head (calibrated nose to shoulder line),
        // a lean of BigLeanDegrees. The stair exercise is a pull-down: hands over the head, then down below the nose (a step each
        // way); down below the shoulders is big.
        public float UpperStraightArm = .9f, UpperBigLeanDegrees = 20f;
        // Upper body steps: the wrists swing apart by ArmSwingAmplitude shoulder widths and the elbows go along by ArmSwingElbow
        // (a flick of the wrists is not a swing). Raised from .3 after QA 2026-11-25.
        public float ArmPumpAmplitude = .6f, ArmSwingAmplitude = .5f, ArmSwingElbow = .15f;
        public float LostSeconds = 2.5f, BaselineDrift = .01f;
    }

    public enum CadenceStage { Standing, Walk, Run, Sprint }
    public static class Cadence
    {
        public const double WindowSeconds = 3;
        public static CadenceStage Stage(float stepsPerMinute, MotionTuning tuning)
        {
            tuning ??= new MotionTuning();
            if (stepsPerMinute <= 0) return CadenceStage.Standing;
            return stepsPerMinute >= tuning.SprintCadence ? CadenceStage.Sprint : stepsPerMinute > tuning.RunCadence ? CadenceStage.Run : CadenceStage.Walk;
        }
        public static string Korean(CadenceStage stage) => stage == CadenceStage.Walk ? "걷기" : stage == CadenceStage.Run ? "달리기" : stage == CadenceStage.Sprint ? "전력" : "서 있음";
    }

    public sealed class MotionReading
    {
        public int Lane;
        public bool JumpEdge, KneeStep, Crouch, Active;
        // Big movements: one-frame edges for jump, knee step and lane change; CrouchDeep holds while the crouch is deep.
        public bool BigJump, BigKnee, BigLane, CrouchDeep;
        public void Clear() { Lane = 0; JumpEdge = KneeStep = Crouch = Active = BigJump = BigKnee = BigLane = CrouchDeep = false; }
    }
}
