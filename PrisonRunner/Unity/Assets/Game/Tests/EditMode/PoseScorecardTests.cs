using System;
using System.Collections.Generic;
using System.IO;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using NUnit.Framework;

namespace Muhanok.Tests
{
    // The recognition scorecard over the labelled recordings in Tools/QA/PoseRecordings (docs/POSE_RECORDINGS.md).
    // Synthetic recordings check the pipeline and hold the classifier to its scripted cases; real webcam recordings
    // (team/) are scored and reported, not asserted, until the team agrees on targets.
    public sealed class PoseScorecardTests
    {
        public static string Folder => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../Tools/QA/PoseRecordings"));
        // The report lands next to the editor logs (not committed).
        public static string ReportPath => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Logs/PoseScorecard.md"));

        [Test] public void RecordingTextKeepsEveryJointDepthAndTheMetadata()
        {
            var recording = new PoseRecording();
            recording.Meta["source"] = PoseRecording.SourceWebcam; recording.Meta["style"] = "UpperBody"; recording.Meta["model"] = "lite"; recording.Meta["camera"] = "1280x720@30";
            var frame = new PoseFrame { HasPose = true, Time = .0333, Aspect = 16f / 9f, ObjectScale = .48f };
            for (int i = 0; i < PoseFrame.Count; i++) frame.Points[i] = new PoseLandmark { X = i / 40f, Y = 1 - i / 40f, Z = -.25f + i * .01f, Visibility = .5f + i / 100f };
            recording.Frames.Add(frame);
            recording.Frames.Add(new PoseFrame { HasPose = false, Time = .0666, Aspect = 16f / 9f });
            var text = new StringWriter(); recording.Write(text);
            var back = PoseRecording.Read(new StringReader(text.ToString()));
            Assert.AreEqual(PlayStyle.UpperBody, back.Style); Assert.AreEqual("lite", back.Get("model")); Assert.AreEqual("1280x720@30", back.Get("camera")); Assert.IsFalse(back.Synthetic);
            Assert.AreEqual(2, back.Frames.Count); Assert.IsFalse(back.Frames[1].HasPose);
            for (int i = 0; i < PoseFrame.Count; i++)
            {
                Assert.AreEqual(frame.Points[i].X, back.Frames[0].Points[i].X, 1e-4); Assert.AreEqual(frame.Points[i].Y, back.Frames[0].Points[i].Y, 1e-4);
                Assert.AreEqual(frame.Points[i].Z, back.Frames[0].Points[i].Z, 1e-3); Assert.AreEqual(frame.Points[i].Visibility, back.Frames[0].Points[i].Visibility, 1e-3);
            }
            Assert.Throws<FormatException>(() => PoseRecording.Read(new StringReader("t,has\n0,1,1,1")), "The first line names the format");
        }

        [Test] public void LabelsReadActionsWindowsAndCounts()
        {
            var labels = PoseLabels.Read(new StringReader("# action,start,end[,count]\nrun,2,30\njump,35.1,35.7\nknee,40,50,12\nstairs,40,50\n"));
            Assert.AreEqual(4, labels.Count);
            Assert.AreEqual(PoseAction.Run, labels[0].Action); Assert.AreEqual(35.1, labels[1].Start, 1e-9); Assert.AreEqual(12, labels[2].Count); Assert.AreEqual(PoseAction.Stairs, labels[3].Action);
            Assert.Throws<FormatException>(() => PoseLabels.Read(new StringReader("hop,1,2")));
        }

        [Test] public void ScorecardCountsHitsAndFalseRecognitions()
        {
            SyntheticPoseRecordings.Build("synthetic-actions", out var recording, out var labels);
            // Drop the second jump's label: its jump becomes a false recognition, and the others still count.
            int second = labels.FindIndex(1, l => l.Action == PoseAction.Jump);
            labels.RemoveAt(second);
            var result = PoseScorecard.Score("check", recording, labels);
            var jump = result.Lines[PoseAction.Jump];
            Assert.AreEqual(4, jump.Expected); Assert.AreEqual(4, jump.Hits); Assert.AreEqual(1, jump.False);
            Assert.AreEqual(1.0, result.Lines[PoseAction.Crouch].Accuracy, 1e-9);
        }

        // Every labelled recording in the folder: writes Logs/PoseScorecard.md and prints it to the test output.
        [Test] public void ScorecardOverTheRecordingFolder()
        {
            var skipped = new List<string>();
            var entries = PoseRecordingFiles.LoadFolder(Folder, skipped);
            Assert.IsNotEmpty(entries, "No labelled recordings under " + Folder);
            var results = PoseRecordingFiles.Score(entries);
            string report = PoseScorecard.Report(results);
            if (skipped.Count > 0) report += "\n라벨 파일이 없어 건너뛴 녹화: " + string.Join(", ", skipped.ConvertAll(Path.GetFileName)) + "\n";
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report);
            TestContext.WriteLine(report);
            // The synthetic seeds are scripted: they must stay recognised, and the heavy sprint must not jump.
            var synthetic = results.FindAll(r => r.Synthetic);
            Assert.AreEqual(SyntheticPoseRecordings.Names.Length, synthetic.Count, "Synthetic seeds missing; regenerate them (SyntheticSeedRecordingsCanBeRegenerated)");
            var total = PoseScorecard.Total(synthetic);
            foreach (var action in new[] { PoseAction.Jump, PoseAction.Crouch, PoseAction.Knee, PoseAction.SideLeft, PoseAction.SideRight })
            {
                Assert.GreaterOrEqual(total[action].Accuracy, .95, "[합성] " + PoseScorecard.Korean(action));
                Assert.AreEqual(0, total[action].False, "[합성] " + PoseScorecard.Korean(action) + " false recognitions");
            }
            foreach (var action in new[] { PoseAction.Walk, PoseAction.Run, PoseAction.Sprint, PoseAction.Stand })
                Assert.GreaterOrEqual(total[action].Accuracy, .9, "[합성] " + PoseScorecard.Korean(action) + " stage");
            int falseJumps = 0; foreach (var r in synthetic) falseJumps += r.JumpsWhileRunning;
            Assert.AreEqual(0, falseJumps, "[합성] jumps while running in place");
        }

        // Writes the synthetic seeds when MONGSCAPE_WRITE_POSE_SEEDS=1; otherwise checks the files on disk match the generator.
        [Test] public void SyntheticSeedRecordingsCanBeRegenerated()
        {
            string folder = Path.Combine(Folder, "synthetic");
            bool write = Environment.GetEnvironmentVariable("MONGSCAPE_WRITE_POSE_SEEDS") == "1";
            foreach (var name in SyntheticPoseRecordings.Names)
            {
                SyntheticPoseRecordings.Build(name, out var recording, out var labels);
                string path = Path.Combine(folder, name + PoseRecordingFiles.GzipExtension), labelPath = Path.Combine(folder, name + PoseRecordingFiles.LabelsExtension);
                if (write) { Directory.CreateDirectory(folder); PoseRecordingFiles.Write(path, recording); PoseRecordingFiles.WriteLabels(labelPath, labels); continue; }
                var onDisk = PoseRecordingFiles.Read(path);
                Assert.IsTrue(onDisk.Synthetic, name + " is marked synthetic");
                Assert.AreEqual(recording.Frames.Count, onDisk.Frames.Count, name + " frames");
                Assert.AreEqual(labels.Count, PoseRecordingFiles.ReadLabels(labelPath).Count, name + " labels");
            }
        }
    }
}
