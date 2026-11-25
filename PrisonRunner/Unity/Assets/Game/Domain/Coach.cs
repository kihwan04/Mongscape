using System;

namespace Muhanok.Domain
{
    public enum CoachLine { None, PaceUp, KneesHigher, DuckDeeper, PraiseDeep, PraiseHigh, PraiseKnee, PraiseSide, PraiseSprint }

    // Short coach subtitles during a run (exercise plan 10): encouragement tied to the movement depth and the pace, never a
    // grade or a combo. One line at a time, at most one every GlobalGap seconds, the same line not again within LineGap
    // (a lagging pace may repeat sooner). Praise comes for the first big move of a kind and then every PraiseEvery-th.
    public sealed class Coach
    {
        public const float ShowSeconds = 2f, GlobalGap = 6f, LineGap = 20f, PaceGap = 10f, SprintSeconds = 5f;
        public const int ShallowKnees = 3, ShallowCrouches = 2, PraiseEvery = 4;
        public struct Input
        {
            public bool Lagging, OnStairs, KneeStep, BigKnee, CrouchStart, CrouchEnd, CrouchDeep, DeepStart, BigJump, BigLane, Sprinting;
        }
        public CoachLine Line { get; private set; }
        public float ShownAt { get; private set; } = -100;
        public bool Showing(float now) => Line != CoachLine.None && now - ShownAt < ShowSeconds;
        private readonly float[] lastShown = new float[9];
        private readonly int[] praised = new int[9];
        private float sprintFor;
        private int shallowKnees, shallowCrouches;
        private bool deepThisCrouch;
        public Coach() { Reset(); }
        public void Reset()
        {
            Line = CoachLine.None; ShownAt = -100; sprintFor = 0; shallowKnees = shallowCrouches = 0; deepThisCrouch = false;
            for (int i = 0; i < lastShown.Length; i++) { lastShown[i] = -100; praised[i] = 0; }
        }
        // Returns the line started this tick (None otherwise).
        public CoachLine Tick(float now, float dt, Input input)
        {
            sprintFor = input.Sprinting ? sprintFor + Math.Max(0, dt) : 0;
            if (input.CrouchStart) deepThisCrouch = false;
            if (input.CrouchDeep) deepThisCrouch = true;
            CoachLine want = CoachLine.None;
            // Never "faster" on the stairs: the stair exercise is slower than running and the pace rests there (QA 2026-11-25 H2).
            if (input.Lagging && !input.OnStairs) want = CoachLine.PaceUp;
            if (input.OnStairs && input.KneeStep && !input.BigKnee && ++shallowKnees >= ShallowKnees && want == CoachLine.None) want = CoachLine.KneesHigher;
            if (input.CrouchEnd)
            {
                if (deepThisCrouch) shallowCrouches = 0;
                else if (++shallowCrouches >= ShallowCrouches && want == CoachLine.None) want = CoachLine.DuckDeeper;
            }
            if (want == CoachLine.None && input.BigKnee) { shallowKnees = 0; want = Praise(CoachLine.PraiseKnee); }
            if (want == CoachLine.None && input.DeepStart) want = Praise(CoachLine.PraiseDeep);
            if (want == CoachLine.None && input.BigJump) want = Praise(CoachLine.PraiseHigh);
            if (want == CoachLine.None && input.BigLane) want = Praise(CoachLine.PraiseSide);
            if (want == CoachLine.None && sprintFor >= SprintSeconds) want = CoachLine.PraiseSprint;
            if (want == CoachLine.None || Showing(now) || now - ShownAt < GlobalGap && want != CoachLine.PaceUp) return CoachLine.None;
            if (now - lastShown[(int)want] < (want == CoachLine.PaceUp ? PaceGap : LineGap)) return CoachLine.None;
            if (want == CoachLine.KneesHigher) shallowKnees = 0;
            if (want == CoachLine.DuckDeeper) shallowCrouches = 0;
            if (want == CoachLine.PraiseSprint) sprintFor = 0;
            Line = want; ShownAt = now; lastShown[(int)want] = now;
            return want;
        }
        // Counts the praise and asks for it the first time and every PraiseEvery-th time.
        private CoachLine Praise(CoachLine line) { int n = ++praised[(int)line]; return n == 1 || n % PraiseEvery == 0 ? line : CoachLine.None; }

        public static string Text(CoachLine line, PlayStyle style)
        {
            bool upper = style == PlayStyle.UpperBody;
            switch (line)
            {
                case CoachLine.PaceUp: return upper ? "경찰이 온다, 팔을 더 빨리!" : "경찰이 온다, 속도 올려!";
                case CoachLine.KneesHigher: return upper ? "팔을 어깨 아래까지 당겨요!" : "무릎 더 높이!";
                case CoachLine.DuckDeeper: return upper ? "상체를 더 깊이 숙여요!" : "더 깊게 앉아 보세요!";
                case CoachLine.PraiseDeep: return upper ? "깊게 숙였어요!" : "깊게! 코인 줄 끝까지";
                case CoachLine.PraiseHigh: return upper ? "팔을 쭉 뻗었어요!" : "높이 떴어요!";
                case CoachLine.PraiseKnee: return upper ? "끝까지 당겼어요!" : "계단 두 칸씩!";
                case CoachLine.PraiseSide: return upper ? "크게 기울였어요!" : "옆으로 크게, 좋아요!";
                case CoachLine.PraiseSprint: return "전력 질주, 좋아요!";
                default: return "";
            }
        }
    }
}
