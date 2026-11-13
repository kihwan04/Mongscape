using System.Collections.Generic;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Shared IMGUI resources. Styles are created once inside OnGUI and reused every frame.
    // Colors follow the overlay palette (wood and gold); every label uses the one game font (UiFont).
    internal sealed class HudSkin
    {
        // Smallest text size on the 1280x720 canvas; the heavy game font blurs below it.
        public const int MinSize = 16;
        public Font Font => UiFont.Main;
        private readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
        private readonly Dictionary<int, GUIStyle> centeredStyles = new Dictionary<int, GUIStyle>();
        private GUIStyle button, flatButton, panelStyle, inputStyle;
        private Texture2D panelTexture, coinIcon, distanceIcon;
        public GUIStyle Large => Style(74, FontStyle.Bold);
        public GUIStyle Small => Style(20, FontStyle.Normal);
        public GUIStyle Label => Style(25, FontStyle.Bold);
        public GUIStyle Button => button ??= new GUIStyle(GUI.skin.button) { font = Font, fontSize = 22 };
        public GUIStyle Input => inputStyle ??= InputStyle();
        public Texture2D CoinIcon => coinIcon ??= CreateHudIcon(false);
        public Texture2D DistanceIcon => distanceIcon ??= CreateHudIcon(true);
        // The game font has one heavy weight, so "Bold" never adds a synthetic bold; the weight only keeps call sites readable.
        public GUIStyle Style(int size, FontStyle weight)
        {
            int key = Mathf.Max(size, MinSize);
            if (!styles.TryGetValue(key, out var style))
                styles[key] = style = new GUIStyle(GUI.skin.label) { font = Font, fontSize = key, fontStyle = FontStyle.Normal, normal = { textColor = Color.white } };
            return style;
        }
        // Wooden frame with a dark fill, like the overlay screens.
        public void Panel(Rect r, float alpha = .9f)
        {
            Card(r, Palette.A(Palette.Wood, alpha));
            Card(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), Palette.A(Palette.Ground, alpha));
        }
        private GUIStyle InputStyle()
        {
            var style = new GUIStyle(GUI.skin.textField) { font = Font, fontSize = 30, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 0, 0) };
            style.normal.background = style.focused.background = style.hover.background = null;
            style.normal.textColor = style.focused.textColor = style.hover.textColor = Palette.Cream;
            return style;
        }
        public GUIStyle Centered(int size, FontStyle weight)
        {
            int key = Mathf.Max(size, MinSize);
            if (!centeredStyles.TryGetValue(key, out var style))
                centeredStyles[key] = style = new GUIStyle(Style(size, weight)) { alignment = TextAnchor.MiddleCenter };
            return style;
        }
        public void Card(Rect r, Color color)
        {
            if (panelStyle == null)
            {
                panelTexture = new Texture2D(48, 48, TextureFormat.RGBA32, false); panelTexture.hideFlags = HideFlags.DontSave;
                panelTexture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
                {
                    float dx = Mathf.Max(12 - x, x - 35), dy = Mathf.Max(12 - y, y - 35);
                    float distance = Mathf.Sqrt(Mathf.Max(0, dx) * Mathf.Max(0, dx) + Mathf.Max(0, dy) * Mathf.Max(0, dy));
                    float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(11, 12, distance));
                    panelTexture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                panelTexture.Apply(); panelStyle = new GUIStyle(); panelStyle.normal.background = panelTexture; panelStyle.border = new RectOffset(12, 12, 12, 12);
            }
            var old = GUI.color; GUI.color = color; GUI.Box(r, GUIContent.none, panelStyle); GUI.color = old;
        }
        // Main call to action: gold with dark text, brighter on hover and glowing gently.
        public bool PrimaryButton(Rect rect, string text, int size = 28)
        {
            bool hover = rect.Contains(Event.current.mousePosition) || (GesturePointer.Visible && rect.Contains(GesturePointer.Position));
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 3f);
            Card(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6), Palette.A(Palette.Gold, .35f + .3f * pulse));
            Card(rect, hover ? Color.Lerp(Palette.Gold, Color.white, .15f) : Palette.Gold);
            var label = Centered(size, FontStyle.Bold);
            GUI.color = Palette.Outline; GUI.Label(rect, text, label);
            bool pressed = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.color = Color.white; return pressed || GesturePointer.Activate(rect, text);
        }
        // Secondary actions: dark fill with a wooden frame; "primary" turns it gold.
        public bool FlatButton(Rect rect, string text, bool primary)
        {
            if (flatButton == null)
            {
                flatButton = new GUIStyle(Button);
                foreach (var state in new[] { flatButton.normal, flatButton.hover, flatButton.active, flatButton.focused }) { state.background = null; state.textColor = Color.white; }
            }
            bool hover = rect.Contains(Event.current.mousePosition) || (GesturePointer.Visible && rect.Contains(GesturePointer.Position));
            if (primary) Card(rect, hover ? Color.Lerp(Palette.Gold, Color.white, .15f) : Palette.Gold);
            else { Card(rect, Palette.Wood); Card(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), hover ? Color.Lerp(Palette.Ground, Color.white, .12f) : Palette.Ground); }
            GUI.color = primary ? Palette.Outline : Palette.Cream;
            bool pressed = GUI.Button(rect, text, flatButton); GUI.color = Color.white; return pressed || GesturePointer.Activate(rect,text);
        }
        public void Dispose()
        {
            if (panelTexture != null) Object.Destroy(panelTexture);
            if (coinIcon != null) Object.Destroy(coinIcon);
            if (distanceIcon != null) Object.Destroy(distanceIcon);
        }
        private static Texture2D CreateHudIcon(bool crown)
        {
            var image = new Texture2D(64, 64, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float dx = x - 31.5f, dy = y - 31.5f, radius = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(29, 30, radius));
                Color color = radius > 25 ? new Color(1, .87f, .30f) : radius > 22 ? new Color(.72f, .40f, .035f) : new Color(1, .71f, .055f);
                if (radius < 17) color = new Color(1, .78f, .10f);
                if (radius > 16 && radius < 18) color = new Color(1, .91f, .43f);
                if (crown)
                {
                    float top = 46 - Mathf.Min(Mathf.Abs(x - 13), Mathf.Min(Mathf.Abs(x - 32), Mathf.Abs(x - 51))) * 1.55f;
                    alpha = x >= 10 && x <= 53 && y >= 12 && y <= top ? 1 : 0;
                    color = y < 18 ? new Color(.85f, .49f, .03f) : new Color(1, .78f, .11f);
                    if ((x - 32) * (x - 32) + (y - 23) * (y - 23) < 12) color = new Color(1, .94f, .62f);
                }
                color.a = alpha; image.SetPixel(x, y, color);
            }
            image.Apply(); return image;
        }
    }
    // Rebuilds a label only when the displayed value changes.
    internal sealed class CachedText
    {
        private long key = long.MinValue;
        private string text = "";
        public string Get(long value, System.Func<long, string> format)
        {
            if (value != key) { key = value; text = format(value); }
            return text;
        }
    }
}
