using System.Collections.Generic;
using UnityEngine;

namespace Muhanok.Presentation.Map
{
    // World signs (TextMesh) were baked with Unity's built-in font; each pooled chunk switches them to the game font once when it is created.
    // A sign that would come out wider than before is scaled down so it still fits its board.
    public static class SignFont
    {
        private static readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
        private static bool listening;

        // Play mode without a domain reload keeps statics: start each session with no cached copies or listener.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            foreach (var material in materials.Values) if (material != null) Object.DestroyImmediate(material);
            materials.Clear();
            if (listening) { Font.textureRebuilt -= Rebuilt; listening = false; }
        }

        public static void Apply(GameObject root)
        {
            var font = UiFont.Main;
            if (font == null) return;
            foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
            {
                if (text.font == font) continue;
                float before = Width(text.font, text), after = Width(font, text);
                text.font = font;
                if (before > 0 && after > before) text.characterSize *= before / after;
                var renderer = text.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                var source = renderer.sharedMaterial;
                if (source == null) { renderer.sharedMaterial = font.material; continue; }
                // One copy per sign material, so the shared asset never changes; it samples the game font's glyph atlas.
                if (!materials.TryGetValue(source, out var material))
                    materials[source] = material = new Material(source) { name = source.name + " (game font)", hideFlags = HideFlags.DontSave };
                material.mainTexture = font.material.mainTexture;
                renderer.sharedMaterial = material;
            }
            if (!listening) { listening = true; Font.textureRebuilt += Rebuilt; }
        }

        // The dynamic font grows its atlas as new glyphs are requested; keep the sign materials on the current one.
        private static void Rebuilt(Font font)
        {
            if (font != UiFont.Main) return;
            foreach (var material in materials.Values) if (material != null) material.mainTexture = font.material.mainTexture;
        }

        private static float Width(Font font, TextMesh text)
        {
            if (font == null || string.IsNullOrEmpty(text.text)) return 0;
            int size = text.fontSize > 0 ? text.fontSize : 64;
            font.RequestCharactersInTexture(text.text, size, text.fontStyle);
            float line = 0, widest = 0;
            foreach (char c in text.text)
            {
                if (c == '\n') { widest = Mathf.Max(widest, line); line = 0; continue; }
                if (font.GetCharacterInfo(c, out var info, size, text.fontStyle)) line += info.advance;
            }
            return Mathf.Max(widest, line);
        }
    }
}
