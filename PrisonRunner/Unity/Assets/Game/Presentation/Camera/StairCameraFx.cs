using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Camera feel on a stair chunk: the view pitches up a few degrees (eased over about 0.4 s) and every KneeStep adds a short head bob.
    // Plain data so tests can read Pitch/Bob; the camera rig only applies them.
    public sealed class StairCameraFx
    {
        public const float PitchDegrees = 6f, EaseSeconds = .4f, BobMetres = .05f, BobSeconds = .18f;
        private int lastSteps;
        private float blend, bobLeft;
        // Degrees to tilt up, 0 off the stairs.
        public float Pitch => Mathf.SmoothStep(0f, PitchDegrees, blend);
        // Vertical offset in metres of the current step bob.
        public float Bob => bobLeft > 0f ? BobMetres * Mathf.Sin(Mathf.Clamp01(1f - bobLeft / BobSeconds) * Mathf.PI) : 0f;
        // True on the frame a KneeStep landed on the stairs (the footstep sound listens to this).
        public bool Stepped { get; private set; }
        public void Tick(float dt, bool onStair, int kneeSteps)
        {
            Stepped = onStair && kneeSteps != lastSteps;
            lastSteps = kneeSteps;
            if (Stepped) bobLeft = BobSeconds;
            else bobLeft = Mathf.Max(0f, bobLeft - dt);
            blend = Mathf.MoveTowards(blend, onStair ? 1f : 0f, dt / EaseSeconds);
        }
        public void Reset(int kneeSteps = 0) { blend = bobLeft = 0f; Stepped = false; lastSteps = kneeSteps; }
        // The runner is on a walkable stair chunk (not in the cart).
        public static bool OnStairChunk(RunnerController runner)
            => !runner.Riding && RouteSurface.IsStair(RouteSurface.TypeAt(RouteSurface.SequenceAt(runner.Z)));
    }
}
