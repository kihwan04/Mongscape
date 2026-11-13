using System;
using System.Collections.Generic;

namespace Muhanok.Domain
{
    public enum TutorialStep { Calibrate, Lane, Jump, Crouch, HighKnee, Rail, Fork, Done }
    // What a step waits for: a calibrated body, a lesson obstacle (or broken rail) passed cleanly, or a lean at the cart fork.
    public enum TutorialPass { Calibrated, ObstaclePassed, ForkChosen, None }
    // The one obstacle each lesson chunk keeps; every random hazard, item and coin is removed.
    public enum TutorialHazard { None, LaneBlock, JumpRow, CrouchRow, StairChunk, RailGap }

    // Lessons for one play style: 보정 → 좌우 이동 → 점프 → 숙이기 → 하이니 → 끊긴 레일 → 갈림길 기울이기 → 완료.
    // A step the style or route lacks is left out (keyboard play needs no calibration; a route without forks has no lean).
    public sealed class TutorialScript
    {
        private readonly List<TutorialStep> steps = new List<TutorialStep>(7);
        public PlayStyle Style { get; }
        public IReadOnlyList<TutorialStep> Steps => steps;
        public int Index { get; private set; }
        public TutorialStep Current => steps[Index];
        public bool Finished => Current == TutorialStep.Done;
        // Retries of the current step and of the whole tutorial.
        public int Retries { get; private set; }
        public int TotalRetries { get; private set; }
        public TutorialScript(PlayStyle style, bool forks = true)
        {
            Style = style;
            foreach (TutorialStep step in Enum.GetValues(typeof(TutorialStep)))
            {
                if (step == TutorialStep.Calibrate && !PlayStyleInfo.UsesCamera(style)) continue;
                if (step == TutorialStep.Fork && !forks) continue;
                steps.Add(step);
            }
        }
        // The step's pass condition was met: the next lesson starts with a fresh retry count.
        public bool Pass() { if (Finished) return false; Index++; Retries = 0; return true; }
        // A mistake restarts the same step in front of its obstacle.
        public void Fail() { if (Finished) return; Retries++; TotalRetries++; }
        public TutorialPass Condition => PassOf(Current);
        public MotionCommand? Awaited => AwaitedOf(Current);

        public static TutorialPass PassOf(TutorialStep step) => step == TutorialStep.Calibrate ? TutorialPass.Calibrated
            : step == TutorialStep.Fork ? TutorialPass.ForkChosen : step == TutorialStep.Done ? TutorialPass.None : TutorialPass.ObstaclePassed;
        // Lane and fork steps accept either side (Left stands for both); the broken rail needs the lean away from the gap.
        public static MotionCommand? AwaitedOf(TutorialStep step) => step == TutorialStep.Lane || step == TutorialStep.Fork ? MotionCommand.Left
            : step == TutorialStep.Rail ? TutorialCourse.RailLean
            : step == TutorialStep.Jump ? MotionCommand.Jump : step == TutorialStep.Crouch ? MotionCommand.Crouch : step == TutorialStep.HighKnee ? MotionCommand.KneeStep : (MotionCommand?)null;
        public static bool Accepts(TutorialStep step, MotionCommand command)
        {
            if (step == TutorialStep.Lane || step == TutorialStep.Fork) return command == MotionCommand.Left || command == MotionCommand.Right;
            var awaited = AwaitedOf(step);
            return awaited.HasValue && awaited.Value == command;
        }
        // The runner already shows the awaited action (from any input source).
        public static bool Performed(TutorialStep step, RunnerController runner)
        {
            switch (step)
            {
                case TutorialStep.Lane: return runner.Lane != TutorialCourse.BlockedLane;
                case TutorialStep.Jump: return runner.Y > .01f;
                case TutorialStep.Crouch: return runner.Crouching;
                case TutorialStep.HighKnee: return runner.KneeStepping;
                // The same lean the rail gap judges (CartRailGap: lean toward the intact rail of at least .55).
                case TutorialStep.Rail: return runner.Riding && runner.CartLean * -TutorialCourse.RailMissingSide >= .55f;
                case TutorialStep.Fork: return runner.Lane != 0;
                default: return false;
            }
        }
        public static string Title(TutorialStep step) => step == TutorialStep.Calibrate ? "자세 맞추기" : step == TutorialStep.Lane ? "옆으로 피하기"
            : step == TutorialStep.Jump ? "점프" : step == TutorialStep.Crouch ? "숙이기" : step == TutorialStep.HighKnee ? "무릎 높이 (하이니)"
            : step == TutorialStep.Rail ? "끊긴 레일 피하기" : step == TutorialStep.Fork ? "갈림길 기울이기" : "튜토리얼 완료";
        public static string Instruction(TutorialStep step, PlayStyle style)
        {
            bool keys = style == PlayStyle.Keyboard, upper = style == PlayStyle.UpperBody;
            switch (step)
            {
                case TutorialStep.Calibrate: return "카메라 앞에 바르게 서 주세요. 자세를 맞추면 출발합니다.";
                case TutorialStep.Lane: return keys ? "A·D(←·→) 키로 옆 줄로 피하세요." : "왼쪽이나 오른쪽 구역으로 몸을 옮겨 피하세요.";
                case TutorialStep.Jump: return keys ? "스페이스 키로 점프!" : upper ? "두 손을 머리 위로 들어 점프!" : "제자리에서 점프!";
                case TutorialStep.Crouch: return keys ? "S(↓) 키를 누르고 있으면 숙입니다." : upper ? "몸을 낮춰 숙이세요!" : "무릎을 굽혀 숙이세요!";
                case TutorialStep.HighKnee: return keys ? "W(↑) 키를 번갈아 연타해 계단을 오르세요!" : upper ? "팔을 크게 번갈아 흔들어 계단을 오르세요!" : "양쪽 무릎을 번갈아 높이 들어 계단을 오르세요!";
                case TutorialStep.Rail:
                {
                    bool right = TutorialCourse.RailLean == MotionCommand.Right;
                    return keys ? (right ? "D(→)" : "A(←)") + " 키로 레일이 남은 쪽으로 기울이세요."
                        : (upper ? "몸을 " : "제자리에서 상체를 ") + (right ? "오른쪽" : "왼쪽") + "으로 기울여 끊긴 레일을 피하세요.";
                }
                // In the cart the full body leans in place; side steps do nothing there.
                case TutorialStep.Fork: return keys ? "A 또는 D 키로 갈 길을 고르세요." : upper ? "몸을 왼쪽이나 오른쪽으로 기울여 갈 길을 고르세요." : "제자리에서 상체를 왼쪽이나 오른쪽으로 기울여 갈 길을 고르세요.";
                default: return "기본 동작을 모두 익혔어요!";
            }
        }
        // Shown for members who have not finished it and for every guest, once per session.
        public static bool ShouldOffer(bool member, bool tutorialDone, bool offeredThisSession) => !offeredThisSession && (!member || !tutorialDone);
    }

    // The fixed tutorial map: a warm-up lap without a fork that holds one lesson obstacle per chunk,
    // then an ordinary lap whose first cart chunk keeps one broken rail and whose cart fork is the last lesson.
    // Positions are route z in metres.
    public static class TutorialCourse
    {
        public const int Seed = 723, BlockedLane = 0;
        // Mine x4 (start, lane, jump, slide), transition, stairs, corridor, checkpoint, descent, zoo, return to the mine.
        public static readonly int[] WarmupLap = { 0, 0, 0, 0, 3, 7, 4, 6, 11, 12, 3 };
        public const int LaneChunk = 1, JumpChunk = 2, CrouchChunk = 3, StairChunk = 5;
        public const float LaneBlockZ = 12f;
        public static int ForkLapStart => WarmupLap.Length;
        // The ordinary lap's first cart chunk (type 1 on every lap layout) keeps its second rail window broken on one side.
        public static int RailChunk => ForkLapStart + 1;
        public const int RailGapIndex = 1, RailMissingSide = -1;
        public static MotionCommand RailLean => RailMissingSide < 0 ? MotionCommand.Right : MotionCommand.Left;
        public static float RailGapZ => RailChunk * RouteSurface.Length + ObstacleLayout.RailGapZ[RailGapIndex];
        public static float ForkDecisionZ => ForkLapStart * RouteSurface.Length + CartFork.Decision;
        public static RoutePlan Plan(RunConfig config) => RoutePlan.Tutorial(config, Seed, WarmupLap);
        public static TutorialHazard HazardAt(int sequence) => sequence == LaneChunk ? TutorialHazard.LaneBlock : sequence == JumpChunk ? TutorialHazard.JumpRow
            : sequence == CrouchChunk ? TutorialHazard.CrouchRow : sequence == StairChunk ? TutorialHazard.StairChunk
            : sequence == RailChunk ? TutorialHazard.RailGap : TutorialHazard.None;
        public static int ChunkOf(TutorialStep step) => step == TutorialStep.Lane ? LaneChunk : step == TutorialStep.Jump ? JumpChunk
            : step == TutorialStep.Crouch ? CrouchChunk : step == TutorialStep.HighKnee ? StairChunk : step == TutorialStep.Rail ? RailChunk : -1;
        // Restart point after a mistake: past the previous lesson, well before this one.
        public static float RestartZ(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Jump: return 40f;
                case TutorialStep.Crouch: return 64f;
                case TutorialStep.HighKnee: return 100f;
                // On the cart, well before the broken rail; a fork retry starts past it so the rail is not judged again.
                case TutorialStep.Rail: return RailChunk * RouteSurface.Length + 2f;
                case TutorialStep.Fork: return RailGapZ + 4f;
                default: return 2f;
            }
        }
    }
}
