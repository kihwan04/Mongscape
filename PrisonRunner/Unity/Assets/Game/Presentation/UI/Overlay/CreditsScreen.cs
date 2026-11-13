using UnityEngine;

namespace Muhanok.Presentation
{
    // The version shown to players: "v1.0.0", with "(Dev)" on Editor and development builds.
    public static class BuildInfo
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static string Label => "v" + UnityEngine.Application.version + " (Dev)";
#else
        public static string Label => "v" + UnityEngine.Application.version;
#endif
    }

    // Credits and third-party notices. Kept in step with THIRD_PARTY_NOTICES.md, which ships next to the game.
    internal sealed class CreditsScreen
    {
        // Name, what it is used for, license.
        internal static readonly string[,] Notices =
        {
            { "Unity 6", "게임 엔진 (Unity Technologies)", "Unity 이용 약관" },
            { "Kenney", "Factory·Furniture·Modular Cave·Nature·Survival Kit", "CC0" },
            { "Adobe Mixamo", "달리기·점프·숙이기·비틀거림 동작", "Mixamo 이용 약관" },
            { "Black Han Sans", "글꼴 (Zess Type)", "SIL Open Font License 1.1" },
            { "BlazePose", "몸동작 인식 모델 (Google MediaPipe, Unity 변환본)", "Apache-2.0" },
            { "YuNet · SFace", "얼굴 서버의 얼굴 검출·인식 모델 (OpenCV Zoo)", "MIT · Apache-2.0" },
            { "얼굴 서버 구성 요소", "OpenCV · PostgreSQL · Psycopg", "각 프로젝트 라이선스" },
        };

        readonly OverlaySkin skin;
        GUIStyle wrap;
        public CreditsScreen(OverlaySkin overlaySkin) { skin = overlaySkin; }

        // True when the player leaves (button or Esc).
        public bool Draw()
        {
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Ground, 0.7f));
            GUI.matrix = oldMatrix;

            var panel = new Rect(160, 50, 960, 620);
            skin.BorderBox(panel, Palette.A(Palette.Panel, 0.97f), Palette.Wood, 3);
            skin.Text(new Rect(200, 72, 600, 50), "크레딧", 36, Palette.Gold, TextAnchor.MiddleLeft);
            skin.Text(new Rect(700, 80, 380, 40), "Mongscape (몽스케이프)  " + BuildInfo.Label, 18, Palette.Sand, TextAnchor.MiddleRight, false, true);
            skin.Text(new Rect(200, 128, 880, 30), "기획·개발: bodyLanguage, Monkeys", 20, Palette.Cream, TextAnchor.MiddleLeft, false, true);
            skin.Text(new Rect(200, 160, 880, 26), "배경 음악과 효과음은 게임 안에서 코드로 합성합니다.", 16, Palette.Sand, TextAnchor.MiddleLeft, false);

            skin.Fill(new Rect(200, 198, 880, 2), Palette.A(Palette.Wood, 0.9f));
            skin.Text(new Rect(200, 206, 880, 32), "사용한 외부 리소스", 22, Palette.Gold, TextAnchor.MiddleLeft);
            for (int i = 0; i < Notices.GetLength(0); i++)
            {
                float y = 246 + i * 44;
                if (i % 2 == 0) skin.Fill(new Rect(200, y - 4, 880, 42), Palette.A(Palette.Track, 0.55f));
                skin.Text(new Rect(214, y, 260, 34), Notices[i, 0], 18, Palette.Cream, TextAnchor.MiddleLeft, false, true);
                skin.Text(new Rect(480, y, 400, 34), Notices[i, 1], 15, Palette.Sand, TextAnchor.MiddleLeft, false);
                skin.Text(new Rect(880, y, 186, 34), Notices[i, 2], 15, Palette.MeNote, TextAnchor.MiddleRight, false);
            }
            wrap ??= new GUIStyle(skin.Style(15, false, TextAnchor.UpperLeft)) { wordWrap = true, clipping = TextClipping.Clip };
            GUI.color = Palette.Sand;
            GUI.Label(new Rect(200, 562, 640, 48), "라이선스 원문과 출처는 게임 폴더의 THIRD_PARTY_NOTICES.md에 있습니다.\nMixamo 원본 파일은 함께 배포하지 않습니다.", wrap);
            GUI.color = Color.white;

            bool back = skin.Button(new Rect(920, 596, 160, 52), "뒤로", Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 22);
            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) { e.Use(); back = true; }
            return back;
        }
    }
}
