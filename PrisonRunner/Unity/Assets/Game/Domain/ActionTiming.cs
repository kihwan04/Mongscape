using System;

namespace Muhanok.Domain
{
    // When to call "점프!" and "숙이기!" so that acting on the call clears the obstacle.
    // An obstacle is judged at the instant the runner reaches it: a jump must still be above the clearance height then,
    // and a crouch must still be held. The call therefore comes at "action time before the obstacle" plus the time the player
    // needs to react (and, with a camera, for the pose to register). Calling it earlier makes an obedient jump land short.
    public static class ActionTiming
    {
        // Seconds from the call to the game receiving the action: reading and reacting, plus pose detection for camera play.
        public const float KeyboardLead = .3f, CameraLead = .45f;
        // A jump clears a jump obstacle above this height (SafePatternValidator: crates and carts).
        public const float JumpClearance = .9f;
        public static float Lead(PlayStyle style) => style == PlayStyle.Keyboard ? KeyboardLead : CameraLead;

        // Seconds after take-off during which the runner is above the clearance height.
        public static void JumpClearWindow(RunConfig config, out float enter, out float exit)
        {
            float g = Math.Max(.01f, config.Gravity), v = (float)Math.Sqrt(2f * g * config.JumpHeight);
            float disc = v * v - 2f * g * JumpClearance;
            if (disc <= 0) { enter = exit = v / g; return; }
            float root = (float)Math.Sqrt(disc);
            enter = (v - root) / g; exit = (v + root) / g;
        }
        // Take-off this long before the obstacle sits in the middle of the clear window.
        public static float JumpTakeoff(RunConfig config) { JumpClearWindow(config, out float enter, out float exit); return (enter + exit) * .5f; }
        // A single crouch lasts CrouchSeconds; starting it halfway through that before the obstacle leaves margin both ways.
        public static float CrouchStart(RunConfig config) => Math.Max(.1f, config.CrouchSeconds) * .5f;

        public static float JumpCueSeconds(RunConfig config, PlayStyle style) => JumpTakeoff(config) + Lead(style);
        public static float CrouchCueSeconds(RunConfig config, PlayStyle style) => CrouchStart(config) + Lead(style);
        // The call is due once the runner is within that many seconds of the obstacle at its current speed.
        public static bool Due(float distance, float speed, float cueSeconds) => distance >= 0 && distance <= Math.Max(1f, speed) * cueSeconds;
    }
}
