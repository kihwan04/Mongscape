using System;
using Unity.Profiling;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Toggleable measurement overlay (F3 / settings): fps, frame time average and worst over the last second,
    // main-thread time and draw counts when the profiler counters are available (editor and development builds),
    // memory, and the pose pipeline diagnostics line.
    public sealed class PerformanceOverlay : MonoBehaviour
    {
        public bool Visible;
        public Func<string> PoseLine;
        public Func<string> QualityLine;
        private readonly float[] frames = new float[240];
        private int count, head;
        private float elapsed;
        private string text = "";
        private GUIStyle style;
        private Texture2D background;
        private ProfilerRecorder mainThread, batches, setPass, triangles;
        private bool recording;
        private readonly System.Text.StringBuilder line = new System.Text.StringBuilder(320);
        private readonly GUIContent content = new GUIContent();
        private string measuredText;
        private Vector2 measuredSize;

        private void StartRecording()
        {
            if (recording) return;
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            recording = true;
        }
        private void OnDisable()
        {
            if (!recording) return;
            mainThread.Dispose(); batches.Dispose(); setPass.Dispose(); triangles.Dispose(); recording = false;
        }

        private void Update()
        {
            if (!Visible) { if (recording) OnDisable(); return; }
            StartRecording();
            float dt = Time.unscaledDeltaTime;
            frames[head] = dt; head = (head + 1) % frames.Length; count = Math.Min(count + 1, frames.Length);
            elapsed += dt;
            if (elapsed < .5f) return;
            elapsed = 0;
            // Statistics over the last second of frames.
            float sum = 0, worst = 0; int n = 0;
            for (int i = 0; i < count && sum < 1f; i++)
            {
                float f = frames[(head - 1 - i + frames.Length) % frames.Length];
                sum += f; worst = Math.Max(worst, f); n++;
            }
            float average = n > 0 ? sum / n : 0;
            line.Clear();
            line.Append("FPS ").Append(average > 0 ? (1f / average).ToString("0") : "-")
                .Append("   평균 ").Append((average * 1000).ToString("0.0")).Append("ms")
                .Append("   최대 ").Append((worst * 1000).ToString("0.0")).Append("ms");
            if (mainThread.Valid && mainThread.Count > 0) line.Append("\nCPU 메인 ").Append((Average(mainThread) * 1e-6).ToString("0.0")).Append("ms");
            if (batches.Valid) line.Append("   배치 ").Append(batches.LastValue).Append("   SetPass ").Append(setPass.LastValue).Append("   삼각형 ").Append((triangles.LastValue / 1000f).ToString("0")).Append("k");
            line.Append("\n메모리 ").Append((GC.GetTotalMemory(false) / 1048576f).ToString("0")).Append("MB (관리) · ")
                .Append((UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f).ToString("0")).Append("MB (전체)");
            string quality = QualityLine?.Invoke(); if (!string.IsNullOrEmpty(quality)) line.Append('\n').Append(quality);
            string pose = PoseLine?.Invoke(); if (!string.IsNullOrEmpty(pose)) line.Append("\n포즈 ").Append(pose);
            text = line.ToString();
        }
        private static double Average(ProfilerRecorder recorder)
        {
            int samples = recorder.Count; if (samples == 0) return 0;
            double total = 0; for (int i = 0; i < samples; i++) total += recorder.GetSample(i).Value;
            return total / samples;
        }

        private void OnGUI()
        {
            if (!Visible || Event.current.type != EventType.Repaint) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = false, normal = { textColor = new Color(.55f, 1f, .6f) } };
                style.font = UiFont.Main;
                background = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave }; background.SetPixel(0, 0, new Color(0, 0, 0, .65f)); background.Apply();
            }
            if (measuredText != text)
            {
                measuredText = text; content.text = text;
                measuredSize = style.CalcSize(content);
                measuredSize.y = style.CalcHeight(content, Mathf.Max(measuredSize.x, 10));
            }
            var size = measuredSize; float height = size.y;
            var rect = new Rect(Screen.width - size.x - 22, 10, size.x + 14, height + 10);
            GUI.depth = -100;
            GUI.DrawTexture(rect, background);
            GUI.Label(new Rect(rect.x + 7, rect.y + 5, size.x, height), content, style);
        }
        private void OnDestroy() { if (background != null) Destroy(background); }
    }
}
