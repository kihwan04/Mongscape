using System;

namespace Muhanok.Domain
{
    // Turns the classifier's running state into counted moves for the result report (steps, jumps, crouches, knee steps,
    // lane changes), and says whether the player did anything other than stepping just now (that counts as walking).
    public sealed class MoveTally
    {
        public const double MovingSeconds = .7;
        private int lastSteps = -1, lastLane;
        private double lastJump = double.NaN, lastKnee = double.NaN, lastMove = -10;
        private bool lastCrouch;
        public void Reset() { lastSteps = -1; lastLane = 0; lastJump = lastKnee = double.NaN; lastMove = -10; lastCrouch = false; }
        public bool Moving(double now) => now - lastMove < MovingSeconds;
        // The first observation only sets the starting point; later ones add what changed since.
        public void Observe(double now, int steps, double jumpTime, bool crouch, double kneeTime, int lane, WorkoutEstimate workout)
        {
            bool first = lastSteps < 0;
            if (!first && workout != null)
            {
                if (steps > lastSteps) workout.CountMove(MoveKind.Step, steps - lastSteps);
                if (jumpTime > lastJump) { workout.CountMove(MoveKind.Jump); lastMove = now; }
                if (crouch && !lastCrouch) workout.CountMove(MoveKind.Crouch);
                if (kneeTime > lastKnee) { workout.CountMove(MoveKind.KneeStep); lastMove = now; }
                if (lane != lastLane) { workout.CountMove(MoveKind.SideStep); lastMove = now; }
            }
            if (crouch) lastMove = now;
            lastSteps = steps; lastJump = jumpTime; lastCrouch = crouch; lastKnee = kneeTime; lastLane = lane;
        }
    }
}
