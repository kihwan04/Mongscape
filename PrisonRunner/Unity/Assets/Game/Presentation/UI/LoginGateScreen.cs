using UnityEngine;

namespace Muhanok.Presentation
{
    public enum LoginAction { None, Face, SignUp, Guest, Settings }

    // First screen after the title: face login, sign-up or guest play. Without the account server only guest play is offered.
    internal sealed class LoginGateScreen
    {
        static readonly string[] Labels = { "얼굴 로그인", "회원가입", "게스트 플레이" };
        static readonly LoginAction[] Actions = { LoginAction.Face, LoginAction.SignUp, LoginAction.Guest };
        static readonly KeyCode[] Keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
        static readonly KeyCode[] Keypad = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3 };

        readonly OverlaySkin skin;
        GUIStyle wrap, reason;
        public LoginGateScreen(OverlaySkin overlaySkin) { skin = overlaySkin; }

        public LoginAction Draw(IIdentityData identity, Texture2D logo)
        {
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Ground, 0.6f));
            GUI.matrix = oldMatrix;

            if (logo != null) GUI.DrawTexture(new Rect(40, 65, 485, 310), logo, ScaleMode.ScaleToFit, true);
            else skin.OutlinedText(new Rect(76, 170, 650, 115), "Mongscape", "Mongscape", 74, Palette.Cream, Palette.Outline, 2.5f, TextAnchor.UpperLeft);

            bool checking = identity == null || identity.ServerChecking;
            string problem = identity?.ServerProblem ?? "";
            bool server = !checking && string.IsNullOrEmpty(problem);
            skin.Text(new Rect(760, 80, 440, 44), "어떻게 시작할까요?", 34, Palette.Cream, TextAnchor.MiddleLeft);
            // An expired session's notice takes the place of the member line.
            string notice = identity?.LoginNotice ?? "";
            if (notice.Length > 0) skin.Text(new Rect(760, 126, 440, 26), notice, 17, Palette.Warn, TextAnchor.MiddleLeft, false, true);
            else skin.Text(new Rect(760, 126, 440, 26), "회원은 코인을 모아 아이템을 해금하고 통합 랭킹에 오릅니다.", 15, Palette.Sand, TextAnchor.MiddleLeft, false, true);

            var action = LoginAction.None;
            var e = Event.current;
            for (int i = 0; i < Labels.Length; i++)
            {
                var r = new Rect(760, 172 + i * 92, 440, 76);
                bool guest = Actions[i] == LoginAction.Guest, enabled = guest || server;
                bool wasEnabled = GUI.enabled; GUI.enabled = wasEnabled && enabled;
                Color text = !enabled ? Palette.A(Palette.Sand, 0.5f) : i == 0 ? Palette.Outline : Palette.Cream;
                bool pressed = false;
                // Without the server these are plain frames, not dimmed buttons, so the labels stay readable.
                if (enabled) pressed = skin.Button(r, Labels[i], i == 0 ? Palette.Gold : Palette.A(Palette.Ground, 0.92f), i == 0 ? Color.clear : Palette.Wood, text, i == 0 ? 30 : 26);
                else
                {
                    GUI.enabled = wasEnabled;
                    skin.BorderBox(r, Palette.A(Palette.Ground, 0.6f), Palette.WoodDim, 2);
                    skin.Text(r, Labels[i], 26, text, TextAnchor.MiddleCenter);
                    GUI.enabled = false;
                }
                skin.Text(new Rect(r.x + 14, r.y + 6, 30, 24), (i + 1).ToString(), 15, text, TextAnchor.UpperLeft, false, true);
                // Keys 1-3 choose like the buttons.
                if (GUI.enabled && e.type == EventType.KeyDown && (e.keyCode == Keys[i] || e.keyCode == Keypad[i])) { e.Use(); pressed = true; }
                GUI.enabled = wasEnabled;
                if (pressed) action = Actions[i];
            }

            // Without the server: what still works first, then the short reason underneath.
            string note = checking ? "얼굴 서버 확인 중…" : server ? "얼굴 서버 연결됨" : "지금은 얼굴 로그인과 회원가입을 쓸 수 없어요.\n게스트로 바로 플레이할 수 있어요.";
            wrap ??= new GUIStyle(skin.Style(15, false, TextAnchor.UpperLeft, true)) { wordWrap = true, clipping = TextClipping.Clip };
            reason ??= new GUIStyle(skin.Style(13, false, TextAnchor.UpperLeft)) { wordWrap = true, clipping = TextClipping.Clip };
            GUI.color = server || checking ? Palette.Sand : Palette.Warn; GUI.Label(new Rect(760, 446, 440, 44), note, wrap);
            if (!server && !checking) { GUI.color = Palette.A(Palette.Sand, 0.85f); GUI.Label(new Rect(760, 490, 440, 36), "이유: " + problem, reason); }
            GUI.color = Color.white;
            skin.Text(new Rect(760, 532, 440, 26), GesturePointer.Hint("숫자키 1~3을 누르세요."), 14, Palette.Sand, TextAnchor.MiddleLeft, false);
            if (skin.Button(new Rect(1050, 590, 150, 48), "설정", Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 20)) action = LoginAction.Settings;
            return action;
        }
    }
}
