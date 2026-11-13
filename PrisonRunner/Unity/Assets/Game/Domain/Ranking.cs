using System;
using System.Collections.Generic;

namespace Muhanok.Domain
{
    public enum PlayStyle { FullBody, UpperBody, Keyboard }

    // The player on this PC. In developer (exhibition) mode each run gets its own name instead.
    [Serializable]
    public sealed class PlayerProfile
    {
        public string Name = "";
        public string Weight = "";
        public double CalorieGoal = 50;
        public PlayStyle Style = PlayStyle.FullBody;
        public bool HasName => !string.IsNullOrWhiteSpace(Name);
        public string DisplayName => HasName ? Name.Trim() : "플레이어";
        public PlayerProfile Clone() => (PlayerProfile)MemberwiseClone();
    }

    public static class PlayStyleInfo
    {
        public static string Label(PlayStyle style) => style == PlayStyle.FullBody ? "전신" : style == PlayStyle.UpperBody ? "상체" : "키보드";
        // 2024 Adult Compendium: 22240 total-body active game 4 MET; upper-body active game about 2.5 MET; seated play 1.3 MET.
        public static double Met(PlayStyle style) => style == PlayStyle.FullBody ? 4 : style == PlayStyle.UpperBody ? 2.5 : 1.3;
        public static bool UsesCamera(PlayStyle style) => style != PlayStyle.Keyboard;
        // Without a connected camera only the keyboard can be played. Developer mode keeps every style for testing.
        public static bool Allowed(PlayStyle style, bool cameraConnected, bool developer) => developer || cameraConnected || !UsesCamera(style);
        public static PlayStyle Playable(PlayStyle style, bool cameraConnected, bool developer) => Allowed(style, cameraConnected, developer) ? style : PlayStyle.Keyboard;
        // PlayStyle value of a saved label; -1 when the label is missing or unknown.
        public static int ModeOf(string label) => label == "전신" ? 0 : label == "상체" ? 1 : label == "키보드" ? 2 : -1;
    }

    [Serializable]
    public sealed class RankingEntry
    {
        public string Name; public int Score; public float Distance; public int Coins; public string Style; public long Ticks;
        // Member id on the account server; empty for guest runs.
        public string AccountId = "";
        // RunScore.Version of Score. Records saved before the field existed load as 0 and become 1.
        public int ScoreVersion;
        // PlayStyle value; records saved without it take it from the Style label, or stay -1 (left out of the mode tabs).
        public int Mode = -1;
        // Calories of the run (0 for keyboard play).
        public double Kcal;
        public bool Ranked => ScoreVersion == RunScore.Version;
        public bool Member => !string.IsNullOrEmpty(AccountId);
        public string Day => Leaderboard.DayKey(new DateTime(Ticks));
    }

    // Score ranking of the runs on this PC, one board per play style; "today" is the same list filtered by day.
    // Older-formula records stay saved but are not ranked, since their scores are not comparable.
    public sealed class Leaderboard
    {
        // Cap per play style.
        public const int MaxEntries = 500;
        private readonly List<RankingEntry> entries = new List<RankingEntry>();
        public IReadOnlyList<RankingEntry> All => entries;
        public static string DayKey(DateTime time) => time.ToString("yyyyMMdd");
        public List<RankingEntry> Day(string day) => entries.FindAll(e => e.Day == day);
        // Ranked runs of one play style, best first.
        public List<RankingEntry> View(bool todayOnly, DateTime now, PlayStyle mode)
        {
            string day = DayKey(now);
            return entries.FindAll(e => e.Ranked && e.Mode == (int)mode && (!todayOnly || e.Day == day));
        }
        // Adds a run and returns its 1-based rank among the ranked runs of its play style.
        public int Add(RankingEntry entry)
        {
            Normalize(entry); entries.Add(entry); Sort(); Trim();
            return entry.Mode < 0 ? 0 : RankOf(entry, false, (PlayStyle)entry.Mode);
        }
        // 1-based rank within one play style, overall or within the entry's own day; 0 when it is not ranked there.
        public int RankOf(RankingEntry entry, bool sameDay, PlayStyle mode)
        {
            if (entry == null || !entry.Ranked || entry.Mode != (int)mode || !entries.Contains(entry)) return 0;
            return View(sameDay, new DateTime(entry.Ticks), mode).IndexOf(entry) + 1;
        }
        public bool Contains(RankingEntry entry) => entry != null && entries.Contains(entry);
        public void Load(IEnumerable<RankingEntry> saved)
        {
            entries.Clear();
            if(saved!=null)foreach(var entry in saved)if(entry!=null){Normalize(entry);entries.Add(entry);}
            Sort(); Trim();
        }
        // Best ranked score for one play style (and one name when given); 0 when there is none.
        public int BestScore(string style, string name = null)
        {
            int best = 0;
            foreach (var e in entries) if (e.Ranked && e.Style == style && (name == null || e.Name == name)) best = Math.Max(best, e.Score);
            return best;
        }
        public void ClearDay(string day) => entries.RemoveAll(e => e.Day == day);
        // A deleted member's runs leave this PC's board too.
        public void RemoveAccount(string id) { if (!string.IsNullOrEmpty(id)) entries.RemoveAll(e => e.AccountId == id); }
        // Older records: version 1 without a version, the mode from the Style label ("전신"/"상체"/"키보드") or -1.
        private static void Normalize(RankingEntry entry)
        {
            if (entry.ScoreVersion <= 0) entry.ScoreVersion = 1;
            if (entry.Mode < 0 || entry.Mode > 2) entry.Mode = PlayStyleInfo.ModeOf(entry.Style);
            if (entry.AccountId == null) entry.AccountId = "";
        }
        // Ranked runs first by score, then unranked older records in their old distance order; ties go to the earlier run.
        private void Sort() => entries.Sort((a, b) => a.Ranked != b.Ranked ? (a.Ranked ? -1 : 1)
            : a.Ranked && b.Score != a.Score ? b.Score.CompareTo(a.Score)
            : !a.Ranked && b.Distance != a.Distance ? b.Distance.CompareTo(a.Distance) : a.Ticks.CompareTo(b.Ticks));
        // The cap applies to the ranked runs of each play style (sorted best first); older and unclassified records are kept.
        private void Trim()
        {
            var kept = new int[3];
            entries.RemoveAll(e => e.Ranked && e.Mode >= 0 && e.Mode <= 2 && ++kept[e.Mode] > MaxEntries);
        }
    }

    // Lifetime totals of this PC, shown only as developer-mode statistics.
    [Serializable]
    public sealed class PlayerStats
    {
        public int Runs;
        public float BestDistance, TotalDistance;
        public double TotalKcal;
        public void Add(float distance, double kcal)
        {
            Runs++; TotalDistance += Math.Max(0f, distance); TotalKcal += Math.Max(0, kcal);
            BestDistance = Math.Max(BestDistance, distance);
        }
    }
}
