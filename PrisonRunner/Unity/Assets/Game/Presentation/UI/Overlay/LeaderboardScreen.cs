using System;
using System.Collections.Generic;
using System.Globalization;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Score ranking with [회원 랭킹 | 이 PC] × [전신 | 상체 | 키보드] × [전체 | 오늘] tabs (state in RankingBoard).
    // After a run it also shows the score, the member's best, the run summary and the calorie panel, and highlights the player's row.
    internal sealed class LeaderboardScreen
    {
        static readonly CultureInfo Num = CultureInfo.InvariantCulture;
        const string NoFoodText = "소모한 칼로리가 없어요";
        // card slots left to right: 2nd, 1st, 3rd
        static readonly int[] SlotRank = { 1, 0, 2 };
        static readonly float[] SlotHeight = { 138, 158, 128 };
        static readonly string[] SourceTabs = { "회원 랭킹", "통합 랭킹" }, ModeTabs = { "전신", "상체", "키보드" }, PeriodTabs = { "전체", "오늘", "도전" };

        readonly OverlaySkin skin;
        readonly RankingBoard board;
        readonly List<KeyValuePair<string, string>> summary = new List<KeyValuePair<string, string>>();
        GUIStyle note;

        readonly GuestNameRow guestName;
        public LeaderboardScreen(OverlaySkin overlaySkin, RankingBoard ranking) { skin = overlaySkin; board = ranking; guestName = new GuestNameRow(overlaySkin, ranking.RefreshLocal); }
        public bool AsksGuestName => board.ResultMode && guestName.Open;
        public bool SaveGuestName(IHudData data, string name) => board.ResultMode && guestName.Save(data, name);

        public void OpenBoard(IHudData data, IIdentityData account)
        {
            guestName.Skip();
            board.OpenBoard(data, account);
        }

        // After a run: member → 회원 랭킹, guest → 이 PC; the mode just played and today's tab.
        public void OpenResult(IHudData data, IIdentityData account, RunSession session)
        {
            summary.Clear();
            summary.Add(new KeyValuePair<string, string>("시간", RunHudModel.Clock(session.Elapsed)));
            summary.Add(new KeyValuePair<string, string>("거리", Dist(session.Score.Distance)));
            summary.Add(new KeyValuePair<string, string>("코인", "+" + session.Score.Coins.ToString(Num)));
            // No combos in the exercise patch: a camera run shows its big movements instead (QA 2026-11-25 L11).
            bool camera = PlayStyleInfo.UsesCamera(data?.Player?.Style ?? PlayStyle.Keyboard);
            if (camera) summary.Add(new KeyValuePair<string, string>(WorkoutReport.BigMovesLabel, WorkoutReport.BigMoves(session)));
            guestName.Show(data);
            board.OpenResult(data, account);
        }

        static string Dist(float m) { return Mathf.FloorToInt(m).ToString("N0", Num) + " m"; }

        // primary/secondary: result buttons (다시 하기 / 메인으로); the menu board only shows a close button.
        // middle: an optional third result button between them (웹캠 보정).
        public void Draw(IHudData data, RunSession session, Action primary, string primaryLabel, Action secondary, string secondaryLabel, Action middle = null, string middleLabel = null)
        {
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Ground, 0.9f));
            GUI.matrix = oldMatrix;

            float x = 230, w = 820;
            if (board.ResultMode)
            {
                // Title number is the run score; next to it the earlier best and 신기록! when this run beat it.
                skin.Text(new Rect(x, 30, 260, 52), RunHudModel.Points(session.Score.Score), 44, Palette.Cream, TextAnchor.MiddleLeft);
                string best = board.BestLine;
                if (best.Length > 0) skin.Text(new Rect(x + 262, 30, 400, 52), best == RankingBoard.GuestBestNote ? best : "(" + best + ")", 18, Palette.Sand, TextAnchor.MiddleLeft, false, true);
                if (board.NewBest)
                {
                    var badge = new Rect(x + w - 150, 36, 150, 42);
                    skin.BorderBox(badge, Palette.Gold, Palette.Cream, 2);
                    skin.Text(badge, RankingBoard.NewBestText, 22, Palette.Outline, TextAnchor.MiddleCenter);
                }
                // Run facts as label/value pairs in fixed columns.
                for (int i = 0; i < summary.Count; i++)
                {
                    float sx = x + i * 150;
                    float labelWidth = skin.Style(14, false, TextAnchor.MiddleLeft).CalcSize(new GUIContent(summary[i].Key)).x;
                    skin.Text(new Rect(sx, 78, labelWidth + 4, 22), summary[i].Key, 14, Palette.Sand, TextAnchor.MiddleLeft, false);
                    skin.Text(new Rect(sx + labelWidth + 8, 78, 90, 22), summary[i].Value, 15, Palette.Cream, TextAnchor.MiddleLeft, false, true);
                }
            }
            else skin.Text(new Rect(x, 36, 300, 48), "랭킹", 44, Palette.Cream, TextAnchor.MiddleLeft);
            DrawTabs(x, w);
            // 통합 랭킹 without the server: a small note in the gap between the mode and period tabs.
            if (!board.MemberTab && board.MembersMissing)
                skin.Text(new Rect(x + 512, 104, w - 760, 44), RankingBoard.MembersMissingText, 13, Palette.Sand, TextAnchor.MiddleCenter, false, true);
            if (board.Status != RankingStatus.Ready)
            {
                skin.Text(new Rect(x, 200, w, 60), board.StatusText, 20, Palette.Sand, TextAnchor.MiddleCenter, false, true);
                if (board.Status == RankingStatus.Failed && skin.Button(new Rect(x + w / 2 - 90, 266, 180, 48), "다시 시도", Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 20)) board.Retry();
            }
            else
            {
                DrawTop3(x, w);
                DrawRows(x, w);
            }
            DrawMine(data, x, w);
            if (board.ResultMode) { DrawWorkout(session, data); DrawKcal(session); DrawWeek(data); }
            if (board.ResultMode) guestName.Draw(data, new Rect(x + 110, 300, w - 220, 180));

            if (secondary == null)
            {
                if (skin.Button(new Rect(540, 606, 200, 56), primaryLabel, Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 22)) primary?.Invoke();
                return;
            }
            float left = middle != null ? 328 : 434, right = middle != null ? 752 : 646;
            if (skin.Button(new Rect(left, 606, 200, 56), primaryLabel, Palette.Gold, Color.clear, Palette.Outline, 22)) primary?.Invoke();
            if (middle != null && skin.Button(new Rect(540, 606, 200, 56), middleLabel, Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 22)) middle.Invoke();
            if (skin.Button(new Rect(right, 606, 200, 56), secondaryLabel, Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 22)) secondary.Invoke();
        }

        // Left panel: the workout report. Time by intensity as bars, moves by kind, then the goal and the foods.
        void DrawWorkout(RunSession session, IHudData data)
        {
            skin.BorderBox(new Rect(24,140,190,450),Palette.A(Palette.Panel,.95f),Palette.Wood,2);
            skin.Text(new Rect(38,150,164,30),"운동 기록",22,Palette.Gold,TextAnchor.MiddleLeft);
            if (!PlayStyleInfo.UsesCamera(data?.Player?.Style ?? PlayStyle.Keyboard))
            {
                skin.Text(new Rect(38,190,164,26),"최고 콤보 "+session.BestDodgeStreak,16,Palette.Cream,TextAnchor.MiddleLeft,false,true);
                skin.Text(new Rect(38,225,164,30),RunHudModel.KeyboardKcal,15,Palette.Sand,TextAnchor.MiddleLeft,false,true);
                return;
            }
            var workout = session.Workout;
            skin.Text(new Rect(38,182,164,20),WorkoutReport.IntensityTitle(workout),13,Palette.Sand,TextAnchor.MiddleLeft,false,true);
            var bars = WorkoutReport.Intensity(workout);
            Color[] tint = { Palette.Sand, Palette.Gold, new Color(1f,.55f,.25f) };
            for (int i = 0; i < bars.Count; i++)
            {
                float y = 204 + i * 28;
                skin.Text(new Rect(38,y,52,22),bars[i].label,14,Palette.Cream,TextAnchor.MiddleLeft,false,true);
                var track = new Rect(92,y+7,62,9);
                skin.Box(track,Palette.A(Palette.Ground,.9f));
                if (bars[i].share > 0) skin.Box(new Rect(track.x,track.y,Mathf.Max(3,track.width*bars[i].share),track.height),tint[i]);
                skin.Text(new Rect(156,y,48,22),WorkoutReport.Clock(bars[i].seconds),14,Palette.Cream,TextAnchor.MiddleRight,false,true);
            }
            if (WorkoutReport.CadenceOff(workout))
            {
                note ??= new GUIStyle(skin.Style(14, false, TextAnchor.UpperLeft)) { wordWrap = true, clipping = TextClipping.Clip };
                var old = GUI.color; GUI.color = Palette.Sand;
                GUI.Label(new Rect(38,234,164,52),WorkoutReport.CadenceOffNote,note); GUI.color = old;
            }
            skin.Text(new Rect(38,292,164,20),"동작 횟수",13,Palette.Sand,TextAnchor.MiddleLeft,false,true);
            var moves = WorkoutReport.Moves(workout);
            for (int i = 0; i < moves.Count; i++)
            {
                float y = 312 + i * 21;
                skin.Text(new Rect(38,y,90,20),moves[i].Key,14,Palette.Sand,TextAnchor.MiddleLeft,false,true);
                skin.Text(new Rect(118,y,86,20),moves[i].Value,14,Palette.Cream,TextAnchor.MiddleRight,false,true);
            }
            skin.Text(new Rect(38,424,164,22),"목표 "+workout.GoalKcal.ToString("F0",Num)+" kcal",14,Palette.Sand,TextAnchor.MiddleLeft,false,true);
            // This run's kcal as familiar foods.
            var foods=FoodEquivalent.For(workout.EstimatedKcal);
            if(foods.Count==0){skin.Text(new Rect(38,450,164,22),NoFoodText,14,Palette.Sand,TextAnchor.MiddleLeft,false);return;}
            for(int i=0;i<foods.Count&&i<3;i++)
            {
                skin.Text(new Rect(38,450+i*44,164,20),foods[i].Food.Name,13,Palette.Sand,TextAnchor.MiddleLeft,false);
                skin.Text(new Rect(38,469+i*44,164,22),foods[i].CountText,15,Palette.Cream,TextAnchor.MiddleLeft,false,true);
            }
        }

        // Right, below the calories: this week against last week for a member; a guest sees the sign-up note.
        void DrawWeek(IHudData data)
        {
            var panel = new Rect(1066, 392, 190, 198);
            skin.BorderBox(panel, Palette.A(Palette.Panel, .95f), Palette.Wood, 2);
            skin.Text(new Rect(panel.x + 14, panel.y + 10, panel.width - 28, 28), "지난주와 비교", 19, Palette.Gold, TextAnchor.MiddleLeft);
            note ??= new GUIStyle(skin.Style(14, false, TextAnchor.UpperLeft)) { wordWrap = true, clipping = TextClipping.Clip };
            var old = GUI.color;
            DrawGoal(data, panel);
            if (!board.MemberResult) { GUI.color = Palette.Gold; GUI.Label(new Rect(panel.x + 14, panel.y + 48, panel.width - 28, 64), WorkoutReport.GuestWeekNote, note); GUI.color = old; return; }
            var stats = data?.LastStats;
            if (stats == null) { GUI.color = Palette.Sand; GUI.Label(new Rect(panel.x + 14, panel.y + 48, panel.width - 28, 64), board.KcalNote, note); GUI.color = old; return; }
            var lines = WorkoutReport.Week(stats);
            for (int i = 0; i < lines.Count; i++)
            {
                float y = panel.y + 44 + i * 46;
                skin.Text(new Rect(panel.x + 14, y, panel.width - 28, 18), lines[i].Key, 13, Palette.Sand, TextAnchor.MiddleLeft, false, true);
                skin.Text(new Rect(panel.x + 14, y + 18, panel.width - 28, 24), lines[i].Value, 15, Palette.Cream, TextAnchor.MiddleLeft, false, true);
            }
            GUI.color = Palette.Gold; GUI.Label(new Rect(panel.x + 14, panel.y + 132, panel.width - 28, 22), WorkoutReport.WeekChange(stats), note); GUI.color = old;
        }
        // Bottom of the week panel: the weekly goal bar (moderate minutes) and the streak with its coins. A keyboard run is not
        // counted at all (QA 2026-11-25 L10); a guest sees this run's moderate minutes only, no PC-wide week or streak (L3).
        void DrawGoal(IHudData data, Rect panel)
        {
            if (data == null) return;
            if (!PlayStyleInfo.UsesCamera(data.Player?.Style ?? PlayStyle.Keyboard))
            {
                skin.Text(new Rect(panel.x + 14, panel.y + 160, panel.width - 28, 18), WorkoutReport.KeyboardWeekNote, 12, Palette.Sand, TextAnchor.MiddleLeft, false, true);
                return;
            }
            double minutes = data.WeekMinutes;
            if (!board.MemberResult)
            {
                skin.Text(new Rect(panel.x + 14, panel.y + 160, panel.width - 28, 18), WorkoutReport.RunModerate(minutes), 12, Palette.Sand, TextAnchor.MiddleLeft, false, true);
                return;
            }
            skin.Text(new Rect(panel.x + 14, panel.y + 150, panel.width - 28, 18), WorkoutReport.WeekGoal(minutes), 12, Palette.Sand, TextAnchor.MiddleLeft, false, true);
            var track = new Rect(panel.x + 14, panel.y + 170, panel.width - 28, 6);
            skin.Box(track, Palette.A(Palette.Ground, .9f));
            float share = Mathf.Clamp01((float)(minutes / ExerciseLog.WeeklyGoalMinutes));
            if (share > 0) skin.Box(new Rect(track.x, track.y, Mathf.Max(3, track.width * share), track.height), share >= 1 ? new Color(.4f, 1, .6f) : Palette.Gold);
            string streak = WorkoutReport.StreakLine(data.Streak, data.LastStreakCoins);
            if (streak.Length > 0) skin.Text(new Rect(panel.x + 14, panel.y + 178, panel.width - 28, 18), streak, 12, Palette.Cream, TextAnchor.MiddleLeft, false, true);
        }

        // Right panel: this run, and for a member today's and lifetime totals from /stats; a guest sees the sign-up note.
        void DrawKcal(RunSession session)
        {
            var panel = new Rect(1066, 140, 190, 240);
            skin.BorderBox(panel, Palette.A(Palette.Panel, .95f), Palette.Wood, 2);
            skin.Text(new Rect(panel.x + 14, panel.y + 12, panel.width - 28, 30), "칼로리", 22, Palette.Gold, TextAnchor.MiddleLeft);
            var lines = board.KcalLines(session);
            for (int i = 0; i < lines.Count; i++)
            {
                float y = panel.y + 52 + i * 50;
                skin.Text(new Rect(panel.x + 14, y, panel.width - 28, 20), lines[i].Key, 13, Palette.Sand, TextAnchor.MiddleLeft, false, true);
                skin.Text(new Rect(panel.x + 14, y + 20, panel.width - 28, 26), lines[i].Value, 17, Palette.Cream, TextAnchor.MiddleLeft, false, true);
            }
            string text = board.KcalNote;
            if (text.Length == 0) return;
            // Notes wrap inside the 162 px column instead of running off the screen edge.
            note ??= new GUIStyle(skin.Style(14, false, TextAnchor.UpperLeft)) { wordWrap = true, clipping = TextClipping.Clip };
            var old = GUI.color; GUI.color = board.MemberResult ? Palette.Sand : Palette.Gold;
            GUI.Label(new Rect(panel.x + 14, panel.y + 52 + lines.Count * 50, panel.width - 28, 64), text, note);
            GUI.color = old;
        }

        void DrawTabs(float x, float w)
        {
            int source = Group(new Rect(x, 104, 232, 44), SourceTabs, board.MemberTab ? 0 : 1);
            int mode = Group(new Rect(x + 252, 104, 252, 44), ModeTabs, (int)board.Mode);
            int period = Group(new Rect(x + w - 240, 104, 240, 44), PeriodTabs, board.Challenge ? 2 : board.TodayOnly ? 1 : 0);
            if (source >= 0) board.Select(source == 0, board.Mode, board.TodayOnly, board.Challenge);
            if (mode >= 0) board.Select(board.MemberTab, (PlayStyle)mode, board.TodayOnly, board.Challenge);
            if (period >= 0) board.Select(board.MemberTab, board.Mode, period == 1, period == 2);
        }

        // One tab row; returns the pressed tab (mouse or hand pointing), -1 otherwise.
        int Group(Rect r, string[] labels, int selected)
        {
            skin.Box(r, Palette.Panel);
            float each = (r.width - 8) / labels.Length; int pressed = -1;
            for (int i = 0; i < labels.Length; i++)
                if (Tab(new Rect(r.x + 4 + i * each, r.y + 4, each, r.height - 8), labels[i], i == selected) && i != selected) pressed = i;
            return pressed;
        }

        bool Tab(Rect r, string label, bool on)
        {
            if (on) skin.Box(r, Palette.Gold);
            skin.Text(r, label, 16, on ? Palette.Outline : Palette.Sand, TextAnchor.MiddleCenter, false, true);
            return GUI.Button(r, GUIContent.none, GUIStyle.none)||GesturePointer.Activate(r,label);
        }

        // This PC's board marks member runs with a small badge after the name.
        bool Badge(RankingRow row) => !board.MemberTab && row.Member;
        void MemberBadge(Rect nameRect, string name, int size, TextAnchor anchor)
        {
            float width = skin.Style(size, false, anchor, true).CalcSize(new GUIContent(name)).x;
            float left = anchor == TextAnchor.MiddleCenter ? nameRect.center.x + width / 2 + 6 : nameRect.x + width + 8;
            var r = new Rect(left, nameRect.center.y - 10, 40, 20);
            skin.Box(r, Palette.A(Palette.Gold, 0.85f));
            skin.Text(r, "회원", 12, Palette.Outline, TextAnchor.MiddleCenter, false, true);
        }

        void DrawTop3(float x, float w)
        {
            var list = board.Rows;
            float cardW = (w - 32) / 3f, bottom = 158 + 150;
            Color[] rankColor = { Palette.Gold, Palette.Silver, Palette.Bronze };
            for (int s = 0; s < 3; s++)
            {
                int rank = SlotRank[s];
                if (rank >= list.Count) continue;
                var r = new Rect(x + s * (cardW + 16), bottom - SlotHeight[s], cardW, SlotHeight[s]);
                skin.BorderBox(r, Palette.A(Palette.Panel, 0.95f), rankColor[rank], 2);
                float cy = r.center.y;
                skin.Text(new Rect(r.x, cy - 54, r.width, 34), list[rank].Rank.ToString(), 30, rankColor[rank], TextAnchor.MiddleCenter);
                var nameRect = new Rect(r.x, cy - 16, r.width, 24);
                skin.Text(nameRect, list[rank].Name, 17, Palette.Cream, TextAnchor.MiddleCenter, false, true);
                if (Badge(list[rank])) MemberBadge(nameRect, list[rank].Name, 17, TextAnchor.MiddleCenter);
                skin.Text(new Rect(r.x, cy + 12, r.width, 34), RunHudModel.Points(list[rank].Score), 28, Palette.Cream, TextAnchor.MiddleCenter);
            }
            if (list.Count == 0)
                skin.Text(new Rect(x, 200, w, 60), board.Challenge ? "오늘의 도전 첫 기록을 남겨 보세요! (모두 같은 맵)" : board.TodayOnly ? "오늘은 아직 기록이 없어요. 첫 기록을 남겨 보세요!" : "아직 기록이 없어요. 첫 기록을 남겨 보세요!", 18, Palette.Sand, TextAnchor.MiddleCenter, false, true);
        }

        void DrawRows(float x, float w)
        {
            var list = board.Rows;
            for (int i = 0; i < 5; i++)
            {
                int rank = i + 3;
                if (rank >= list.Count) break;
                var r = new Rect(x, 318 + i * 42, w, 38);
                skin.BorderBox(r, Palette.A(Palette.Panel, 0.92f), Palette.WoodDim, 2);
                Columns(r, list[rank].Rank, list[rank].Name, RunHudModel.Points(list[rank].Score), 18, 20, Palette.Muted, Palette.Cream);
                if (Badge(list[rank])) MemberBadge(new Rect(r.x + 84, r.y, r.width - 260, r.height), list[rank].Name, 16, TextAnchor.MiddleLeft);
            }
        }

        void DrawMine(IHudData data, float x, float w)
        {
            var me = board.Mine;
            if (me == null || board.Status != RankingStatus.Ready) return;
            var row = new Rect(x, 532, w, 50);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            skin.BorderBox(row, Palette.MeFill, Color.Lerp(Palette.Gold, Palette.Cream, pulse * 0.35f), 2);
            Columns(row, me.Rank, "▶ 내 기록", RunHudModel.Points(me.Score), 20, 24, Palette.Gold, Palette.Gold);
        }

        void Columns(Rect r, int rank, string name, string score, int rankSize, int scoreSize, Color rankColor, Color scoreColor)
        {
            skin.Text(new Rect(r.x + 20, r.y, 60, r.height), rank.ToString(), rankSize, rankColor, TextAnchor.MiddleLeft);
            skin.Text(new Rect(r.x + 84, r.y, r.width - 260, r.height), name, 16, Palette.Cream, TextAnchor.MiddleLeft, false, true);
            skin.Text(new Rect(r.xMax - 180, r.y, 160, r.height), score, scoreSize, scoreColor, TextAnchor.MiddleRight);
        }
    }
}
