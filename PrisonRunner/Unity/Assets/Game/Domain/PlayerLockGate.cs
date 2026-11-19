using System;
namespace Muhanok.Domain
{
    // Exhibition player lock (off unless switched on): remembers where the tracked player stands and how big the body is,
    // and only while the body is being searched for again right after a loss it
    //  - limits the search to a band around the player's last position (people further to the side are not seen), and
    //  - rejects a much smaller body (someone standing well behind the player).
    // The reference follows the player continuously, so stepping forward or back while tracked never locks the player out,
    // and it expires LockSeconds after the player was last tracked: then anyone can be found again, as without the lock.
    // Units are camera pixels; size is the body's hip-to-top alignment length.
    public sealed class PlayerLockGate
    {
        public const double LockSeconds = 3;
        // Band half width in body sizes around the last hip position (lanes are side steps of well under one body size).
        public const float HalfWidth = 1.2f, MinHalfWidthOfFrame = .15f;
        // A new body smaller than this share of the player's last size is someone farther away.
        public const float MinScale = .55f;
        // Share of each tracked frame mixed into the reference (about a third of a second to follow a step).
        public const float Follow = .2f;
        private bool known;
        private double seen = -10;
        public float X { get; private set; }
        public float Size { get; private set; }
        public int Rejected { get; private set; }

        public void Track(float hipX, float size, double now)
        {
            if (size <= 1f) return;
            if (!known || now - seen > LockSeconds) { X = hipX; Size = size; known = true; }
            else { X += (hipX - X) * Follow; Size += (size - Size) * Follow; }
            seen = now;
        }
        public bool Active(double now) => known && now >= seen && now - seen <= LockSeconds;
        // Visible band in normalised image x (0..1) for a frame of this width.
        public void Band(float frameWidth, out float min, out float max)
        {
            float half = Math.Max(HalfWidth * Size, MinHalfWidthOfFrame * frameWidth);
            min = (X - half) / frameWidth; max = (X + half) / frameWidth;
        }
        public bool Accepts(float size, double now)
        {
            if (!Active(now) || size >= MinScale * Size) return true;
            Rejected++; return false;
        }
        public void Reset() { known = false; seen = -10; Rejected = 0; }
    }
}
