using System;
using System.Collections.Generic;
using Muhanok.Application;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Infrastructure
{
    public sealed class MotionCommandBuffer
    {
        private readonly Queue<MotionCommand> commands = new Queue<MotionCommand>(32);
        public void Add(MotionCommand command) { if (commands.Count < 32) commands.Enqueue(command); }
        public bool Read(out MotionCommand command) { command = MotionCommand.Center; if (commands.Count == 0) return false; command = commands.Dequeue(); return true; }
        public void Clear() { commands.Clear(); }
    }
    public sealed class PoseGameInput : IGameInput
    {
        public readonly MotionCommandBuffer Buffer = new MotionCommandBuffer();
        public bool TryRead(out MotionCommand command) => Buffer.Read(out command);
        public void Clear() => Buffer.Clear();
    }

    // Camera + neural network that produces pose frames. Implemented by the Inference Engine assembly.
    public interface IPoseEstimator : IDisposable
    {
        PoseStatus Status { get; }
        string Message { get; }
        Texture CameraTexture { get; }
        // One line for the preview: tracking mode, scores and inference rate.
        string Diagnostics { get; }
        void Start();
        void Stop();
        void Tick(double now);
        bool TryTakeFrame(PoseFrame target);
    }
    public static class PoseEstimatorRegistry
    {
        // Set by Muhanok.PoseInference when the Inference Engine package is installed.
        public static Func<PoseCameraOptions, IPoseEstimator> Factory;
    }

    // Converts classifier readings into the same commands the keyboard produces.
    // Walking lanes are relative steps toward the body zone; the cart uses absolute lean, as before.
    public sealed class PoseCommandSource
    {
        private readonly PoseGameInput output;
        private readonly Func<int> runnerLane;
        private readonly Func<bool> riding;
        private int lastZone;
        private bool lastRiding;
        private double lastCrouch = -10;
        public PoseCommandSource(PoseGameInput target, Func<int> lane, Func<bool> cart) { output = target; runnerLane = lane; riding = cart; }
        public void Reset(int zone) { lastZone = zone; lastRiding = riding(); lastCrouch = -10; output.Clear(); }
        public void Feed(MotionReading reading, double now)
        {
            bool cart = riding();
            if (reading.Lane != lastZone || cart != lastRiding)
            {
                if (cart) output.Buffer.Add(reading.Lane < 0 ? MotionCommand.Left : reading.Lane > 0 ? MotionCommand.Right : MotionCommand.Center);
                else
                {
                    int steps = reading.Lane - runnerLane();
                    for (int i = 0; i < Math.Abs(steps); i++) output.Buffer.Add(steps < 0 ? MotionCommand.Left : MotionCommand.Right);
                }
                lastZone = reading.Lane; lastRiding = cart;
            }
            if (reading.JumpEdge) output.Buffer.Add(MotionCommand.Jump);
            // Held actions refresh the runner's timers while the body keeps the pose.
            if (reading.Crouch && now - lastCrouch >= .2) { output.Buffer.Add(MotionCommand.Crouch); lastCrouch = now; }
            // A knee step is a one-frame edge, sent once.
            if (reading.KneeStep) output.Buffer.Add(MotionCommand.KneeStep);
        }
    }

    // Owns the estimator and classifier for the chosen play style.
    public sealed class PoseTracker : IDisposable
    {
        private readonly PoseCameraOptions options;
        private readonly MotionClassifier classifier;
        private readonly MotionTuning tuning, baseTuning;
        private readonly LandmarkSmoother smoother = new LandmarkSmoother();
        private readonly PoseCommandSource commands;
        private readonly PoseFrame incoming = new PoseFrame();
        private IPoseEstimator estimator;
        private bool estimatorFailed, calibrated;
        public readonly PoseFrame LastFrame = new PoseFrame();
        public PlayStyle Style { get; private set; } = PlayStyle.Keyboard;
        public MotionReading Reading => classifier.Reading;
        // The calibrated pose and the thresholds in use (the tutorial's joint comparison draws the same lines).
        public PoseBaseline Baseline => classifier.Baseline(LastFrame.Aspect);
        public MotionTuning Tuning => tuning;
        public float CalibrationProgress => classifier.CalibrationProgress;
        public bool KneesMissing => UsesCamera && classifier.KneesMissing && classifier.Status == PoseStatus.Calibrating;
        public float StepsPerMinute => classifier.StepsPerMinute;
        public int StepCount => classifier.StepCount;
        // Walking / running / sprinting from the steps a minute; read only for now (the runner speed does not follow it yet).
        public CadenceStage CadenceStage => classifier.CadenceStage;
        public float Bounce => classifier.Bounce;
        public float JumpThreshold => classifier.JumpThreshold;
        // Calibration restarted just now because the player moved.
        public bool CalibrationMoving => UsesCamera && classifier.CalibrationMoving && classifier.Status == PoseStatus.Calibrating;
        // How the player stands in the picture (head to ankles for the full body); NoBody while nobody is tracked.
        private readonly FramingGuide framing = new FramingGuide();
        public FramingIssue Framing => !UsesCamera ? FramingIssue.Ok : estimator == null || estimator.Status != PoseStatus.Tracking || Status == PoseStatus.Searching ? FramingIssue.NoBody : framing.Issue;
        // Joint recording (F6, Editor and development builds): raw landmark frames only, never images.
        private readonly PoseRecorder recorder = new PoseRecorder();
        public bool Recording => recorder.Recording;
        public int RecordedFrames => recorder.Frames;
        public double LastJumpTime { get; private set; } = -10;
        public double LastKneeStepTime { get; private set; } = -10;
        // Big movements seen so far (exercise plan 6); the bootstrap acts on each new one.
        public int BigJumps { get; private set; }
        public int BigKnees { get; private set; }
        public int BigLanes { get; private set; }
        // When the last calibration finished (Tick time, -1 none) and whether tracking dropped or restarted since.
        public double CalibratedAt { get; private set; } = -1;
        public bool LostSinceCalibration { get; private set; } = true;
        public Texture CameraTexture => estimator?.CameraTexture;
        public string Message => estimator?.Message ?? "";
        public string Diagnostics => estimator?.Diagnostics ?? "";
        public bool UsesCamera => PlayStyleInfo.UsesCamera(Style);
        public bool Tracking => Status == PoseStatus.Tracking;
        // The camera delivers pose frames, calibrated or not. Hand pointing needs only this: it reads the shoulders of each frame,
        // so a style switch (the login page's upper-body camera) that restarts calibration must not switch it off.
        public bool CameraLive => UsesCamera && estimator != null && estimator.Status == PoseStatus.Tracking;
        public PoseStatus Status
        {
            get
            {
                if (!UsesCamera) return PoseStatus.Off;
                if (estimator == null) return estimatorFailed ? PoseStatus.NoModel : PoseStatus.Starting;
                var camera = estimator.Status;
                return camera == PoseStatus.Tracking ? classifier.Status : camera;
            }
        }
        // riding: the cart takes absolute lean commands (not inside a fork window).
        // inCart: the whole cart ride, where the full body leans the shoulders instead of stepping aside.
        // onStairs: on or right before a stair chunk, where the jump reading is switched off (MotionClassifier.SetStairs).
        public PoseTracker(PoseGameInput output, PoseCameraOptions camera, MotionTuning tuning, Func<int> lane, Func<bool> riding, Func<bool> inCart = null, Func<bool> onStairs = null)
        {
            // Work on copies: settings change them at runtime and must not edit the GameSettings asset.
            options = Copy(camera ?? new PoseCameraOptions()); baseTuning = Copy(tuning ?? new MotionTuning());
            this.tuning = Copy(baseTuning); classifier = new MotionClassifier(this.tuning);
            commands = new PoseCommandSource(output, lane, riding); this.inCart = inCart; this.onStairs = onStairs;
        }
        private readonly Func<bool> inCart, onStairs;
        // A duck row is just ahead of the runner (raises the jump line); set by the bootstrap.
        public Func<bool> DuckAhead;
        public void SetStyle(PlayStyle style)
        {
            if (style == Style && (estimator != null || !UsesCamera)) return;
            if (style != Style) StopRecording();
            Style = style; classifier.SetStyle(style); commands.Reset(0); ForgetCalibration();
            if (!UsesCamera) { estimator?.Stop(); return; }
            if (estimator == null && !estimatorFailed)
            {
                try { estimator = PoseEstimatorRegistry.Factory?.Invoke(options); }
                catch (Exception error) { Debug.LogException(error); estimator = null; }
                estimatorFailed = estimator == null;
            }
            estimator?.Start();
        }
        private static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        public static string[] CameraDevices()
        {
            var devices = WebCamTexture.devices; var names = new string[devices.Length];
            for (int i = 0; i < devices.Length; i++) names[i] = devices[i].name;
            return names;
        }
        // Settings window: camera device, landmark model size and lane sensitivity (>1 = smaller movement needed).
        public void Configure(string device, bool liteModel, float laneSensitivity)
        {
            float k = Mathf.Clamp(laneSensitivity, .6f, 1.4f);
            tuning.LaneEnter = baseTuning.LaneEnter / k; tuning.LaneExit = baseTuning.LaneExit / k;
            tuning.UpperLaneEnter = baseTuning.UpperLaneEnter / k; tuning.UpperLaneExit = baseTuning.UpperLaneExit / k;
            tuning.UpperLeanEnterDegrees = baseTuning.UpperLeanEnterDegrees / k;
            tuning.CartLeanEnter = baseTuning.CartLeanEnter / k; tuning.CartLeanExit = baseTuning.CartLeanExit / k;
            device = device ?? "";
            if (device == options.DeviceName && liteModel == !options.FullLandmarkModel) return;
            options.DeviceName = device; options.FullLandmarkModel = !liteModel;
            // A new camera or model needs a fresh estimator; restart it if a camera style is active.
            estimator?.Dispose(); estimator = null; estimatorFailed = false; ForgetCalibration();
            if (UsesCamera) { var style = Style; Style = PlayStyle.Keyboard; SetStyle(style); }
        }
        public void SetInferenceRate(float fps) { options.MaxInferenceRate=Mathf.Clamp(fps,15,30); }
        public void RefreshCamera()
        {
            var style=Style;estimator?.Dispose();estimator=null;estimatorFailed=false;
            if(UsesCamera){Style=PlayStyle.Keyboard;SetStyle(style);}
            Recalibrate();
        }
        public void Recalibrate() { classifier.Recalibrate(); smoother.Reset(); commands.Reset(0); ForgetCalibration(); }
        private void ForgetCalibration() { calibrated = false; LostSinceCalibration = true; }
        public void ResetCommands() => commands.Reset(classifier.Reading.Lane);
        public void Tick(double now, bool feedCommands)
        {
            if (!UsesCamera || estimator == null) return;
            estimator.Tick(now);
            // A camera that stops delivering (or the person leaving) after calibration counts as lost tracking.
            if (calibrated && Status != PoseStatus.Tracking) LostSinceCalibration = true;
            // Overlapping inference passes can finish together; every frame is read in capture order.
            while (estimator.TryTakeFrame(incoming)) Read(now, feedCommands);
        }
        private void Read(double now, bool feedCommands)
        {
            recorder.Write(incoming);
            smoother.Apply(incoming, tuning);
            LastFrame.CopyFrom(incoming);
            framing.Update(LastFrame, Style, now);
            classifier.SetCart(inCart != null && inCart());
            classifier.SetStairs(onStairs != null && onStairs());
            classifier.SetDuckAhead(DuckAhead != null && DuckAhead());
            var reading = classifier.Update(incoming);
            if (classifier.Calibrated && !calibrated) { CalibratedAt = now; LostSinceCalibration = false; }
            else if (!classifier.Calibrated || classifier.Status != PoseStatus.Tracking) LostSinceCalibration |= calibrated;
            calibrated = classifier.Calibrated;
            if (reading.JumpEdge) LastJumpTime = now;
            if (reading.KneeStep) LastKneeStepTime = now;
            if (reading.BigJump) BigJumps++;
            if (reading.BigKnee) BigKnees++;
            if (reading.BigLane) BigLanes++;
            if (feedCommands && classifier.Status == PoseStatus.Tracking) commands.Feed(reading, now);
            else commands.Reset(reading.Lane);
        }
        // Starts a recording file in the folder (the name says when and which play style); returns its base name.
        public string StartRecording(string folder)
        {
            if (!UsesCamera) return null;
            string name = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Style;
            var texture = CameraTexture;
            var meta = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("source", PoseRecording.SourceWebcam),
                new KeyValuePair<string, string>("style", Style.ToString()),
                new KeyValuePair<string, string>("camera", (texture != null ? texture.width + "x" + texture.height : options.Width + "x" + options.Height) + "@" + options.Fps),
                new KeyValuePair<string, string>("device", options.DeviceName),
                new KeyValuePair<string, string>("model", options.FullLandmarkModel ? "full" : "lite"),
                new KeyValuePair<string, string>("inference_fps", options.MaxInferenceRate.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("created", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK")),
                new KeyValuePair<string, string>("app", UnityEngine.Application.version),
                new KeyValuePair<string, string>("person", ""),
                new KeyValuePair<string, string>("note", ""),
            };
            recorder.Start(folder, meta, name);
            return name;
        }
        public string StopRecording()
        {
            try { return recorder.Stop(); }
            catch (Exception error) { Debug.LogException(error); return null; }
        }
        public void Dispose() { StopRecording(); estimator?.Dispose(); estimator = null; }
    }
}
