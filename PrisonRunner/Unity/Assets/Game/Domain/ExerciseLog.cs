using System;
using System.Collections.Generic;

namespace Muhanok.Domain
{
    // One player's exercise on one KST day: active seconds, moderate seconds (5 MET or more by the cadence stage), and the
    // active seconds per course (ExerciseKind). Owner is the member id, or "" for guests of this PC.
    [Serializable]
    public sealed class ExerciseDay
    {
        public string Owner = "", Day = "";
        public double Active, Moderate;
        public double[] Kinds = new double[4];
        // Streak coin bonus already credited for this day.
        public bool StreakPaid;
    }

    // Weekly goal, daily streak and course balance (exercise plan 8). Kept on this PC for now; every read and write goes
    // through this class (and PlayerProgressStore saves it), so an account-server column can back it later without
    // touching the screens.
    public sealed class ExerciseLog
    {
        // WHO: 150 minutes of moderate activity a week. A day of 10 active minutes keeps the streak.
        public const double WeeklyGoalMinutes = 150, StreakDayMinutes = 10;
        // Coins for each day that extends a streak of two days or more (members; guests collect no coins).
        public const int StreakCoins = 10;
        // Keep about two months of days per owner.
        public const int KeepDays = 62;
        private readonly List<ExerciseDay> days = new List<ExerciseDay>();
        public IReadOnlyList<ExerciseDay> Days => days;
        public static string DayKey(DateTime kstDate) => kstDate.ToString("yyyyMMdd");
        // KST calendar date of a UTC time.
        public static DateTime Kst(DateTime utc) => utc.AddHours(9).Date;
        public void Load(IEnumerable<ExerciseDay> saved)
        {
            days.Clear();
            if (saved != null) foreach (var d in saved) if (d != null && !string.IsNullOrEmpty(d.Day)) { d.Owner ??= ""; if (d.Kinds == null || d.Kinds.Length != 4) d.Kinds = new double[4]; days.Add(d); }
        }
        public ExerciseDay Find(string owner, string day) => days.Find(d => d.Owner == (owner ?? "") && d.Day == day);
        private ExerciseDay Get(string owner, string day)
        {
            var found = Find(owner, day);
            if (found != null) return found;
            found = new ExerciseDay { Owner = owner ?? "", Day = day }; days.Add(found); return found;
        }
        // Adds one run's time. Returns the streak bonus coins this run earns: the day reached 10 minutes for the first time
        // and yesterday was also a streak day (paid once a day).
        public int Add(string owner, DateTime utcNow, double active, double moderate, double[] kinds)
        {
            var today = Kst(utcNow); var day = Get(owner, DayKey(today));
            bool before = day.Active / 60 >= StreakDayMinutes;
            day.Active += Math.Max(0, active); day.Moderate += Math.Max(0, moderate);
            if (kinds != null) for (int i = 0; i < 4 && i < kinds.Length; i++) day.Kinds[i] += Math.Max(0, kinds[i]);
            Trim(owner, today);
            if (before || day.Active / 60 < StreakDayMinutes || day.StreakPaid || Streak(owner, utcNow) < 2) return 0;
            day.StreakPaid = true; return StreakCoins;
        }
        private void Trim(string owner, DateTime today)
        {
            string oldest = DayKey(today.AddDays(-KeepDays));
            days.RemoveAll(d => d.Owner == (owner ?? "") && string.CompareOrdinal(d.Day, oldest) < 0);
        }
        // Monday of the KST week holding the date.
        public static DateTime WeekStart(DateTime kstDate) => kstDate.AddDays(-(((int)kstDate.DayOfWeek + 6) % 7));
        // Moderate minutes from Monday to today.
        public double WeekModerateMinutes(string owner, DateTime utcNow)
        {
            var today = Kst(utcNow); double sum = 0;
            for (var d = WeekStart(today); d <= today; d = d.AddDays(1)) { var day = Find(owner, DayKey(d)); if (day != null) sum += day.Moderate; }
            return sum / 60;
        }
        public double WeekGoalProgress(string owner, DateTime utcNow) => Math.Min(1, WeekModerateMinutes(owner, utcNow) / WeeklyGoalMinutes);
        // Days in a row with 10 active minutes, ending today (or yesterday when today is not done yet).
        public int Streak(string owner, DateTime utcNow)
        {
            var day = Kst(utcNow);
            if (!Done(owner, day)) day = day.AddDays(-1);
            int n = 0; while (Done(owner, day)) { n++; day = day.AddDays(-1); }
            return n;
        }
        private bool Done(string owner, DateTime date) { var d = Find(owner, DayKey(date)); return d != null && d.Active / 60 >= StreakDayMinutes; }
        // The course done least this week (strength, cardio, balance), for the fork's "추천" tag.
        public ExerciseKind Recommend(string owner, DateTime utcNow)
        {
            var today = Kst(utcNow); var sums = new double[4];
            for (var d = WeekStart(today); d <= today; d = d.AddDays(1)) { var day = Find(owner, DayKey(d)); if (day != null) for (int i = 0; i < 4; i++) sums[i] += day.Kinds[i]; }
            var best = ExerciseKind.Strength;
            foreach (var kind in new[] { ExerciseKind.Cardio, ExerciseKind.Balance }) if (sums[(int)kind] < sums[(int)best]) best = kind;
            return best;
        }
    }
}
