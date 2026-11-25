using System;
using System.Collections.Generic;
using System.Globalization;
using Muhanok.Application;
using Muhanok.Domain;

namespace Muhanok.Presentation
{
    // The result screen's workout report without drawing: time by intensity (walk / run / sprint), moves by kind,
    // and this week against last week (members, from /stats). No grade or "perfect" ratio.
    public static class WorkoutReport
    {
        static readonly CultureInfo Num = CultureInfo.InvariantCulture;
        public const string EstimateNote = "칼로리는 추정값", GuestWeekNote = "회원가입하면 지난주와 비교할 수 있어요";
        public static readonly CadenceStage[] Stages = { CadenceStage.Walk, CadenceStage.Run, CadenceStage.Sprint };
        public static readonly MoveKind[] Kinds = { MoveKind.Step, MoveKind.Jump, MoveKind.Crouch, MoveKind.KneeStep, MoveKind.SideStep };
        public static string StageLabel(CadenceStage stage) => stage == CadenceStage.Walk ? "걷기" : stage == CadenceStage.Run ? "달리기" : stage == CadenceStage.Sprint ? "전력" : "서 있음";
        public static string MoveLabel(MoveKind kind) => kind == MoveKind.Step ? "걸음" : kind == MoveKind.Jump ? "점프" : kind == MoveKind.Crouch ? "숙이기" : kind == MoveKind.KneeStep ? "무릎 들기" : "옆걸음";
        public static string Clock(double seconds) { int s = (int)Math.Round(Math.Max(0, double.IsNaN(seconds) ? 0 : seconds)); return (s / 60).ToString(Num) + ":" + (s % 60).ToString("00", Num); }

        // With "달리기 속도 조절" off, stepping earns nothing: the report shows one row, the time of the motion moments.
        public const string MoveRowLabel = "동작", CadenceOffNote = "속도 조절 꺼짐 · 점프·숙이기·무릎·옆걸음만 칼로리";
        public static bool CadenceOff(WorkoutEstimate workout) => workout != null && !workout.CadenceCounts;
        public static string IntensityTitle(WorkoutEstimate workout) => CadenceOff(workout) ? "동작한 시간" : "강도별 시간";
        // (label, seconds, share of the walking + running + sprinting time); cadence off: the motion-moment time alone.
        public static List<(string label, double seconds, float share)> Intensity(WorkoutEstimate workout)
        {
            var rows = new List<(string, double, float)>();
            if (CadenceOff(workout)) { rows.Add((MoveRowLabel, workout.MoveSeconds, workout.MoveSeconds > 0 ? 1f : 0f)); return rows; }
            double total = 0; foreach (var s in Stages) total += workout?.StageSeconds(s) ?? 0;
            foreach (var s in Stages) { double v = workout?.StageSeconds(s) ?? 0; rows.Add((StageLabel(s), v, total > 0 ? (float)(v / total) : 0f)); }
            return rows;
        }
        public static List<KeyValuePair<string, string>> Moves(WorkoutEstimate workout)
        {
            var rows = new List<KeyValuePair<string, string>>();
            foreach (var k in Kinds) rows.Add(new KeyValuePair<string, string>(MoveLabel(k), (workout?.Moves(k) ?? 0).ToString("N0", Num) + "회"));
            return rows;
        }

        // This week against last week: lines (label, value) and a change note. Guests get the sign-up note; members wait for /stats.
        public static List<KeyValuePair<string, string>> Week(StatsReply stats)
        {
            var rows = new List<KeyValuePair<string, string>>();
            if (stats == null) return rows;
            rows.Add(new KeyValuePair<string, string>("이번 주", Kcal(stats.week_kcal) + " · " + Minutes(stats.week_seconds)));
            rows.Add(new KeyValuePair<string, string>("지난주", Kcal(stats.last_week_kcal) + " · " + Minutes(stats.last_week_seconds)));
            return rows;
        }
        // "하루 평균 12% 더", "하루 평균 5% 덜", "지난주 기록 없음" (by calories; short enough for the 162 px column). This week is only part done, so it is
        // compared as a daily average over its days so far (Monday counts 1 day) with last week's over 7 (QA 2026-11-25 L1).
        public static string WeekChange(StatsReply stats) => WeekChange(stats, DateTime.UtcNow);
        public static string WeekChange(StatsReply stats, DateTime utcNow)
        {
            if (stats == null) return "";
            if (stats.last_week_kcal <= 0) return stats.week_kcal > 0 ? "지난주 기록 없음 · 이번 주가 시작이에요" : "";
            var today = ExerciseLog.Kst(utcNow);
            int days = (today - ExerciseLog.WeekStart(today)).Days + 1;
            double now = stats.week_kcal / days, before = stats.last_week_kcal / 7;
            double change = (now - before) / before * 100;
            int pct = (int)Math.Round(Math.Abs(change));
            return pct == 0 ? "하루 평균 같아요" : "하루 평균 " + pct.ToString(Num) + "% " + (change > 0 ? "더" : "덜");
        }
        // The weekly goal counts moderate exercise only (5 MET or more: running in place, or the moments of a jump, crouch,
        // knee raise or side step); walking in place and the upper body's arm swings are lighter (QA 2026-11-25 L2).
        public const string WeekGoalTitle = "이번 주 중강도 운동", WeekGoalNote = "달리기·동작한 시간만 · 걷기와 상체는 빠져요";
        // "주간 중강도 37 / 150분" (moderate minutes this week) and "연속 3일" (days with 10 active minutes).
        public static string WeekGoal(double minutes) => "주간 중강도 " + WeekGoalValue(minutes);
        // A guest's result: this run's moderate minutes; a keyboard run's: why there is no bar.
        public static string RunModerate(double minutes) => "이번 판 중강도 " + ((int)Math.Floor(Math.Max(0, minutes))).ToString(Num) + "분";
        public const string KeyboardWeekNote = "주간 목표는 카메라 판만 세요";
        // The result summary's big movements (deep crouch, high jump, high knee, wide side step) in place of a combo.
        public const string BigMovesLabel = "크게 한 동작";
        public static string BigMoves(RunSession session)
        {
            int n = 0; if (session != null) foreach (int c in session.BigMoves) n += c;
            return n.ToString("N0", Num) + "회";
        }
        public static string WeekGoalValue(double minutes) => ((int)Math.Floor(Math.Max(0, minutes))).ToString(Num) + " / " + ExerciseLog.WeeklyGoalMinutes.ToString("0", Num) + "분";
        public static string StreakLine(int days, int coins) => days <= 0 ? "" : "연속 " + days.ToString(Num) + "일" + (coins > 0 ? " · 코인 +" + coins.ToString(Num) : "");
        static string Kcal(double v) => Math.Max(0, v).ToString(v >= 100 ? "N0" : "F1", Num) + "kcal";
        static string Minutes(double seconds) => ((int)Math.Round(Math.Max(0, seconds) / 60)).ToString("N0", Num) + "분";
    }
}
