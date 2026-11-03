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
        // card slots left to right: 2nd, 1st, 3rd
        static readonly int[] SlotRank = { 1, 0, 2 };
        static readonly float[] SlotHeight = { 138, 158, 128 };
        static readonly string[] SourceTabs = { "회원 랭킹", "이 PC" }, ModeTabs = { "전신", "상체", "키보드" }, PeriodTabs = { "전체", "오늘" };

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
            summary.Add(new KeyValuePair<string, string>("최고 콤보", session.BestDodgeStreak.ToString(Num)));
            guestName.Show(data);
            board.OpenResult(data, account);
        }

        static string Dist(float m) { return Mathf.FloorToInt(m).ToString("N0", Num) + " m"; }

        // primary/secondary: result buttons (다시 하기 / 메인으로 or 다음 플레이어); the menu board only shows a close button.
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
            if (board.ResultMode) { DrawWorkout(session, data); DrawKcal(session); }
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

        void DrawWorkout(RunSession session, IHudData data)
        {
            skin.BorderBox(new Rect(24,140,190,380),Palette.A(Palette.Panel,.95f),Palette.Wood,2);
            skin.Text(new Rect(38,152,164,30),"운동 기록",22,Palette.Gold,TextAnchor.MiddleLeft);
            skin.Text(new Rect(38,190,164,26),"최고 콤보 "+session.BestDodgeStreak,16,Palette.Cream,TextAnchor.MiddleLeft,false,true);
            if (!PlayStyleInfo.UsesCamera(data?.Player?.Style ?? PlayStyle.Keyboard))
            {
                skin.Text(new Rect(38,235,164,30),RunHudModel.KeyboardKcal,15,Palette.Sand,TextAnchor.MiddleLeft,false,true);
                return;
            }
            skin.Text(new Rect(38,220,164,26),"목표 "+session.Workout.GoalKcal.ToString("F0",Num)+" kcal",15,Palette.Sand,TextAnchor.MiddleLeft,false,true);
            for(int i=0;i<5;i++)
            {
                skin.Text(new Rect(38,258+i*46,164,22),WorkoutEstimate.ActivityNames[i],14,Palette.Sand,TextAnchor.MiddleLeft,false);
                skin.Text(new Rect(38,278+i*46,164,22),session.Workout.KcalFor(i).ToString("F1",Num)+" kcal",16,Palette.Cream,TextAnchor.MiddleLeft,false,true);
            }
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
            int period = Group(new Rect(x + w - 160, 104, 160, 44), PeriodTabs, board.TodayOnly ? 1 : 0);
            if (source >= 0) board.Select(source == 0, board.Mode, board.TodayOnly);
            if (mode >= 0) board.Select(board.MemberTab, (PlayStyle)mode, board.TodayOnly);
            if (period >= 0) board.Select(board.MemberTab, board.Mode, period == 1);
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
                skin.Text(new Rect(x, 200, w, 60), board.TodayOnly ? "오늘은 아직 기록이 없어요. 첫 기록을 남겨 보세요!" : "아직 기록이 없어요. 첫 기록을 남겨 보세요!", 18, Palette.Sand, TextAnchor.MiddleCenter, false, true);
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
            // Developer mode has a name per run; otherwise the row reads "내 기록".
            string name = !board.MemberTab && data != null && data.DeveloperMode && board.MineEntry != null ? board.MineEntry.Name : "▶ 내 기록";
            Columns(row, me.Rank, name, RunHudModel.Points(me.Score), 20, 24, Palette.Gold, Palette.Gold);
        }

        void Columns(Rect r, int rank, string name, string score, int rankSize, int scoreSize, Color rankColor, Color scoreColor)
        {
            skin.Text(new Rect(r.x + 20, r.y, 60, r.height), rank.ToString(), rankSize, rankColor, TextAnchor.MiddleLeft);
            skin.Text(new Rect(r.x + 84, r.y, r.width - 260, r.height), name, 16, Palette.Cream, TextAnchor.MiddleLeft, false, true);
            skin.Text(new Rect(r.xMax - 180, r.y, 160, r.height), score, scoreSize, scoreColor, TextAnchor.MiddleRight);
        }
    }
}
