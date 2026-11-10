using System;
using System.Globalization;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using Unity.InferenceEngine;
using UnityEngine;

namespace Muhanok.PoseInference
{
    // Webcam -> BlazePose detector (224) -> landmark model (256) on the GPU, read back without blocking.
    // Models: Unity's Inference Engine conversion of Google's MediaPipe BlazePose (Apache-2.0), in Resources/MuhanokPose.
    // While the body is tracked, up to PipelineDepth landmark passes are in flight at once (one per camera frame),
    // so the GPU readback wait of one frame overlaps the next instead of halving the rate.
    public sealed class BlazePoseEstimator : IPoseEstimator
    {
        private const string Folder = "MuhanokPose/";
        private const int DetectorSize = 224, LandmarkSize = 256, AnchorCount = 2254, Landmarks = 33;
        // 33 landmarks plus the two alignment points, five values each.
        private const int LandmarkValues = 5 * 35;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => PoseEstimatorRegistry.Factory = options => new BlazePoseEstimator(options);

        // The camera frame an inference ran on.
        private struct FrameInfo { public double Time; public int Width, Height; public bool Flipped; }

        // One landmark pass: its own worker and buffers, so two passes can be read back independently.
        private sealed class Slot
        {
            public Worker Worker;
            public Model Model;
            public Tensor<float> Input, Pending;
            public readonly float[] Values = new float[LandmarkValues];
            public bool Busy, Copied, FromTracking;
            public long Sequence;
            public double Started;
            public FrameInfo Frame;
            public Affine ToImage;
            public float Radius;
        }

        private readonly PoseCameraOptions options;
        private WebCamTexture camera;
        private Worker detector;
        private Tensor<float> detectorInput;
        private Slot[] slots = new Slot[0];
        private long sequence;
        private Tensor<int> pendingIndex;
        private Tensor<float> pendingScore, pendingBox;
        private float[,] anchors;
        private ComputeShader sampler;
        private int kernel;
        private bool loaded, logits, logitsKnown;
        // A camera frame arrived since the last inference started (didUpdateThisFrame is true for one game frame only).
        private bool freshFrame;
        private bool detecting;
        private double detectStarted;
        private FrameInfo detectFrame;
        private Affine detectorToImage;
        // Finished frames waiting for TryTakeFrame, oldest first.
        private readonly PoseFrame[] queue = { new PoseFrame(), new PoseFrame() };
        private int queueHead, queueCount;
        // Landmarks of the pass being read; copied into the queue only when published.
        private readonly PoseFrame scratch = new PoseFrame();
        private double openedAt, statsSince = -1, lastStart = -10;
        private bool tracking;
        private Vector2 roiHip, roiTop;
        private float shoulderQuality, lastDetectorScore;
        private int published, detections, lostTracks, cameraFrames, latencySamples, mostInFlight;
        private double latencySum;
        public string Diagnostics { get; private set; } = "";
        private bool gotIndex, gotScore, gotBox;
        private int detectedIndex;
        private float detectedScore;
        private readonly float[] detectedBox = new float[12];
        private string[] candidates = new string[0];
        private int candidate, attempt;
        public PoseStatus Status { get; private set; } = PoseStatus.Off;
        public string Message { get; private set; } = "";
        public Texture CameraTexture => camera;

        public BlazePoseEstimator(PoseCameraOptions cameraOptions) { options = cameraOptions ?? new PoseCameraOptions(); }

        public void Start()
        {
            if (!Load()) { Status = PoseStatus.NoModel; return; }
            ResetPipeline();
            if (camera != null) { if (!camera.isPlaying) { camera.Play(); openedAt = Time.realtimeSinceStartupAsDouble; } Status = PoseStatus.Starting; return; }
            var devices = WebCamTexture.devices;
            if (devices.Length == 0) { Status = PoseStatus.NoCamera; Message = "연결된 웹캠이 없습니다."; Debug.LogWarning("Muhanok pose: no webcam devices."); return; }
            // Preferred device first, then real cameras, then virtual ones (OBS and similar often fail to start).
            var names = new System.Collections.Generic.List<string>();
            foreach (var d in devices) if (!string.IsNullOrEmpty(options.DeviceName) && d.name.Contains(options.DeviceName)) names.Add(d.name);
            foreach (var d in devices) if (!names.Contains(d.name) && !IsVirtual(d.name)) names.Add(d.name);
            foreach (var d in devices) if (!names.Contains(d.name)) names.Add(d.name);
            candidates = names.ToArray(); candidate = 0; attempt = 0;
            Debug.Log("Muhanok pose: webcams = " + string.Join(", ", candidates));
            Open();
        }
        private static bool IsVirtual(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("virtual") || n.Contains("obs") || n.Contains("snap camera") || n.Contains("xsplit") || n.Contains("nvidia broadcast");
        }
        // Attempt 0 asks for the configured size; attempt 1 lets the driver choose its default mode.
        private void Open()
        {
            string device = candidates[candidate];
            camera = attempt == 0 ? new WebCamTexture(device, options.Width, options.Height, options.Fps) : new WebCamTexture(device);
            camera.Play(); openedAt = Time.realtimeSinceStartupAsDouble;
            Message = device; Status = PoseStatus.Starting;
        }
        // The driver failed to deliver frames: try the default mode, then the next device.
        private void Retry()
        {
            Debug.LogWarning("Muhanok pose: webcam '" + candidates[candidate] + "' did not start" + (attempt == 0 ? " at " + options.Width + "x" + options.Height + "; trying its default mode." : "; trying the next device."));
            camera.Stop(); UnityEngine.Object.Destroy(camera); camera = null;
            if (++attempt > 1) { attempt = 0; candidate++; }
            if (candidate >= candidates.Length)
            {
                Status = PoseStatus.NoCamera;
                Message = "웹캠을 열 수 없습니다. 다른 프로그램이 카메라를 쓰고 있는지, Windows 설정 > 개인 정보 > 카메라에서 데스크톱 앱 접근이 켜져 있는지 확인하세요.";
                Debug.LogWarning("Muhanok pose: " + Message);
                return;
            }
            Open();
        }

        public void Stop()
        {
            if (camera != null && camera.isPlaying) camera.Stop();
            if (Status != PoseStatus.NoCamera) Status = PoseStatus.Off;
            ResetPipeline();
        }
        // In-flight GPU work is abandoned; its readbacks are never collected.
        private void ResetPipeline()
        {
            foreach (var slot in slots) slot.Busy = slot.Copied = false;
            detecting = gotIndex = gotScore = gotBox = false; tracking = false; freshFrame = false;
            queueHead = queueCount = 0; statsSince = -1;
        }

        private bool Load()
        {
            if (loaded) return true;
            if (!SystemInfo.supportsComputeShaders) { Message = "이 PC의 그래픽 카드가 컴퓨트 셰이더를 지원하지 않습니다."; return false; }
            var detectorAsset = Resources.Load<ModelAsset>(Folder + "pose_detection");
            var landmarkAsset = Resources.Load<ModelAsset>(Folder + (options.FullLandmarkModel ? "pose_landmarks_detector_full" : "pose_landmarks_detector_lite"));
            var anchorText = Resources.Load<TextAsset>(Folder + "anchors");
            sampler = Resources.Load<ComputeShader>(Folder + "PoseImageSampler");
            if (detectorAsset == null || landmarkAsset == null || anchorText == null || sampler == null) { Message = "포즈 모델 파일을 찾을 수 없습니다."; return false; }
            try
            {
                anchors = ParseAnchors(anchorText.text);
                var detectorModel = ModelLoader.Load(detectorAsset);
                // Keep only the best anchor on the GPU: index, sigmoid score and its box/keypoints.
                var graph = new FunctionalGraph();
                var input = graph.AddInput(detectorModel, 0);
                var outputs = Functional.Forward(detectorModel, input);
                var boxes = outputs[0];
                var scores = outputs[1];
                var best = Functional.ArgMax(scores, 1).Squeeze();
                var bestScore = Functional.Sigmoid(Functional.Clamp(scores, -100f, 100f)).IndexSelect(1, best).Unsqueeze(0);
                var bestBox = boxes.IndexSelect(1, best).Unsqueeze(0);
                detector = new Worker(graph.Compile(best, bestScore, bestBox), BackendType.GPUCompute);
                detectorInput = new Tensor<float>(new TensorShape(1, DetectorSize, DetectorSize, 3));
                // Each slot loads its own copy of the landmark model so the workers share nothing.
                slots = new Slot[Mathf.Clamp(options.PipelineDepth, 1, 2)];
                for (int i = 0; i < slots.Length; i++)
                {
                    var model = ModelLoader.Load(landmarkAsset);
                    slots[i] = new Slot { Model = model, Worker = new Worker(model, BackendType.GPUCompute), Input = new Tensor<float>(new TensorShape(1, LandmarkSize, LandmarkSize, 3)) };
                }
                kernel = sampler.FindKernel("SampleAffine");
                loaded = true;
            }
            catch (Exception error)
            {
                Debug.LogException(error); Message = "포즈 모델을 불러오지 못했습니다: " + error.Message; DisposeModels();
            }
            return loaded;
        }

        public void Tick(double now)
        {
            if (!loaded || camera == null || Status == PoseStatus.Off) return;
            if (!camera.isPlaying || camera.width <= 16)
            {
                if (now - openedAt > 3) Retry(); else Status = PoseStatus.Starting;
                return;
            }
            if (Status == PoseStatus.Starting) Status = PoseStatus.Tracking;
            if (camera.didUpdateThisFrame) { freshFrame = true; cameraFrames++; }
            if (detecting) CollectDetection(now);
            CollectLandmarks(now);
            UpdateStats(now);
            // Starts in the same frame a result came back; a camera frame that arrived meanwhile is not lost.
            if (!freshFrame || detecting || now - lastStart < 1.0 / Math.Max(1f, options.MaxInferenceRate)) return;
            // MediaPipe-style: while the body is tracked, the next region comes from the latest landmarks
            // and the detector only runs again after the body is lost and every pass in flight has finished.
            if (tracking)
            {
                var slot = FreeSlot();
                if (slot == null) return;
                freshFrame = false; lastStart = now;
                StartLandmarks(slot, roiHip, roiTop, Capture(now), true, now);
            }
            else if (!AnyBusy()) { freshFrame = false; lastStart = now; Detect(now); }
        }

        private Slot FreeSlot() { foreach (var s in slots) if (!s.Busy) return s; return null; }
        private bool AnyBusy() { foreach (var s in slots) if (s.Busy) return true; return false; }
        private Slot Oldest()
        {
            Slot oldest = null;
            foreach (var s in slots) if (s.Busy && (oldest == null || s.Sequence < oldest.Sequence)) oldest = s;
            return oldest;
        }
        private FrameInfo Capture(double now) => new FrameInfo { Time = now, Width = camera.width, Height = camera.height, Flipped = camera.videoVerticallyMirrored };

        // Unity keeps async readback data only during the frame the request completes, so each output
        // is copied as soon as it is ready. A failed or stale readback drops that frame instead of throwing.
        private void CollectDetection(double now)
        {
            try
            {
                if (!gotIndex && Ready(pendingIndex)) { using (var t = pendingIndex.ReadbackAndClone()) detectedIndex = t[0]; gotIndex = true; }
                if (!gotScore && Ready(pendingScore)) { using (var t = pendingScore.ReadbackAndClone()) detectedScore = t[0]; gotScore = true; }
                if (!gotBox && Ready(pendingBox)) { using (var t = pendingBox.ReadbackAndClone()) for (int k = 0; k < 12; k++) detectedBox[k] = t[0, 0, k]; gotBox = true; }
            }
            catch (InvalidOperationException) { DropDetection(); return; }
            if (gotIndex && gotScore && gotBox) ReadDetection(now);
            else if (now - detectStarted > 1) DropDetection();
        }
        private void DropDetection() { detecting = gotIndex = gotScore = gotBox = false; tracking = false; }

        // Every ready pass is copied right away; results are then handed on in capture order.
        private void CollectLandmarks(double now)
        {
            foreach (var slot in slots)
            {
                if (!slot.Busy || slot.Copied) continue;
                if (!Ready(slot.Pending)) { if (now - slot.Started > 1) DropSlot(slot); continue; }
                try
                {
                    using (var values = slot.Pending.ReadbackAndClone())
                    {
                        if (values.shape.length < LandmarkValues) { DropSlot(slot); continue; }
                        for (int i = 0; i < LandmarkValues; i++) slot.Values[i] = values[i];
                    }
                    slot.Copied = true;
                }
                catch (InvalidOperationException) { DropSlot(slot); }
            }
            for (var slot = Oldest(); slot != null && slot.Copied; slot = Oldest()) ReadLandmarks(slot, now);
        }
        private void DropSlot(Slot slot) { slot.Busy = slot.Copied = false; tracking = false; }
        private static bool Ready(Tensor tensor) => tensor != null && tensor.IsReadbackRequestDone();

        private void Detect(double now)
        {
            detectFrame = Capture(now);
            float size = Mathf.Max(detectFrame.Width, detectFrame.Height), scale = size / DetectorSize;
            // Letterbox the frame into the square detector input; tensor rows go down, texture rows go up.
            detectorToImage = Affine.Translation(.5f * (detectFrame.Width - size), .5f * (detectFrame.Height + size)) * Affine.Scale(scale, -scale);
            Sample(detectorInput, DetectorSize, detectorToImage, detectFrame);
            detector.Schedule(detectorInput);
            pendingIndex = detector.PeekOutput(0) as Tensor<int>;
            pendingScore = detector.PeekOutput(1) as Tensor<float>;
            pendingBox = detector.PeekOutput(2) as Tensor<float>;
            pendingIndex.ReadbackRequest(); pendingScore.ReadbackRequest(); pendingBox.ReadbackRequest();
            gotIndex = gotScore = gotBox = false; detectStarted = now;
            detecting = true;
        }

        private void ReadDetection(double now)
        {
            int index = detectedIndex; float score = detectedScore; var box = detectedBox;
            detecting = gotIndex = gotScore = gotBox = false; lastDetectorScore = score; detections++;
            if (score < options.DetectionThreshold || index < 0 || index >= AnchorCount)
            {
                Publish(false, detectFrame, .5f, now); return;
            }
            // BlazePose box keypoint 0 is the hip centre, keypoint 1 a point above the head; they define the body ROI.
            float ax = DetectorSize * anchors[index, 0], ay = DetectorSize * anchors[index, 1];
            var hip = detectorToImage.Apply(ax + box[4], ay + box[5]);
            var top = detectorToImage.Apply(ax + box[6], ay + box[7]);
            // No pass is in flight while detecting, so a slot is free.
            var slot = FreeSlot();
            if (slot != null) StartLandmarks(slot, hip, top, detectFrame, false, now);
        }

        // Crop around the body: centre at the hip point, rotated toward the point above the head, 1.25x margin.
        private void StartLandmarks(Slot slot, Vector2 hip, Vector2 top, FrameInfo frame, bool fromTracking, double now)
        {
            var delta = top - hip;
            float radius = 1.25f * delta.magnitude;
            float theta = Mathf.Atan2(delta.y, delta.x);
            float scale = radius / (.5f * LandmarkSize);
            slot.ToImage = Affine.Translation(hip.x, hip.y) * Affine.Scale(scale, -scale) * Affine.Rotation(.5f * Mathf.PI - theta) * Affine.Translation(-.5f * LandmarkSize, -.5f * LandmarkSize);
            slot.Radius = radius; slot.Frame = frame; slot.FromTracking = fromTracking;
            Sample(slot.Input, LandmarkSize, slot.ToImage, frame);
            slot.Worker.Schedule(slot.Input);
            slot.Pending = LandmarkOutput(slot);
            slot.Pending.ReadbackRequest();
            slot.Busy = true; slot.Copied = false; slot.Sequence = ++sequence; slot.Started = now;
            int inFlight = 0; foreach (var s in slots) if (s.Busy) inFlight++;
            mostInFlight = Math.Max(mostInFlight, inFlight);
        }

        private static Tensor<float> LandmarkOutput(Slot slot)
        {
            foreach (var output in slot.Model.outputs) if (output.name == "Identity") return slot.Worker.PeekOutput("Identity") as Tensor<float>;
            return slot.Worker.PeekOutput(0) as Tensor<float>;
        }

        private void ReadLandmarks(Slot slot, double now)
        {
            slot.Busy = slot.Copied = false;
            var values = slot.Values; var frame = slot.Frame; var toImage = slot.ToImage;
            // Visibility/presence may come as logits depending on the conversion; detect once.
            if (!logitsKnown)
            {
                for (int i = 0; i < Landmarks; i++) { float v = values[5 * i + 3]; if (v < -.01f || v > 1.01f) { logits = true; break; } }
                logitsKnown = logits || frame.Time > 0;
            }
            var target = scratch;
            for (int i = 0; i < Landmarks; i++)
            {
                var image = toImage.Apply(values[5 * i], values[5 * i + 1]);
                float u = image.x / frame.Width, v = image.y / frame.Height;
                if (frame.Flipped) v = 1 - v;
                float visibility = Activate(values[5 * i + 3]), presence = Activate(values[5 * i + 4]);
                // Mirror view: the player's right side appears on the right of the screen.
                target.Points[i] = new PoseLandmark { X = 1 - u, Y = 1 - v, Visibility = Mathf.Min(visibility, presence) };
            }
            // Landmarks 33 and 34 are BlazePose's alignment points (hip centre, above the head) for the next crop.
            var nextHip = toImage.Apply(values[5 * 33], values[5 * 33 + 1]);
            var nextTop = toImage.Apply(values[5 * 34], values[5 * 34 + 1]);
            shoulderQuality = Mathf.Max(target.Points[11].Visibility, target.Points[12].Visibility);
            bool keep = shoulderQuality >= .5f && (nextTop - nextHip).magnitude > 8f;
            tracking = keep;
            if (keep) { roiHip = nextHip; roiTop = nextTop; }
            // A tracked crop that lost the body is not published: the detector runs once the passes in flight finish.
            if (!keep && slot.FromTracking) { lostTracks++; return; }
            var queued = Enqueue(); queued.CopyFrom(target);
            Finish(queued, true, frame, frame.Height > 0 ? 2f * slot.Radius / frame.Height : .5f, now);
        }
        private float Activate(float value) => logits ? 1f / (1f + Mathf.Exp(-value)) : Mathf.Clamp01(value);

        // The next free queue frame; a full queue drops its oldest frame.
        private PoseFrame Enqueue()
        {
            if (queueCount == queue.Length) { queueHead = (queueHead + 1) % queue.Length; queueCount--; }
            var frame = queue[(queueHead + queueCount) % queue.Length]; queueCount++;
            return frame;
        }
        private void Publish(bool pose, FrameInfo frame, float objectScale, double now) => Finish(Enqueue(), pose, frame, objectScale, now);
        private void Finish(PoseFrame target, bool pose, FrameInfo frame, float objectScale, double now)
        {
            target.HasPose = pose; target.Time = frame.Time; target.ObjectScale = objectScale;
            target.Aspect = frame.Height > 0 ? frame.Width / (float)frame.Height : 4f / 3f;
            published++;
            if (pose) { latencySum += now - frame.Time; latencySamples++; }
        }

        // Developer diagnostics: inference vs camera rate, camera-frame-to-result delay and passes in flight.
        private void UpdateStats(double now)
        {
            if (statsSince < 0) { statsSince = now; published = detections = lostTracks = cameraFrames = latencySamples = mostInFlight = 0; latencySum = 0; return; }
            double span = now - statsSince;
            if (span < .5) return;
            string latency = latencySamples > 0 ? (latencySum / latencySamples * 1000).ToString("0") + "ms" : "-";
            Diagnostics = (tracking ? "이어서 추적" : "사람 찾기") + " · 어깨 " + shoulderQuality.ToString("0.00") + " · 검출 " + lastDetectorScore.ToString("0.00")
                + " · 추론 " + (published / span).ToString("0") + "fps / 카메라 " + (cameraFrames / span).ToString("0") + "fps"
                + " · 지연 " + latency + " · 동시 " + mostInFlight + "/" + slots.Length + " · 재검출 " + detections + " · 놓침 " + lostTracks;
            published = detections = lostTracks = cameraFrames = latencySamples = mostInFlight = 0; latencySum = 0; statsSince = now;
        }

        public bool TryTakeFrame(PoseFrame target)
        {
            if (queueCount == 0) return false;
            target.CopyFrom(queue[queueHead]); queueHead = (queueHead + 1) % queue.Length; queueCount--;
            return true;
        }

        private void Sample(Tensor<float> tensor, int size, Affine transform, FrameInfo frame)
        {
            var data = ComputeTensorData.Pin(tensor);
            sampler.SetTexture(kernel, "Source", camera);
            sampler.SetBuffer(kernel, "Output", data.buffer);
            sampler.SetInt("OutWidth", size); sampler.SetInt("OutHeight", size);
            sampler.SetInt("SourceWidth", frame.Width); sampler.SetInt("SourceHeight", frame.Height);
            sampler.SetVector("Row0", new Vector4(transform.A, transform.B, transform.C, 0));
            sampler.SetVector("Row1", new Vector4(transform.D, transform.E, transform.F, 0));
            sampler.SetInt("ToSrgb", options.ConvertToSrgb ? 1 : 0);
            sampler.Dispatch(kernel, (size + 7) / 8, (size + 7) / 8, 1);
        }

        private static float[,] ParseAnchors(string csv)
        {
            var result = new float[AnchorCount, 4];
            var lines = csv.Split('\n');
            if (lines.Length < AnchorCount) throw new FormatException("anchors.csv has " + lines.Length + " lines");
            for (int i = 0; i < AnchorCount; i++)
            {
                var values = lines[i].Split(',');
                for (int j = 0; j < 4; j++) result[i, j] = float.Parse(values[j], CultureInfo.InvariantCulture);
            }
            return result;
        }

        private void DisposeModels()
        {
            detector?.Dispose(); detectorInput?.Dispose();
            foreach (var slot in slots) { slot?.Worker?.Dispose(); slot?.Input?.Dispose(); }
            detector = null; detectorInput = null; slots = new Slot[0]; loaded = false;
        }
        public void Dispose()
        {
            Stop();
            if (camera != null) { UnityEngine.Object.Destroy(camera); camera = null; }
            DisposeModels();
        }

        // Row-major 2x3 affine transform: (x, y) -> (A x + B y + C, D x + E y + F).
        private readonly struct Affine
        {
            public readonly float A, B, C, D, E, F;
            public Affine(float a, float b, float c, float d, float e, float f) { A = a; B = b; C = c; D = d; E = e; F = f; }
            public static Affine Translation(float x, float y) => new Affine(1, 0, x, 0, 1, y);
            public static Affine Scale(float x, float y) => new Affine(x, 0, 0, 0, y, 0);
            public static Affine Rotation(float theta)
            {
                float s = Mathf.Sin(theta), c = Mathf.Cos(theta);
                return new Affine(c, -s, 0, s, c, 0);
            }
            public static Affine operator *(Affine l, Affine r) => new Affine(
                l.A * r.A + l.B * r.D, l.A * r.B + l.B * r.E, l.A * r.C + l.B * r.F + l.C,
                l.D * r.A + l.E * r.D, l.D * r.B + l.E * r.E, l.D * r.C + l.E * r.F + l.F);
            public Vector2 Apply(float x, float y) => new Vector2(A * x + B * y + C, D * x + E * y + F);
        }
    }
}
