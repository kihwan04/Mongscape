using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Webcam calibration screen. Two uses share the same picture: a large camera view with the skeleton, a big call
    // ("가만히 서 주세요", "움직이지 마세요" when the player moves, a camera setup hint when the body is cut off), a ring that
    // fills while the classifier measures, and "보정 완료" for a second at the end.
    // - Run start (and the tutorial's calibration step): DrawRun, while the bootstrap holds the countdown (IHudData.RunCalibration).
    // - Result screen 웹캠 보정: Show/Tick/Draw; hand pointing is off meanwhile (the bootstrap stops feeding GesturePointer);
    //   Esc or [닫기] with the mouse closes it early.
    internal sealed class CalibrationPanel
    {
        public const float DoneSeconds = 1f;
        public const string TutorialLine = "보정: 내 키와 서 있는 자세를 재는 단계입니다";
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

        // The big call over the camera view.
        public static string Call(IHudData data, bool done)
        {
            if (done) return "보정 완료";
            var status = data.PoseStatus;
            if (status == PoseStatus.Starting) return "카메라 준비 중";
            if (status == PoseStatus.NoCamera || status == PoseStatus.NoModel) return PoseStatusText.Korean(status);
            if (status == PoseStatus.Searching || data.PoseFraming == FramingIssue.NoBody) return "카메라 앞에 서 주세요";
            if (data.PoseKneesMissing) return PosePreview.KneesHint;
            if (data.PoseCalibrationMoving) return "움직이지 마세요";
            return "가만히 서 주세요";
        }

        // The setup hint under the call (body cut off, too far, off to one side), or null.
        public static string Hint(IHudData data, bool done)
        {
            if (done || data.PoseKneesMissing) return null;
            var issue = data.PoseFraming;
            if (issue == FramingIssue.Ok || issue == FramingIssue.NoBody) return null;
            return CameraFraming.Hint(issue, data.Player?.Style ?? PlayStyle.FullBody);
        }

        // Result screen panel.
        public void Draw(IHudData data)
        {
            if (!Open || data == null) return;
            Dim();
            skin.BorderBox(new Rect(240, 40, 800, 640), Palette.A(Palette.Panel, .98f), Palette.Gold, 2);
            skin.Text(new Rect(270, 50, 740, 40), "웹캠 보정", 30, Palette.Gold, TextAnchor.MiddleCenter);
            Body(data, Done, new Rect(270, 92, 740, 44), new Rect(270, 136, 740, 26), new Rect(300, 168, 560, 420), new Vector2(940, 300), 64, 30);
            if (skin.Button(new Rect(540, 606, 200, 56), "닫기 (Esc)", Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 22)) Close();
        }

        // Run start: the whole screen until the countdown begins.
        public void DrawRun(IHudData data)
        {
            if (data == null || data.RunCalibration == RunCalibrationState.None) return;
            bool done = data.RunCalibration == RunCalibrationState.Done;
            Dim();
            skin.BorderBox(new Rect(130, 24, 1020, 672), Palette.A(Palette.Panel, .97f), Palette.Gold, 2);
            Body(data, done, new Rect(150, 34, 980, 70), new Rect(150, 104, 980, 32), new Rect(170, 146, 680, 510), new Vector2(990, 340), 100, 40);
            if (data.TutorialRun) skin.Text(new Rect(150, 658, 980, 30), TutorialLine, 20, Palette.Cream, TextAnchor.MiddleCenter, false, true);
        }

        private void Dim()
        {
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Dark, .8f));
            GUI.matrix = oldMatrix;
        }

        private void Body(IHudData data, bool done, Rect callArea, Rect hintArea, Rect preview, Vector2 ring, float radius, int callSize)
        {
            string call = Call(data, done), hint = Hint(data, done);
            bool warn = !done && (data.PoseCalibrationMoving || data.PoseKneesMissing || data.PoseFraming == FramingIssue.NoBody);
            skin.Text(callArea, call, callSize, done ? Palette.Gold : warn ? Palette.Warn : Palette.Cream, TextAnchor.MiddleCenter);
            if (hint != null) skin.Text(hintArea, hint, 20, Palette.Warn, TextAnchor.MiddleCenter, false, true);
            PosePreview.Draw(hudSkin, preview, data, false);
            float progress = done ? 1 : data.PoseStatus == PoseStatus.Calibrating ? data.CalibrationProgress : 0;
            Ring(ring, radius, progress, done ? Palette.Gold : data.PoseCalibrationMoving ? Palette.Warn : new Color(.3f, 1f, .55f));
            skin.Text(new Rect(ring.x - radius, ring.y - 30, radius * 2, 60), Mathf.RoundToInt(progress * 100) + "%", 34, done ? Palette.Gold : Palette.Cream, TextAnchor.MiddleCenter);
            if (done && Event.current.type == EventType.Repaint)
            {
                var badge = new Rect(preview.center.x - 150, preview.center.y - 40, 300, 80);
                skin.BorderBox(badge, Palette.Gold, Palette.Cream, 2);
                skin.Text(badge, "보정 완료", 34, Palette.Outline, TextAnchor.MiddleCenter);
            }
        }

        // A ring of dots that fills clockwise from the top.
        private static void Ring(Vector2 centre, float radius, float progress, Color fill)
        {
            if (Event.current.type != EventType.Repaint) return;
            const int Dots = 48;
            float size = Mathf.Max(6, radius * .16f);
            int lit = Mathf.RoundToInt(Mathf.Clamp01(progress) * Dots);
            for (int i = 0; i < Dots; i++)
            {
                float a = i / (float)Dots * Mathf.PI * 2;
                var p = centre + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * radius;
                GUI.color = i < lit ? fill : new Color(1, 1, 1, .18f);
                GUI.DrawTexture(new Rect(p.x - size * .5f, p.y - size * .5f, size, size), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
    }
}
