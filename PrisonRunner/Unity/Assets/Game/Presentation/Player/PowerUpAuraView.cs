using Muhanok.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Muhanok.Presentation
{
    // Item feedback on the runner: a see-through shield bubble that bursts when it absorbs a hit, and sparks circling while the magnet works.
    // Built from primitives with its own unlit transparent materials; in first person the camera sits inside the bubble and sees a light tint.
    public sealed class PowerUpAuraView : MonoBehaviour
    {
        public const float BurstSeconds = .45f;
        private const float BubbleSize = 2.1f;
        private static readonly Color ShieldTint = new Color(.35f, .72f, 1f, .16f), SparkTint = new Color(1f, .32f, .25f, .85f);
        private RunSession session;
        private Transform bubble, magnet;
        private Material bubbleMaterial, sparkMaterial;
        private Mesh sphere;
        private int lastSaves;
        private float burst;
        public bool ShieldVisible => bubble != null && bubble.gameObject.activeSelf;
        public bool MagnetVisible => magnet != null && magnet.gameObject.activeSelf;
        public bool Bursting => burst > 0;
        public void Configure(RunSession run, RunnerView hero)
        {
            session = run;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null || hero == null) return;
            bubbleMaterial = new Material(shader) { color = ShieldTint };
            sparkMaterial = new Material(shader) { color = SparkTint };
            bubble = Part(PrimitiveType.Sphere, hero.transform, new Vector3(0, .95f, 0), Vector3.one * BubbleSize, bubbleMaterial); bubble.name = "ShieldBubble";
            magnet = new GameObject("MagnetSparks").transform; magnet.SetParent(hero.transform, false); magnet.localPosition = new Vector3(0, 1.15f, 0);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                Part(PrimitiveType.Sphere, magnet, new Vector3(Mathf.Cos(angle), (i % 2) * .18f - .09f, Mathf.Sin(angle)), Vector3.one * .14f, sparkMaterial);
            }
            bubble.gameObject.SetActive(false); magnet.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if (session == null || bubble == null) return;
            bool live = session.Flow.State == GameFlowState.Running;
            if (session.ShieldSaves > lastSaves) burst = BurstSeconds;
            lastSaves = session.ShieldSaves;
            if (!live) burst = 0;
            bool shield = live && session.Items.IsActive(ItemKind.Shield);
            bubble.gameObject.SetActive(shield || burst > 0);
            if (burst > 0)
            {
                // Breaking: a quick swell while it fades out.
                float t = 1 - burst / BurstSeconds;
                bubble.localScale = Vector3.one * BubbleSize * (1 + .7f * t);
                bubbleMaterial.color = new Color(ShieldTint.r, ShieldTint.g, ShieldTint.b, .45f * (1 - t));
                burst = Mathf.Max(0, burst - Time.deltaTime);
            }
            else if (shield)
            {
                bubble.localScale = Vector3.one * BubbleSize;
                bubbleMaterial.color = new Color(ShieldTint.r, ShieldTint.g, ShieldTint.b, ShieldTint.a * (.8f + .2f * Mathf.Sin(Time.time * 4)));
            }
            bool pulling = live && session.Items.IsActive(ItemKind.Magnet);
            magnet.gameObject.SetActive(pulling);
            if (pulling) magnet.localRotation = Quaternion.Euler(0, Time.time * 240, 0);
        }
        private Transform Part(PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            var collider = part.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            // The sprite shader multiplies by vertex colour, which primitive meshes do not carry: use a white-coloured copy.
            var filter = part.GetComponent<MeshFilter>();
            if (sphere == null) { sphere = Instantiate(filter.sharedMesh); sphere.name = "PowerUpAuraSphere"; var white = new Color[sphere.vertexCount]; for (int i = 0; i < white.Length; i++) white[i] = Color.white; sphere.colors = white; }
            filter.sharedMesh = sphere;
            var renderer = part.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return part.transform;
        }
        private void OnDestroy() { if (bubbleMaterial != null) Destroy(bubbleMaterial); if (sparkMaterial != null) Destroy(sparkMaterial); if (sphere != null) Destroy(sphere); }
    }
}
