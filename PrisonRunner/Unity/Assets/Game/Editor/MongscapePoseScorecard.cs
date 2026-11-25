using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace Muhanok.Editor
{
    // Mongscape → Pose Scorecard: replays every labelled recording in Tools/QA/PoseRecordings with the game's motion tuning
    // and writes Logs/PoseScorecard.md. The sweep variant tries a few jump settings at once (docs/POSE_RECORDINGS.md).
    public static class MongscapePoseScorecard
    {
        private static string Folder => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../Tools/QA/PoseRecordings"));
        private static string Output(string name) => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Logs/" + name));

        private static MotionTuning GameTuning()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Content/Data/GameSettings.asset");
            var field = settings != null ? settings.GetType().GetField("Motion") : null;
            var tuning = field?.GetValue(settings) as MotionTuning;
            return tuning != null ? JsonUtility.FromJson<MotionTuning>(JsonUtility.ToJson(tuning)) : new MotionTuning();
        }

        [MenuItem("Mongscape/Pose Scorecard")]
        public static void Run()
        {
            var skipped = new List<string>();
            var entries = PoseRecordingFiles.LoadFolder(Folder, skipped);
            string report = PoseScorecard.Report(PoseRecordingFiles.Score(entries, GameTuning()));
            if (skipped.Count > 0) report += "\n라벨 파일이 없어 건너뛴 녹화: " + string.Join(", ", skipped.ConvertAll(Path.GetFileName)) + "\n";
            Write("PoseScorecard.md", report);
        }

        // Jump symmetry × bounce factor: false jumps while running against real jumps recognised, per recording set.
        [MenuItem("Mongscape/Pose Scorecard Sweep (jump)")]
        public static void Sweep()
        {
            var entries = PoseRecordingFiles.LoadFolder(Folder);
            var text = new StringBuilder("# 점프 기준값 비교\n\n합성 녹화는 [합성]으로 따로 셉니다.\n\n| 무릎 대칭 | 들썩임 배수 | 실제: 점프 정답률 | 실제: 제자리 달리기 중 잘못된 점프 | [합성] 점프 정답률 | [합성] 제자리 달리기 중 잘못된 점프 |\n| ---: | ---: | ---: | ---: | ---: | ---: |\n");
            foreach (float symmetry in new[] { .08f, .12f, .16f, 99f })
                foreach (float factor in new[] { 0f, 2f, 2.5f, 3f })
                {
                    var tuning = GameTuning(); tuning.JumpKneeSymmetry = symmetry; tuning.JumpBounceFactor = factor;
                    var results = PoseRecordingFiles.Score(entries, tuning);
                    text.Append("| ").Append(symmetry >= 99 ? "끔" : symmetry.ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ").Append(factor == 0 ? "끔" : factor.ToString("0.0", CultureInfo.InvariantCulture));
                    foreach (bool synthetic in new[] { false, true })
                    {
                        var set = results.FindAll(r => r.Synthetic == synthetic);
                        var total = PoseScorecard.Total(set);
                        int falseJumps = 0; foreach (var r in set) falseJumps += r.JumpsWhileRunning;
                        string accuracy = total.TryGetValue(PoseAction.Jump, out var jump) && !double.IsNaN(jump.Accuracy) ? (jump.Accuracy * 100).ToString("0", CultureInfo.InvariantCulture) + "%" : "-";
                        text.Append(" | ").Append(set.Count == 0 ? "-" : accuracy).Append(" | ").Append(set.Count == 0 ? "-" : falseJumps.ToString(CultureInfo.InvariantCulture));
                    }
                    text.Append(" |\n");
                }
            Write("PoseScorecardSweep.md", text.ToString());
        }

        private static void Write(string name, string text)
        {
            string path = Output(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Debug.Log("Pose scorecard written: " + path + "\n" + text);
            EditorUtility.RevealInFinder(path);
        }
    }
}
