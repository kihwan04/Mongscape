using UnityEngine;

namespace Muhanok.Presentation
{
    public enum TutorialChoice { None, Start, Skip }

    // Offered after sign-up and once on "게임 시작" (members who have not finished it, every guest).
    internal sealed class TutorialOfferScreen
    {
        readonly OverlaySkin skin;
        GUIStyle wrap;
        public TutorialOfferScreen(OverlaySkin overlaySkin) { skin = overlaySkin; }

        public TutorialChoice Draw(bool afterSignUp)
        {
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Ground, 0.6f));
            GUI.matrix = oldMatrix;

            var panel = new Rect(340, 150, 600, 420);
            skin.BorderBox(panel, Palette.A(Palette.Panel, 0.96f), Palette.Wood, 3);
            skin.Text(new Rect(380, 182, 520, 50), afterSignUp ? "가입을 환영합니다!" : "처음이신가요?", 36, Palette.Gold, TextAnchor.MiddleCenter);
            wrap ??= new GUIStyle(skin.Style(18, false, TextAnchor.UpperCenter, true)) { wordWrap = true, clipping = TextClipping.Clip };
            GUI.color = Palette.Cream;
            GUI.Label(new Rect(380, 246, 520, 96), "튜토리얼에서 옆으로 피하기·점프·숙이기·무릎 높이·갈림길 기울이기를 천천히 연습해 보세요.\n튜토리얼 기록은 랭킹·코인·칼로리에 들어가지 않습니다.", wrap);
            GUI.color = Color.white;

            var choice = TutorialChoice.None;
            var e = Event.current;
            if (skin.Button(new Rect(380, 362, 520, 70), "튜토리얼 시작", Palette.Gold, Color.clear, Palette.Outline, 28)) choice = TutorialChoice.Start;
            if (skin.Button(new Rect(380, 446, 520, 58), afterSignUp ? "나중에 하기" : "바로 게임 시작", Palette.A(Palette.Ground, 0.92f), Palette.Wood, Palette.Cream, 22)) choice = TutorialChoice.Skip;
            // Keys: 1 or Enter starts the tutorial, 2 or Esc skips it.
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Alpha1 || e.keyCode == KeyCode.Keypad1 || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { e.Use(); choice = TutorialChoice.Start; }
                else if (e.keyCode == KeyCode.Alpha2 || e.keyCode == KeyCode.Keypad2 || e.keyCode == KeyCode.Escape) { e.Use(); choice = TutorialChoice.Skip; }
            }
            skin.Text(new Rect(380, 516, 520, 26), GesturePointer.Hint("1(시작)이나 2(건너뛰기)를 누르세요."), 14, Palette.Muted, TextAnchor.MiddleCenter, false, true);
            return choice;
        }
    }
}
