namespace PrisonRunner.Core
{
    public static class LaneRules
    {
        public const int Left = -1;
        public const int Center = 0;
        public const int Right = 1;

        public static int Move(int current, int direction)
        {
            int next = current + direction;
            return next < Left ? Left : next > Right ? Right : next;
        }
    }
}
