using System;

namespace Muhanok.Domain
{
    // Two-chunk junction: choose while the tracks open, then turn directly into the destination entrance.
    public static class CartFork
    {
        public const int FirstChunk = 4, LastChunk = 5, ConnectorChunk = 6, DestinationChunk = 7;
        public const float Start = 96, ChoiceOpen = 120, Decision = 120, SplitEnd = 144, End = 144, Spacing = 6.6f;
        // Zero velocity and acceleration at each join prevent a lateral jerk as the choice commits.
        private static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*t*(t*(t*6-15)+10);}
        public static float Offset(float lapDistance, int branch)
        {
            if (lapDistance <= Start) return 0;
            if (lapDistance >= SplitEnd) return Math.Max(-1,Math.Min(1,branch))*Spacing;
            float spread=lapDistance<ChoiceOpen?2.2f*Smooth((lapDistance-Start)/(ChoiceOpen-Start))
                :2.2f+(Spacing-2.2f)*Smooth((lapDistance-Decision)/(SplitEnd-Decision));
            return Math.Max(-1, Math.Min(1, branch)) * spread;
        }
    }
}
