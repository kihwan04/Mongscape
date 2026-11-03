using UnityEngine;

namespace Muhanok.Presentation
{
    internal sealed class TitleScreen
    {
        private readonly HudSkin skin;
        public TitleScreen(HudSkin hudSkin) { skin = hudSkin; }
        public void Draw(Texture2D logo, float fade, System.Action open)
        {
            GUI.color = new Color(1f, 1f, 1f, fade);
            if (logo != null) GUI.DrawTexture(new Rect(56, 88, 450, 188), logo, ScaleMode.ScaleToFit, true);
            else GUI.Label(new Rect(76, 170, 650, 115), "Mongscape", skin.Large);
            // The whole screen starts the game; a pulsing prompt with a shadow stays readable over the dark mine.
            float pulse = .65f + .35f * Mathf.Sin(Time.unscaledTime * 3f);
            var prompt = new Rect(240, 630, 800, 56);
            GUI.color = new Color(0, 0, 0, .75f * fade * pulse); GUI.Label(new Rect(prompt.x + 2, prompt.y + 2, prompt.width, prompt.height), "아무 곳이나 눌러 시작하세요", skin.Centered(32, FontStyle.Bold));
            GUI.color = Palette.A(Palette.Gold, fade * pulse); GUI.Label(prompt, "아무 곳이나 눌러 시작하세요", skin.Centered(32, FontStyle.Bold));
            if (GesturePointer.Activate(prompt,"title-start")) open?.Invoke();
            GUI.color = new Color(1f, 1f, 1f, .8f * fade);
            if (GesturePointer.Enabled) GUI.Label(new Rect(340, 686, 600, 24), "손으로 이 문구를 잠시 가리켜도 시작합니다", skin.Centered(16, FontStyle.Normal));
            // Clicking anywhere starts the game, so settings open with a key here.
            GUI.color = new Color(1f, 1f, 1f, .7f * fade); GUI.Label(new Rect(1040, 20, 220, 30), "F1  설정", skin.Style(18, FontStyle.Bold));
        }
    }
}
