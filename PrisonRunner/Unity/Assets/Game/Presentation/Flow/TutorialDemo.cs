using UnityEngine;

namespace Muhanok.Presentation
{
    // The tutorial's "이렇게 하세요" window (exercise plan 10-3, method A): a copy of the monkey far below the map plays the
    // step's clip in a loop, seen by its own camera into a render texture that the tutorial draws in the bottom-left corner
    // (the player's own pose window is bottom-right, so the two can be compared). Only clips the monkey already has are used.
    public sealed class TutorialDemo : MonoBehaviour
    {
        public static readonly Vector3 Stage = new Vector3(0, -400, 0);
        // One-shot clips start again after this long so the move repeats.
        public const float Replay = 1.6f;
        private RenderTexture target;
        private Camera view;
        private Animator animator;
        private GameObject actor;
        private string clip;
        private float playedAt;
        public Texture Texture => clip != null && target != null ? target : null;
        public string Clip => clip;
        public void Configure(GameObject prefab)
        {
            if (prefab == null) return;
            var root = new GameObject("TutorialDemoStage"); root.transform.position = Stage; root.transform.SetParent(transform, true);
            actor = Instantiate(prefab, root.transform); actor.name = "TutorialDemoMonkey";
            actor.transform.localPosition = Vector3.zero; actor.transform.localRotation = Quaternion.identity;
            foreach (var c in actor.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            animator = actor.GetComponentInChildren<Animator>();
            target = new RenderTexture(384, 384, 16) { name = "TutorialDemo", hideFlags = HideFlags.DontSave };
            var eye = new GameObject("TutorialDemoCamera"); eye.transform.SetParent(root.transform, false);
            // In front of the monkey (it faces +z), a little to the side so arms and knees read.
            eye.transform.localPosition = new Vector3(1f, 1.2f, 3.9f); eye.transform.LookAt(Stage + new Vector3(0, .9f, 0));
            view = eye.AddComponent<Camera>(); view.targetTexture = target; view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.13f, .1f, .08f); view.nearClipPlane = .1f; view.farClipPlane = 8f; view.fieldOfView = 38f; view.enabled = false;
            var lamp = new GameObject("TutorialDemoLight").AddComponent<Light>(); lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(1.2f, 2.5f, 2.5f); lamp.type = LightType.Point; lamp.range = 8; lamp.intensity = 3; lamp.color = new Color(1, .9f, .8f);
            actor.SetActive(false);
        }
        // null hides the window and stops the camera.
        public void Show(string next)
        {
            if (next == clip) return;
            clip = next;
            // An inactive animator knows no states: wake the actor before asking.
            if (actor != null && clip != null) actor.SetActive(true);
            bool on = clip != null && animator != null && animator.HasState(0, Animator.StringToHash(clip));
            if (!on) clip = null;
            if (actor != null) actor.SetActive(on);
            if (view != null) view.enabled = on;
            if (on) { animator.Play(clip, 0, 0); playedAt = Time.unscaledTime; }
        }
        private void Update()
        {
            if (clip == null || animator == null) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.loop && Time.unscaledTime - playedAt >= Replay) { animator.Play(clip, 0, 0); playedAt = Time.unscaledTime; }
        }
        private void OnDestroy() { if (target != null) { target.Release(); Destroy(target); } }
    }
}
