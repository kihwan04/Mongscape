using UnityEngine;

namespace Muhanok.Presentation
{
    // The one typeface for every screen: IMGUI menus and HUD, the UI Toolkit settings window, the debug overlay and world signs.
    // Glyphs Black Han Sans lacks (arrows, symbols) come from the OS fonts listed as fallbacks in its import settings.
    public static class UiFont
    {
        public const string ResourcePath = "MuhanokUI/BlackHanSans-Regular";
        private static Font main;
        public static Font Main
        {
            get
            {
                if (main != null) return main;
                main = Resources.Load<Font>(ResourcePath);
                // Only a broken import gets here; keep text readable rather than invisible.
                if (main == null) main = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Arial" }, 32);
                return main;
            }
        }
    }
}
