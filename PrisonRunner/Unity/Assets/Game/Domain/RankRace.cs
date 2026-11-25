using System;
using System.Collections.Generic;

namespace Muhanok.Domain
{
    // The ranking strip of a run: other players' scores loaded once at the start (same play style), the live score from the HUD.
    // Shows the two rows above the player, the player, and one row below, with the points to the next row; passing rows
    // queues one overtake per row passed (the view plays them one after another). Ties stay above the player.
    public sealed class RankRace
    {
        public readonly struct Row
        {
            public readonly string Name; public readonly int Score;
            public Row(string name, int score) { Name = name ?? ""; Score = score; }
        }
        public const int Above = 2, Below = 1;
        // Within this many points of the row above, the view nudges toward it.
        public const int CloseGap = 50;
        // An empty board gets practice rows so a first player still has someone to pass.
        public static readonly int[] PracticeScores = { 300, 700, 1200, 2000, 3000, 4500 };
        public const string PracticeName = "연습 기록";
        private readonly List<Row> others = new List<Row>();
        private readonly Queue<Row> passed = new Queue<Row>();
        public IReadOnlyList<Row> Others => others;
        public int Score { get; private set; }
        public int Rank { get; private set; } = 1;
        public bool Loaded { get; private set; }
        public bool Practice { get; private set; }
        public int Pending => passed.Count;
        // Best first; with no rows at all the practice rows stand in.
        public void Load(IEnumerable<Row> rows)
        {
            others.Clear(); passed.Clear(); Practice = false;
            if (rows != null) foreach (var r in rows) if (r.Score > 0) others.Add(r);
            if (others.Count == 0) { Practice = true; foreach (int s in PracticeScores) others.Add(new Row(PracticeName, s)); }
            others.Sort((a, b) => b.Score.CompareTo(a.Score));
            // A reload mid-run (the server answered late) keeps the live score and passes nobody.
            Rank = RankOf(Score); Loaded = true;
        }
        // This PC's rows for the strip from ranked entries (best first): runs since `since` that are not a member's (members come
        // from the server, one row each), one row per name with its best score, so one guest never fills the strip twice.
        // onlyMine: the member's own runs instead, each labelled "내 기록" (QA 2026-11-25 M4).
        public const string MineName = "내 기록";
        public static List<Row> LocalRows(IEnumerable<RankingEntry> entries, DateTime since, bool onlyMine, string memberId)
        {
            var rows = new List<Row>();
            var best = new Dictionary<string, int>();
            var order = new List<string>();
            if (entries == null) return rows;
            foreach (var e in entries)
            {
                if (e == null || new DateTime(e.Ticks) < since) continue;
                if (onlyMine) { if (e.Member && e.AccountId == memberId) rows.Add(new Row(MineName, e.Score)); continue; }
                if (e.Member) continue;
                string name = e.Name ?? "";
                if (!best.TryGetValue(name, out int score)) { order.Add(name); best[name] = e.Score; }
                else if (e.Score > score) best[name] = e.Score;
            }
            foreach (var name in order) rows.Add(new Row(name, best[name]));
            return rows;
        }
        public void Clear() { others.Clear(); passed.Clear(); Loaded = false; Score = 0; Rank = 1; Practice = false; }
        // 1-based rank of a score: every other row at or above it stays ahead.
        public int RankOf(int score) { int above = 0; foreach (var r in others) if (r.Score >= score) above++; return above + 1; }
        public void Update(int score)
        {
            if (!Loaded) return;
            int rank = RankOf(score);
            // Rows passed since the last update, nearest first (the one just above the old place, then upward).
            for (int i = Rank - 2; i >= rank - 1 && i >= 0; i--) passed.Enqueue(others[i]);
            Score = Math.Max(0, score); Rank = rank;
        }
        public bool TakeOvertake(out Row row) { if (passed.Count > 0) { row = passed.Dequeue(); return true; } row = default; return false; }
        public bool First => Loaded && Rank == 1;
        // Rows to draw around the player: indexes into Others (-1 for the player), top to bottom.
        public List<int> Window()
        {
            var list = new List<int>(Above + Below + 1);
            int me = Rank - 1; // others[me - 1] is just above, others[me] just below
            for (int i = Math.Max(0, me - Above); i < me; i++) list.Add(i);
            list.Add(-1);
            for (int i = me; i < Math.Min(others.Count, me + Below); i++) list.Add(i);
            return list;
        }
        // Points to pass the row just above; 0 when first.
        public int ToNext => Rank <= 1 ? 0 : Math.Max(1, others[Rank - 2].Score - Score + 1);
        public bool Close => Rank > 1 && ToNext <= CloseGap;
    }
}
