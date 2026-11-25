using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Muhanok.Domain
{
    // A pose recording: the raw landmark frames the camera produced (before smoothing), never any image.
    // Text format (docs/POSE_RECORDINGS.md):
    //   # mongscape-pose 1
    //   # key=value            metadata: source, style, camera, device, model, created, person, note
    //   t,has,aspect,scale,x0,y0,z0,v0,...,x32,y32,z32,v32
    //   0.0333,1,1.3333,0.52,0.5012,...
    // "has" 0 rows carry only the first four columns (no body in the frame).
    public sealed class PoseRecording
    {
        public const string Magic = "# mongscape-pose 1";
        public const string SourceWebcam = "webcam", SourceSynthetic = "synthetic";
        public readonly Dictionary<string, string> Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<PoseFrame> Frames = new List<PoseFrame>();

        public string Source => Get("source", SourceWebcam);
        public bool Synthetic => string.Equals(Source, SourceSynthetic, StringComparison.OrdinalIgnoreCase);
        public PlayStyle Style => Enum.TryParse(Get("style", "FullBody"), true, out PlayStyle style) ? style : PlayStyle.FullBody;
        public double Duration => Frames.Count == 0 ? 0 : Frames[Frames.Count - 1].Time - Frames[0].Time;
        public string Get(string key, string fallback = "") => Meta.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : fallback;

        public static string Header()
        {
            var columns = new StringBuilder("t,has,aspect,scale");
            for (int i = 0; i < PoseFrame.Count; i++) columns.Append(",x").Append(i).Append(",y").Append(i).Append(",z").Append(i).Append(",v").Append(i);
            return columns.ToString();
        }

        public static void WriteMeta(TextWriter writer, IEnumerable<KeyValuePair<string, string>> meta)
        {
            writer.WriteLine(Magic);
            foreach (var pair in meta) writer.WriteLine("# " + pair.Key + "=" + Clean(pair.Value));
            writer.WriteLine(Header());
        }

        public static void WriteFrame(TextWriter writer, PoseFrame frame, double time)
        {
            var line = new StringBuilder(2400);
            line.Append(F(time, "0.####")).Append(',').Append(frame.HasPose ? '1' : '0').Append(',').Append(F(frame.Aspect, "0.####")).Append(',').Append(F(frame.ObjectScale, "0.####"));
            if (frame.HasPose)
                foreach (var p in frame.Points)
                    line.Append(',').Append(F(p.X, "0.####")).Append(',').Append(F(p.Y, "0.####")).Append(',').Append(F(p.Z, "0.###")).Append(',').Append(F(p.Visibility, "0.###"));
            writer.WriteLine(line.ToString());
        }

        public void Write(TextWriter writer)
        {
            WriteMeta(writer, Meta);
            foreach (var frame in Frames) WriteFrame(writer, frame, frame.Time);
        }

        public static PoseRecording Read(TextReader reader)
        {
            var recording = new PoseRecording();
            string line; int number = 0; bool magic = false;
            while ((line = reader.ReadLine()) != null)
            {
                number++;
                line = line.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    if (line == Magic) { magic = true; continue; }
                    int eq = line.IndexOf('=');
                    if (eq > 1) recording.Meta[line.Substring(1, eq - 1).Trim()] = line.Substring(eq + 1).Trim();
                    continue;
                }
                if (line.StartsWith("t,", StringComparison.Ordinal)) continue;
                var cells = line.Split(',');
                if (cells.Length < 4) throw new FormatException("Line " + number + ": needs t,has,aspect,scale");
                var frame = new PoseFrame { Time = P(cells[0]), HasPose = cells[1] == "1", Aspect = P(cells[2]), ObjectScale = P(cells[3]) };
                if (frame.HasPose)
                {
                    if (cells.Length < 4 + PoseFrame.Count * 4) throw new FormatException("Line " + number + ": needs 33 joints of x,y,z,v");
                    for (int i = 0; i < PoseFrame.Count; i++)
                    {
                        int c = 4 + i * 4;
                        frame.Points[i] = new PoseLandmark { X = P(cells[c]), Y = P(cells[c + 1]), Z = P(cells[c + 2]), Visibility = P(cells[c + 3]) };
                    }
                }
                recording.Frames.Add(frame);
            }
            if (!magic) throw new FormatException("Not a Mongscape pose recording (first line must be '" + Magic + "')");
            return recording;
        }

        private static string Clean(string value) => (value ?? "").Replace('\r', ' ').Replace('\n', ' ');
        private static string F(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
        private static float P(string value) => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    // What the player was doing when, written by hand (or by the synthetic generator) next to a recording.
    //   # action,start,end[,count]
    //   run,2.0,30.0
    //   jump,35.1,35.7
    //   knee,40,50,12        (12 knee lifts expected in the window)
    //   stairs,40,50         (context: the game would be on a stair chunk then; also "cart")
    public enum PoseAction { Stand, Walk, Run, Sprint, Jump, Crouch, Knee, SideLeft, SideRight, Stairs, Cart }

    public sealed class PoseLabel
    {
        public PoseAction Action;
        public double Start, End;
        public int Count = 1;
        public bool Contains(double t, double margin = 0) => t >= Start - margin && t <= End + margin;
    }

    public static class PoseLabels
    {
        public static readonly Dictionary<string, PoseAction> Names = new Dictionary<string, PoseAction>(StringComparer.OrdinalIgnoreCase)
        {
            ["stand"] = PoseAction.Stand, ["walk"] = PoseAction.Walk, ["run"] = PoseAction.Run, ["sprint"] = PoseAction.Sprint,
            ["jump"] = PoseAction.Jump, ["crouch"] = PoseAction.Crouch, ["knee"] = PoseAction.Knee,
            ["side_left"] = PoseAction.SideLeft, ["side_right"] = PoseAction.SideRight, ["stairs"] = PoseAction.Stairs, ["cart"] = PoseAction.Cart,
        };
        public static string Name(PoseAction action) { foreach (var pair in Names) if (pair.Value == action) return pair.Key; return action.ToString(); }

        public static List<PoseLabel> Read(TextReader reader)
        {
            var labels = new List<PoseLabel>(); string line; int number = 0;
            while ((line = reader.ReadLine()) != null)
            {
                number++;
                line = line.Trim();
                if (line.Length == 0 || line[0] == '#' || line.StartsWith("action,", StringComparison.OrdinalIgnoreCase)) continue;
                var cells = line.Split(',');
                if (cells.Length < 3 || !Names.TryGetValue(cells[0].Trim(), out var action)) throw new FormatException("Label line " + number + ": action,start,end[,count] with a known action");
                var label = new PoseLabel { Action = action, Start = double.Parse(cells[1], CultureInfo.InvariantCulture), End = double.Parse(cells[2], CultureInfo.InvariantCulture) };
                if (cells.Length > 3 && cells[3].Trim().Length > 0) label.Count = Math.Max(1, int.Parse(cells[3], CultureInfo.InvariantCulture));
                if (label.End < label.Start) throw new FormatException("Label line " + number + ": end before start");
                labels.Add(label);
            }
            return labels;
        }

        public static void Write(TextWriter writer, IEnumerable<PoseLabel> labels)
        {
            writer.WriteLine("# action,start,end[,count]");
            foreach (var label in labels)
                writer.WriteLine(Name(label.Action) + "," + label.Start.ToString("0.###", CultureInfo.InvariantCulture) + "," + label.End.ToString("0.###", CultureInfo.InvariantCulture)
                    + (label.Count != 1 ? "," + label.Count : ""));
        }
    }
}
