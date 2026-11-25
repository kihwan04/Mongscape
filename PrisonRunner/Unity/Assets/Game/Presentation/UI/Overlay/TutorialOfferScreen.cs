using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    public enum TutorialChoice { None, Start, Skip }

    // Offered after sign-up and once on "게임 시작" (members who have not finished it, every guest), and opened by the main menu's
    // "튜토리얼". The player starts it in a chosen play style; body styles calibrate before the first lesson.
    internal sealed class TutorialOfferScreen
    {
        public enum Mode { AfterSignUp, BeforePlay, FromMenu }
        readonly OverlaySkin skin;
        GUIStyle wrap;
        public TutorialOfferScreen(OverlaySkin overlaySkin) { skin = overlaySkin; }

        // bodyOk: a camera is available for the full and upper body styles. The chosen style comes back in `style`.
        public TutorialChoice Draw(Mode mode, bool bodyOk, PlayStyle current, out PlayStyle style)
        {
            style = current;
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Ground, 0.6f));
            GUI.matrix = oldMatrix;

            var panel = new Rect(300, 130, 680, 460);
            skin.BorderBox(panel, Palette.A(Palette.Panel, 0.96f), Palette.Wood, 3);
            skin.Text(new Rect(340, 160, 600, 50), mode == Mode.AfterSignUp ? "가입을 환영합니다!" : mode == Mode.BeforePlay ? "처음이신가요?" : "튜토리얼", 36, Palette.Gold, TextAnchor.MiddleCenter);
            wrap ??= new GUIStyle(skin.Style(18, false, TextAnchor.UpperCenter, true)) { wordWrap = true, clipping = TextClipping.Clip };
            GUI.color = Palette.Cream;
            GUI.Label(new Rect(340, 222, 600, 96), "옆으로 피하기·점프·숙이기·무릎 높이·갈림길 기울이기를 연습해 보세요. 연습할 방식을 고르면 시작합니다(전신·상체는 먼저 자세를 맞춥니다).\n튜토리얼 기록은 랭킹·코인·칼로리에 들어가지 않습니다.", wrap);
            GUI.color = Color.white;

            var choice = TutorialChoice.None;
            for (int i = 0; i < 3; i++)
            {
                var option = (PlayStyle)i;
                bool allowed = PlayStyleInfo.Allowed(option, bodyOk);
                var r = new Rect(340 + i * 204, 336, 192, 78);
                bool was = GUI.enabled; GUI.enabled = was && allowed;
                bool picked = skin.Button(r, (i + 1) + ". " + PlayStyleInfo.Label(option), allowed ? (option == current ? Palette.Gold : Palette.A(Palette.Gold, .78f)) : Palette.A(Palette.Ground, .6f),
                    Color.clear, allowed ? Palette.Outline : Palette.Muted, 26);
                GUI.enabled = was;
                if (picked) { choice = TutorialChoice.Start; style = option; }
            }
            if (!bodyOk) skin.Text(new Rect(340, 418, 600, 22), "카메라가 연결되지 않아 키보드로만 연습할 수 있어요.", 14, Palette.Muted, TextAnchor.MiddleCenter, false, true);
            string skip = mode == Mode.AfterSignUp ? "나중에 하기" : mode == Mode.BeforePlay ? "바로 게임 시작" : "돌아가기";
            if (skin.Button(new Rect(340, 448, 600, 58), "4. " + skip, Palette.A(Palette.Ground, 0.92f), Palette.Wood, Palette.Cream, 22)) choice = TutorialChoice.Skip;
            // Keys: 1~3 pick the style, Enter the current one; 4 or Esc skips.
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                int key = e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha3 ? e.keyCode - KeyCode.Alpha1 : e.keyCode >= KeyCode.Keypad1 && e.keyCode <= KeyCode.Keypad3 ? e.keyCode - KeyCode.Keypad1 : -1;
                if (key >= 0 && PlayStyleInfo.Allowed((PlayStyle)key, bodyOk)) { e.Use(); choice = TutorialChoice.Start; style = (PlayStyle)key; }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { e.Use(); choice = TutorialChoice.Start; style = PlayStyleInfo.Playable(current, bodyOk); }
                else if (e.keyCode == KeyCode.Alpha4 || e.keyCode == KeyCode.Keypad4 || e.keyCode == KeyCode.Escape) { e.Use(); choice = TutorialChoice.Skip; }
            }
            skin.Text(new Rect(340, 520, 600, 26), GesturePointer.Hint("1~3으로 방식을 고르거나 4(건너뛰기)를 누르세요."), 14, Palette.Muted, TextAnchor.MiddleCenter, false, true);
            return choice;
        }
    }
}
