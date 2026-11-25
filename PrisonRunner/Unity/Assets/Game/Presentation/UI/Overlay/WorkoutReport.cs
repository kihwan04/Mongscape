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

        // (label, seconds, share of the walking + running + sprinting time).
        public static List<(string label, double seconds, float share)> Intensity(WorkoutEstimate workout)
        {
            var rows = new List<(string, double, float)>();
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
        // "지난주보다 12% 더", "지난주보다 5% 덜", "지난주 기록 없음" (by calories).
        public static string WeekChange(StatsReply stats)
        {
            if (stats == null) return "";
            if (stats.last_week_kcal <= 0) return stats.week_kcal > 0 ? "지난주 기록 없음 · 이번 주가 시작이에요" : "";
            double change = (stats.week_kcal - stats.last_week_kcal) / stats.last_week_kcal * 100;
            int pct = (int)Math.Round(Math.Abs(change));
            return pct == 0 ? "지난주와 같아요" : "지난주보다 " + pct.ToString(Num) + "% " + (change > 0 ? "더" : "덜");
        }
        static string Kcal(double v) => Math.Max(0, v).ToString(v >= 100 ? "N0" : "F1", Num) + "kcal";
        static string Minutes(double seconds) => ((int)Math.Round(Math.Max(0, seconds) / 60)).ToString("N0", Num) + "분";
    }
}
