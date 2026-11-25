using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Muhanok.Domain;

namespace Muhanok.Infrastructure
{
    // Pose recordings on disk: <name>.pose.csv or <name>.pose.csv.gz, labels in <name>.labels.csv next to it.
    // The shared folder for the scorecard is Tools/QA/PoseRecordings (docs/POSE_RECORDINGS.md).
    public static class PoseRecordingFiles
    {
        public const string PlainExtension = ".pose.csv", GzipExtension = ".pose.csv.gz", LabelsExtension = ".labels.csv";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public sealed class Entry
        {
            public string Name, Path, LabelsPath;
            public PoseRecording Recording;
            public List<PoseLabel> Labels;
        }

        public static string NameOf(string path)
        {
            string file = System.IO.Path.GetFileName(path);
            if (file.EndsWith(GzipExtension, StringComparison.OrdinalIgnoreCase)) return file.Substring(0, file.Length - GzipExtension.Length);
            if (file.EndsWith(PlainExtension, StringComparison.OrdinalIgnoreCase)) return file.Substring(0, file.Length - PlainExtension.Length);
            return System.IO.Path.GetFileNameWithoutExtension(file);
        }

        public static PoseRecording Read(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new StreamReader(path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? (Stream)new GZipStream(stream, CompressionMode.Decompress) : stream, Utf8))
                return PoseRecording.Read(reader);
        }

        public static List<PoseLabel> ReadLabels(string path)
        {
            using (var reader = new StreamReader(path, Utf8)) return PoseLabels.Read(reader);
        }

        public static void Write(string path, PoseRecording recording)
        {
            using (var stream = File.Create(path))
            using (var writer = new StreamWriter(path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? (Stream)new GZipStream(stream, CompressionLevel.Optimal) : stream, Utf8) { NewLine = "\n" })
                recording.Write(writer);
        }

        public static void WriteLabels(string path, IEnumerable<PoseLabel> labels)
        {
            using (var writer = new StreamWriter(path, false, Utf8) { NewLine = "\n" }) PoseLabels.Write(writer, labels);
        }

        // Every recording under the folder (subfolders too) that has a labels file; recordings without labels are listed in skipped.
        public static List<Entry> LoadFolder(string folder, List<string> skipped = null)
        {
            var entries = new List<Entry>();
            if (!Directory.Exists(folder)) return entries;
            var files = new List<string>(Directory.GetFiles(folder, "*" + PlainExtension, SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(folder, "*" + GzipExtension, SearchOption.AllDirectories));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var path in files)
            {
                string name = NameOf(path), labels = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), name + LabelsExtension);
                if (!File.Exists(labels)) { skipped?.Add(path); continue; }
                entries.Add(new Entry { Name = name, Path = path, LabelsPath = labels, Recording = Read(path), Labels = ReadLabels(labels) });
            }
            return entries;
        }

        public static List<PoseScorecard.Result> Score(IEnumerable<Entry> entries, MotionTuning tuning = null)
        {
            var results = new List<PoseScorecard.Result>();
            foreach (var entry in entries) results.Add(PoseScorecard.Score(entry.Name, entry.Recording, entry.Labels, tuning));
            return results;
        }
    }

    // Development tool (developer mode, F6): writes the raw landmark frames the camera produces to a recording file.
    // Only joint coordinates are written, never camera images. The file is plain text while recording and gzipped on Stop,
    // with an empty labels file next to it to fill in by hand.
    public sealed class PoseRecorder : IDisposable
    {
        private StreamWriter writer;
        private string plainPath;
        private double startTime = -1;
        public bool Recording => writer != null;
        public int Frames { get; private set; }
        public string LastPath { get; private set; }

        public void Start(string folder, IEnumerable<KeyValuePair<string, string>> meta, string fileName)
        {
            Stop();
            Directory.CreateDirectory(folder);
            plainPath = Path.Combine(folder, fileName + PoseRecordingFiles.PlainExtension);
            writer = new StreamWriter(plainPath, false, new UTF8Encoding(false)) { NewLine = "\n" };
            PoseRecording.WriteMeta(writer, meta);
            startTime = -1; Frames = 0;
        }

        // Frames before smoothing, as the estimator delivered them; times count from the first frame.
        public void Write(PoseFrame frame)
        {
            if (writer == null) return;
            if (startTime < 0) startTime = frame.Time;
            PoseRecording.WriteFrame(writer, frame, frame.Time - startTime);
            Frames++;
        }

        // Closes the file, gzips it and returns the recording path (null when nothing was recording).
        public string Stop()
        {
            if (writer == null) return null;
            writer.Dispose(); writer = null;
            string gz = plainPath + ".gz";
            using (var input = File.OpenRead(plainPath))
            using (var output = File.Create(gz))
            using (var zip = new GZipStream(output, CompressionLevel.Optimal)) input.CopyTo(zip);
            File.Delete(plainPath);
            string labels = Path.Combine(Path.GetDirectoryName(gz), PoseRecordingFiles.NameOf(gz) + PoseRecordingFiles.LabelsExtension);
            if (!File.Exists(labels))
                File.WriteAllText(labels, "# action,start,end[,count]\n# actions: stand, walk, run, sprint, jump, crouch, knee, side_left, side_right; context: stairs, cart\n# times in seconds from the start of the recording (docs/POSE_RECORDINGS.md)\n", new UTF8Encoding(false));
            LastPath = gz;
            return gz;
        }

        public void Dispose() { try { Stop(); } catch (IOException) { } }
    }
}
