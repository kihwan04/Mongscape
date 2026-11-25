using System;

namespace Muhanok.Domain
{
    public enum BonusCoin { None, UnderBar, UnderBarDeep, Air }

    // Coins a big movement earns (exercise plan 6), placed on the course instead of a coin credited on the spot:
    // a low coin line under each duck row (the bars), whose far half only a deep slide reaches, and a few coins over each
    // jump row, above a normal jump's reach and inside a big jump's.
    public static class BonusCoins
    {
        // A coin is taken within this distance of the runner's chest (CoinController: the runner's y + ChestHeight).
        public const float ChestHeight = .85f, ReachX = .8f, ReachY = .9f;
        // Under the bars: CrouchCount coins every CrouchSpacing m from CrouchFrom m before the row; the last CrouchDeep need a deep slide.
        public const int CrouchCount = 6, CrouchDeep = 3;
        public const float CrouchSpacing = 1.5f, CrouchFrom = -1.5f, CrouchHeight = .45f;
        // Over a jump row: AirCount coins AirSpacing m apart, centred on the row.
        public const int AirCount = 3;
        public const float AirSpacing = 1.2f, AirMargin = .1f;

        // Height over the ground just out of a normal jump's reach (runner Y = JumpHeight).
        public static float AirHeight(RunConfig config) => config.JumpHeight + ChestHeight + ReachY + AirMargin;
        public static float CrouchZ(int index) => CrouchFrom + CrouchSpacing * index;
        public static float AirZ(int index) => (index - (AirCount - 1) * .5f) * AirSpacing;
        public static BonusCoin CrouchKind(int index) => index >= CrouchCount - CrouchDeep ? BonusCoin.UnderBarDeep : BonusCoin.UnderBar;
        // The runner may take the coin now: under-bar coins only while sliding (the far ones only in a deep slide).
        public static bool Reachable(BonusCoin kind, bool crouching, bool deepSliding)
            => kind == BonusCoin.UnderBar ? crouching : kind == BonusCoin.UnderBarDeep ? crouching && deepSliding : true;
        // Whether a runner at height y (over the ground) is close enough in height to a coin at coinHeight.
        public static bool HeightReach(float runnerY, float coinHeight) => Math.Abs(runnerY + ChestHeight - coinHeight) <= ReachY;
    }
}
