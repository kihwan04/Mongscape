using UnityEngine;

namespace Muhanok.Presentation.Map
{
    // Every obstacle to avoid (bars, barricades, crates, carts, lasers) wears the banana's pulsing red-orange glow as an
    // outline round the face the runner sees, so "red glow = avoid" holds for the whole game (exercise plan 10-1). Coins
    // keep their yellow and white. The colour-blind setting (HazardMarker.ThickOutline) thickens the outline.
    public sealed class HazardOutline : MonoBehaviour
    {
        public const float Thin = .06f, Thick = .12f;
        private readonly Transform[] bars = new Transform[4];
        private Bounds box;
        private bool thick;

        public static HazardOutline Attach(GameObject target)
        {
            var existing = target.GetComponentInChildren<HazardOutline>(true);
            if (existing != null) return existing;
            var node = new GameObject("DangerOutline"); node.transform.SetParent(target.transform, false);
            return node.AddComponent<HazardOutline>();
        }
        // Local bounds of the obstacle's own renderers (the outline's bars left out).
        public static bool Measure(Transform root, Transform space, out Bounds bounds)
        {
            bounds = default; bool any = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponentInParent<HazardOutline>(true) != null || renderer is ParticleSystemRenderer) continue;
                var b = renderer.bounds; var c = b.center; var e = b.extents;
                for (int i = 0; i < 8; i++)
                {
                    var p = space.InverseTransformPoint(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                }
            }
            return any && bounds.size.x > .05f && bounds.size.y > .05f;
        }
        private void Start()
        {
            if (!Measure(transform.parent, transform, out box)) { enabled = false; return; }
            var material = HazardMarker.DangerMaterial;
            for (int i = 0; i < 4; i++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube); bar.name = "DangerOutlineBar";
                Destroy(bar.GetComponent<Collider>()); bar.transform.SetParent(transform, false);
                var r = bar.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                bars[i] = bar.transform;
            }
            thick = !HazardMarker.ThickOutline; Layout();
        }
        // Four bars framing the near face (the runner comes from -z), just in front of it.
        private void Layout()
        {
            if (thick == HazardMarker.ThickOutline) return;
            thick = HazardMarker.ThickOutline; float w = thick ? Thick : Thin;
            float z = box.min.z - .03f, x0 = box.min.x, x1 = box.max.x, y0 = Mathf.Max(box.min.y, .02f), y1 = box.max.y;
            Place(bars[0], new Vector3((x0 + x1) * .5f, y1, z), new Vector3(x1 - x0 + w, w, w));
            Place(bars[1], new Vector3((x0 + x1) * .5f, y0, z), new Vector3(x1 - x0 + w, w, w));
            Place(bars[2], new Vector3(x0, (y0 + y1) * .5f, z), new Vector3(w, y1 - y0, w));
            Place(bars[3], new Vector3(x1, (y0 + y1) * .5f, z), new Vector3(w, y1 - y0, w));
        }
        private static void Place(Transform bar, Vector3 at, Vector3 size) { bar.localPosition = at; bar.localRotation = Quaternion.identity; bar.localScale = size; }
        private void LateUpdate() { if (bars[0] == null) return; Layout(); HazardMarker.Pulse(); }
    }
}
