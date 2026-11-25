using UnityEngine;

namespace Muhanok.Presentation.Map
{
    // Banana peels read as danger (exercise plan 10-1): a red-orange glowing ring that pulses about once a second, a
    // translucent red disc on the floor so a low peel still shows where it is, and a small "!" above it so the warning does
    // not rely on colour alone. Never white or yellow: those read as something to collect, like coins.
    // The colour-blind setting makes the ring thicker.
    public sealed class HazardMarker : MonoBehaviour
    {
        public static readonly Color Danger = new Color(1f, .27f, .08f);
        public const float PulseSeconds = 1f;
        // Settings: 색약 모드 (thicker ring).
        public static bool ThickOutline;
        private static Material ringMaterial, discMaterial;
        private static Mesh thinRing, thickRing;
        private static int pulsedFrame = -1;
        private MeshFilter ring;
        private Transform mark;
        private bool thick;
        private Camera view;

        public static HazardMarker Attach(GameObject target)
        {
            var existing = target.GetComponentInChildren<HazardMarker>(true);
            if (existing != null) return existing;
            var node = new GameObject("DangerMarker"); node.transform.SetParent(target.transform, false);
            return node.AddComponent<HazardMarker>();
        }
        private void Awake()
        {
            Materials();
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); disc.name = "DangerDisc";
            Destroy(disc.GetComponent<Collider>()); disc.transform.SetParent(transform, false);
            disc.transform.localPosition = new Vector3(0, .012f, .1f); disc.transform.localScale = new Vector3(1.5f, .004f, 1.5f);
            Setup(disc.GetComponent<Renderer>(), discMaterial);
            var ringNode = new GameObject("DangerRing"); ringNode.transform.SetParent(transform, false); ringNode.transform.localPosition = new Vector3(0, .03f, .1f);
            ring = ringNode.AddComponent<MeshFilter>(); Setup(ringNode.AddComponent<MeshRenderer>(), ringMaterial);
            var text = new GameObject("DangerMark"); text.transform.SetParent(transform, false); text.transform.localPosition = new Vector3(0, .8f, .1f);
            var mesh = text.AddComponent<TextMesh>(); mesh.text = "!"; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 96; mesh.characterSize = .05f; mesh.color = Danger; mesh.fontStyle = FontStyle.Bold;
            var font = Muhanok.Presentation.UiFont.Main; if (font != null) { mesh.font = font; text.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
            mark = text.transform;
            thick = !ThickOutline; Refresh();
        }
        private static void Setup(Renderer renderer, Material material) { renderer.sharedMaterial = material; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false; }
        private void Refresh() { if (thick == ThickOutline) return; thick = ThickOutline; ring.sharedMesh = thick ? thickRing : thinRing; }
        private void LateUpdate()
        {
            Refresh();
            // One pulse update a frame for every marker (shared material).
            if (pulsedFrame != Time.frameCount)
            {
                pulsedFrame = Time.frameCount;
                float glow = .55f + .45f * Mathf.Sin(Time.time * Mathf.PI * 2 / PulseSeconds);
                ringMaterial.color = Color.Lerp(Danger * .6f, Danger, glow);
                // Kept under the bloom threshold so the ring stays red instead of flaring white.
                ringMaterial.SetColor("_EmissionColor", Danger * Mathf.Lerp(.45f, .95f, glow));
            }
            if (view == null) view = Camera.main;
            if (view != null) { var to = mark.position - view.transform.position; to.y = 0; if (to.sqrMagnitude > .01f) mark.rotation = Quaternion.LookRotation(to); }
        }
        private static void Materials()
        {
            if (ringMaterial != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            ringMaterial = new Material(shader) { name = "Danger ring", color = Danger, hideFlags = HideFlags.DontSave };
            ringMaterial.EnableKeyword("_EMISSION"); ringMaterial.SetColor("_EmissionColor", Danger * .5f);
            ringMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            discMaterial = new Material(shader) { name = "Danger disc", color = new Color(.95f, .12f, .06f, .5f), hideFlags = HideFlags.DontSave };
            // URP Lit as a transparent surface.
            discMaterial.SetFloat("_Surface", 1); discMaterial.SetFloat("_Blend", 0); discMaterial.SetFloat("_ZWrite", 0);
            discMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); discMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            discMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); discMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            thinRing = Ring(.5f, .7f); thickRing = Ring(.46f, .9f);
        }
        private static Mesh Ring(float inner, float outer)
        {
            const int n = 40; var v = new Vector3[n * 2]; var t = new int[n * 12];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v[i * 2] = d * inner; v[i * 2 + 1] = d * outer;
                int j = (i + 1) % n; int k = i * 6;
                t[k] = i * 2; t[k + 1] = j * 2; t[k + 2] = i * 2 + 1; t[k + 3] = i * 2 + 1; t[k + 4] = j * 2; t[k + 5] = j * 2 + 1;
                // Both faces, so the ring shows whatever the winding.
                int b = n * 6 + k; t[b] = t[k]; t[b + 1] = t[k + 2]; t[b + 2] = t[k + 1]; t[b + 3] = t[k + 3]; t[b + 4] = t[k + 5]; t[b + 5] = t[k + 4];
            }
            // Both faces share the vertices, so the normals are set (all up): recalculated ones would cancel to zero and light as NaN.
            var normals = new Vector3[v.Length]; for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            var mesh = new Mesh { name = "Danger ring", vertices = v, normals = normals, triangles = t, hideFlags = HideFlags.DontSave }; mesh.RecalculateBounds();
            return mesh;
        }
    }
}
