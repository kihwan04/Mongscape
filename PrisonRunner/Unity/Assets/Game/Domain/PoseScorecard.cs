using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Muhanok.Domain
{
    // Replays labelled pose recordings through the same smoother and classifier the game uses, and scores each action:
    // how many labelled actions were recognised and how many recognitions happened where nothing (of that kind) was labelled.
    // Changing a threshold only needs this run again; Sweep tries several tunings at once.
    public sealed class PoseScorecard
    {
        // An event may come this much before or after its label window (camera delay, labelling by hand).
        public const double Margin = .3;
        // The cadence needs a few steps before it can read the pace; the first seconds of a pace label are not scored.
        public const double CadenceWarmUp = 2;

        public sealed class Event { public double Time; public PoseAction Action; }
        public sealed class Line
        {
            public PoseAction Action;
            public int Expected, Hits, False;
            // Cadence labels: frames scored and how many had the right stage, and the mean steps a minute over them.
            public int Frames, Correct; public double CadenceSum;
            public bool IsPace => Action == PoseAction.Stand || Action == PoseAction.Walk || Action == PoseAction.Run || Action == PoseAction.Sprint;
            public double Accuracy => IsPace ? (Frames == 0 ? double.NaN : Correct / (double)Frames) : (Expected == 0 ? double.NaN : Hits / (double)Expected);
            public double MeanCadence => Frames == 0 ? 0 : CadenceSum / Frames;
            public void Add(Line other) { Expected += other.Expected; Hits += other.Hits; False += other.False; Frames += other.Frames; Correct += other.Correct; CadenceSum += other.CadenceSum; }
        }
        public sealed class Result
        {
            public string Name;
            public bool Synthetic;
            public double Seconds;
            public readonly List<Event> Events = new List<Event>();
            public readonly SortedDictionary<PoseAction, Line> Lines = new SortedDictionary<PoseAction, Line>();
            // Jumps recognised inside run or sprint labels, and the seconds of those labels: the "false jump while sprinting" check.
            public int JumpsWhileRunning; public double RunningSeconds;
            public Line LineOf(PoseAction action) { if (!Lines.TryGetValue(action, out var line)) Lines[action] = line = new Line { Action = action }; return line; }
        }

        public static Result Score(string name, PoseRecording recording, IList<PoseLabel> labels, MotionTuning tuning = null)
        {
            tuning ??= new MotionTuning();
            var result = new Result { Name = name, Synthetic = recording.Synthetic, Seconds = recording.Duration };
            var classifier = new MotionClassifier(tuning); classifier.SetStyle(recording.Style);
            var smoother = new LandmarkSmoother();
            var frame = new PoseFrame();
            int lane = 0; bool crouch = false;
            foreach (var source in recording.Frames)
            {
                frame.CopyFrom(source);
                double t = frame.Time;
                classifier.SetStairs(Inside(labels, PoseAction.Stairs, t));
                classifier.SetCart(Inside(labels, PoseAction.Cart, t));
                if (frame.HasPose) smoother.Apply(frame, tuning);
                var reading = classifier.Update(frame);
                if (!classifier.Calibrated) { lane = 0; crouch = false; continue; }
                if (reading.JumpEdge) result.Events.Add(new Event { Time = t, Action = PoseAction.Jump });
                // Knee steps only matter on stairs (the game reads them nowhere else).
                if (reading.KneeStep && (Inside(labels, PoseAction.Stairs, t) || Inside(labels, PoseAction.Knee, t))) result.Events.Add(new Event { Time = t, Action = PoseAction.Knee });
                if (reading.Crouch && !crouch) result.Events.Add(new Event { Time = t, Action = PoseAction.Crouch });
                crouch = reading.Crouch;
                if (reading.Lane != lane && reading.Lane != 0) result.Events.Add(new Event { Time = t, Action = reading.Lane < 0 ? PoseAction.SideLeft : PoseAction.SideRight });
                lane = reading.Lane;
                foreach (var label in labels)
                {
                    if (!IsPaceAction(label.Action)) continue;
                    double warm = Math.Min(CadenceWarmUp, (label.End - label.Start) * .5);
                    if (t < label.Start + warm || t > label.End) continue;
                    var line = result.LineOf(label.Action);
                    line.Frames++; line.CadenceSum += classifier.StepsPerMinute;
                    if (StageOf(label.Action) == classifier.CadenceStage) line.Correct++;
                }
            }
            foreach (var action in new[] { PoseAction.Jump, PoseAction.Crouch, PoseAction.Knee, PoseAction.SideLeft, PoseAction.SideRight })
            {
                var events = result.Events.FindAll(e => e.Action == action);
                var used = new bool[events.Count];
                bool labelled = false;
                foreach (var label in labels)
                {
                    if (label.Action != action) continue;
                    labelled = true;
                    int found = 0;
                    for (int i = 0; i < events.Count; i++)
                        if (!used[i] && label.Contains(events[i].Time, Margin)) { used[i] = true; found++; }
                    var line = result.LineOf(action);
                    line.Expected += label.Count; line.Hits += Math.Min(found, label.Count); line.False += Math.Max(0, found - label.Count);
                }
                int stray = 0; foreach (bool u in used) if (!u) stray++;
                if (stray > 0 || labelled) result.LineOf(action).False += stray;
            }
            foreach (var label in labels)
            {
                if (label.Action != PoseAction.Run && label.Action != PoseAction.Sprint && label.Action != PoseAction.Walk) continue;
                result.RunningSeconds += label.End - label.Start;
                foreach (var e in result.Events) if (e.Action == PoseAction.Jump && label.Contains(e.Time)) result.JumpsWhileRunning++;
            }
            return result;
        }

        public static bool IsPaceAction(PoseAction action) => action == PoseAction.Stand || action == PoseAction.Walk || action == PoseAction.Run || action == PoseAction.Sprint;
        public static CadenceStage StageOf(PoseAction action) => action == PoseAction.Walk ? CadenceStage.Walk : action == PoseAction.Run ? CadenceStage.Run : action == PoseAction.Sprint ? CadenceStage.Sprint : CadenceStage.Standing;
        private static bool Inside(IList<PoseLabel> labels, PoseAction action, double t) { foreach (var label in labels) if (label.Action == action && label.Contains(t)) return true; return false; }

        // All results added up per action.
        public static SortedDictionary<PoseAction, Line> Total(IEnumerable<Result> results)
        {
            var total = new SortedDictionary<PoseAction, Line>();
            foreach (var result in results)
                foreach (var pair in result.Lines)
                {
                    if (!total.TryGetValue(pair.Key, out var line)) total[pair.Key] = line = new Line { Action = pair.Key };
                    line.Add(pair.Value);
                }
            return total;
        }

        // Markdown report. Synthetic and webcam recordings are kept apart and the synthetic part is marked as such:
        // it shows that the pipeline works, not how well the game recognises real people.
        public static string Report(IList<Result> results, string title = "인식 점수표")
        {
            var text = new StringBuilder();
            text.AppendLine("# " + title).AppendLine();
            var webcam = new List<Result>(); var synthetic = new List<Result>();
            foreach (var r in results) (r.Synthetic ? synthetic : webcam).Add(r);
            Section(text, "실제 웹캠 녹화", webcam, false);
            Section(text, "[합성] 스크립트로 만든 몸 (실제 정확도 아님)", synthetic, true);
            return text.ToString();
        }

        private static void Section(StringBuilder text, string heading, List<Result> results, bool synthetic)
        {
            text.AppendLine("## " + heading).AppendLine();
            if (results.Count == 0) { text.AppendLine("녹화 없음.").AppendLine(); return; }
            string tag = synthetic ? "[합성] " : "";
            double seconds = 0; foreach (var r in results) seconds += r.Seconds;
            text.AppendLine(tag + "녹화 " + results.Count + "개, " + seconds.ToString("0", CultureInfo.InvariantCulture) + "초").AppendLine();
            text.AppendLine("| 동작 | 라벨 | 맞힘 | 정답률 | 오인식 | 평균 분당 걸음 |");
            text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
            foreach (var line in Total(results).Values)
            {
                string labels = line.IsPace ? line.Frames + "프레임" : line.Expected.ToString(CultureInfo.InvariantCulture);
                string hits = line.IsPace ? line.Correct.ToString(CultureInfo.InvariantCulture) : line.Hits.ToString(CultureInfo.InvariantCulture);
                string accuracy = double.IsNaN(line.Accuracy) ? "-" : (line.Accuracy * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
                string cadence = line.IsPace ? line.MeanCadence.ToString("0", CultureInfo.InvariantCulture) : "";
                text.AppendLine("| " + tag + Korean(line.Action) + " | " + labels + " | " + hits + " | " + accuracy + " | " + (line.IsPace ? "" : line.False.ToString(CultureInfo.InvariantCulture)) + " | " + cadence + " |");
            }
            int jumps = 0; double running = 0; foreach (var r in results) { jumps += r.JumpsWhileRunning; running += r.RunningSeconds; }
            text.AppendLine();
            if (running > 0) text.AppendLine(tag + "제자리 달리기 중 잘못된 점프: " + jumps + "회 / " + (running / 60).ToString("0.0", CultureInfo.InvariantCulture) + "분 (합격 기준: 전력 1분에 0회)");
            text.AppendLine();
            text.AppendLine("| 녹화 | 길이 | 동작별 정답률 |").AppendLine("| --- | ---: | --- |");
            foreach (var r in results)
            {
                var parts = new List<string>();
                foreach (var line in r.Lines.Values) parts.Add(Korean(line.Action) + " " + (double.IsNaN(line.Accuracy) ? "-" : (line.Accuracy * 100).ToString("0", CultureInfo.InvariantCulture) + "%") + (line.IsPace || line.False == 0 ? "" : " (오인식 " + line.False + ")"));
                text.AppendLine("| " + tag + r.Name + " | " + r.Seconds.ToString("0", CultureInfo.InvariantCulture) + "초 | " + string.Join(", ", parts) + " |");
            }
            text.AppendLine();
        }

        public static string Korean(PoseAction action)
        {
            switch (action)
            {
                case PoseAction.Stand: return "서 있기";
                case PoseAction.Walk: return "걸음";
                case PoseAction.Run: return "달리기";
                case PoseAction.Sprint: return "전력";
                case PoseAction.Jump: return "점프";
                case PoseAction.Crouch: return "숙이기";
                case PoseAction.Knee: return "무릎 들기";
                case PoseAction.SideLeft: return "왼쪽 사이드 스텝";
                case PoseAction.SideRight: return "오른쪽 사이드 스텝";
                default: return PoseLabels.Name(action);
            }
        }
    }
}
