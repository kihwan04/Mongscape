using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Result screen 웹캠 보정: a large camera preview while the player stands still; "보정 완료" shows for a second, then it closes.
    // Hand pointing is off meanwhile (the bootstrap stops feeding GesturePointer); Esc or [닫기] with the mouse closes it early.
    internal sealed class CalibrationPanel
    {
        public const float DoneSeconds = 1f;
        readonly OverlaySkin skin;
        readonly HudSkin hudSkin;
        float doneAt = -1;
        public bool Open { get; private set; }
        public bool Done => doneAt >= 0;
        public CalibrationPanel(OverlaySkin overlaySkin, HudSkin hud) { skin = overlaySkin; hudSkin = hud; }

        public void Show(IHudData data) { if (data == null) return; data.Recalibrate(); Open = true; doneAt = -1; GesturePointer.Reset(); }
        public void Close() { Open = false; doneAt = -1; }

        // Completion runs on the clock (HUDPresenter.Update), so the panel also closes while the HUD is hidden.
        public void Tick(IHudData data, float time)
        {
            if (!Open || data == null) return;
            if (doneAt < 0 && data.PoseStatus == PoseStatus.Tracking && data.CalibrationProgress >= 1) doneAt = time;
            if (doneAt >= 0 && time - doneAt >= DoneSeconds) Close();
        }

        // Esc closes; every other key stays here so the result screen underneath (guest name row) does not react.
        public void Keys()
        {
            var e = Event.current;
            if (!Open || e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Escape) Close();
            e.Use();
        }

        public void Draw(IHudData data)
        {
            if (!Open || data == null) return;
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Dark, .8f));
            GUI.matrix = oldMatrix;
            skin.BorderBox(new Rect(240, 40, 800, 640), Palette.A(Palette.Panel, .98f), Palette.Gold, 2);
            skin.Text(new Rect(270, 54, 740, 44), "웹캠 보정", 34, Palette.Gold, TextAnchor.MiddleCenter);
            skin.Text(new Rect(270, 98, 740, 28), "정면에서 가만히 서 주세요", 20, Palette.Cream, TextAnchor.MiddleCenter, false, true);
            var preview = new Rect(280, 136, 720, 440);
            PosePreview.Draw(hudSkin, preview, data, true);
            if (Done)
            {
                var badge = new Rect(preview.center.x - 150, preview.center.y - 40, 300, 80);
                skin.BorderBox(badge, Palette.Gold, Palette.Cream, 2);
                skin.Text(badge, "보정 완료", 34, Palette.Outline, TextAnchor.MiddleCenter);
            }
            if (skin.Button(new Rect(540, 600, 200, 56), "닫기 (Esc)", Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 22)) Close();
        }
    }
}
