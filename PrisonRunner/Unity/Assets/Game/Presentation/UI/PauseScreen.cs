using System;
using System.Globalization;
using Muhanok.Domain;
using Muhanok.Presentation.Map;
using UnityEngine;

namespace Muhanok.Presentation
{
    internal sealed class PauseScreen
    {
        private static readonly CultureInfo Num = CultureInfo.InvariantCulture;
        private readonly HudSkin skin;
        public PauseScreen(HudSkin hudSkin) { skin = hudSkin; }
        // Leaving to the menu ends the run without a record.
        public void Draw(RunSession session, IHudData data, Action resume, Action settings, Action toMenu)
        {
            skin.Card(new Rect(0, 0, 1280, 720), Palette.A(Palette.Ground, .75f));
            skin.Panel(new Rect(285, 140, 710, 440), .97f);
            GUI.color = Color.white; GUI.Label(new Rect(325, 165, 620, 60), "잠깐 쉬어가기", skin.Style(34, FontStyle.Bold));
            // Label and value columns, one fact per row, left of the buttons at x 710.
            int row = 0;
            Row(ref row, "점수", RunHudModel.Points(session.Score.Score), true);
            Row(ref row, "시간", RunHudModel.Clock(session.Elapsed));
            Row(ref row, "거리", session.Score.Distance.ToString("N0", Num) + " m");
            // A member's best for this play style; guests have none.
            int best = data?.BestScore ?? -1;
            if (best >= 0) Row(ref row, "최고", RunHudModel.Points(Mathf.Max(best, session.Score.Score)));
            Row(ref row, "코인", session.Score.Coins.ToString(Num));
            Row(ref row, "위치", RouteSurface.AreaAt(session.Runner.Z));
            bool workout = PlayStyleInfo.UsesCamera(data?.Player?.Style ?? PlayStyle.Keyboard);
            Row(ref row, "칼로리", workout ? session.Workout.EstimatedKcal.ToString("F1", Num) + " / " + session.Workout.GoalKcal.ToString("F0", Num) + " kcal" : RunHudModel.KeyboardKcal);
            if (session.Config.IntervalMode) Row(ref row, "구간 전환", session.PhaseRemainingSeconds.ToString("F0", Num) + "초 남음");
            else Row(ref row, "최고 콤보", session.BestDodgeStreak.ToString(Num));
            if (skin.PrimaryButton(new Rect(710, 247, 240, 58), "계속하기", 26)) resume?.Invoke();
            if (skin.FlatButton(new Rect(710, 325, 240, 48), "설정", false)) settings?.Invoke();
            if (skin.FlatButton(new Rect(710, 391, 240, 48), "메인으로", false)) toMenu?.Invoke();
            GUI.color = new Color(1, 1, 1, .75f);
            GUI.Label(new Rect(325, 534, 625, 30), "메인으로 나가면 이번 플레이 기록은 저장하지 않습니다.", skin.Style(17, FontStyle.Normal));
            GUI.color = Color.white;
        }
        private void Row(ref int row, string label, string value, bool large = false)
        {
            float y = 238 + row++ * 34;
            GUI.color = Palette.Sand; GUI.Label(new Rect(325, y, 110, 32), label, skin.Style(18, FontStyle.Normal));
            GUI.color = Color.white; GUI.Label(new Rect(440, y - (large ? 5 : 0), 250, 36), value, skin.Style(large ? 26 : 20, large ? FontStyle.Bold : FontStyle.Normal));
        }
    }
}
