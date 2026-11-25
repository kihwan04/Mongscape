using System;

namespace Muhanok.Domain
{
    // Hazards are spaced in time, not distance: chunks are planned up to 96 m ahead with the speed expected there, and a
    // jump row (or an animal to jump) closer than JumpGapSeconds to the previous jump is left out, so a faster runner gets
    // the same time between jumps. At most StreakMax jumps run back to back; the next one needs BreakSeconds more, unless
    // another action (duck, side step, stairs, the cart) came in between.
    public sealed class HazardPacer
    {
        public const float JumpGapSeconds = 1.2f, BreakSeconds = 1.5f;
        public const int StreakMax = 3;
        // Jumps this close (seconds) belong to one streak.
        public const float StreakSeconds = JumpGapSeconds + BreakSeconds;
        private float lastJump = float.NaN, lastOther = float.NaN, lastJumpSpeed = 1f;
        public int Streak { get; private set; }
        public int Dropped { get; private set; }
        public void Reset() { lastJump = lastOther = float.NaN; Streak = 0; Dropped = 0; lastJumpSpeed = 1f; }
        private bool OtherSinceJump => !float.IsNaN(lastOther) && (float.IsNaN(lastJump) || lastOther >= lastJump);
        private float Seconds(float z, float speed) => float.IsNaN(lastJump) ? float.MaxValue : (z - lastJump) / Math.Max(1f, speed);
        // Whether a jump at route z (metres) may stay; the planning speed is in metres per second.
        public bool AllowJump(float z, float speed)
        {
            float gap = Seconds(z, speed);
            if (gap < JumpGapSeconds) return false;
            if (Streak >= StreakMax && !OtherSinceJump && gap < StreakSeconds) return false;
            return true;
        }
        // Keeps the jump when it is allowed (and counts it); returns whether it stays.
        public bool TryJump(float z, float speed)
        {
            if (!AllowJump(z, speed)) { Dropped++; return false; }
            float gap = Seconds(z, speed);
            Streak = OtherSinceJump || gap >= StreakSeconds ? 1 : Streak + 1;
            lastJump = z; lastJumpSpeed = speed; return true;
        }
        // A jump that stays whatever the gap (the opening row, map terrain): it still starts or extends the streak.
        public void Jump(float z)
        {
            float gap = float.IsNaN(lastJump) ? float.MaxValue : (z - lastJump) / Math.Max(1f, lastJumpSpeed);
            Streak = OtherSinceJump || gap >= StreakSeconds ? 1 : Streak + 1; lastJump = z;
        }
        // Any other action (or a stretch without hazards such as stairs or the cart) at route z.
        public void Other(float z) { lastOther = z; }
    }
}
