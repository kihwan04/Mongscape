using System;
using System.Collections.Generic;

namespace Muhanok.Domain
{
    // Exercise plan 10-3: 카메라 확인 → 보정 → 제자리 달리기 → 경찰 → 사이드 스텝 → 점프 → 숙이기 → 계단 → 바나나 → 수레·갈림길.
    public enum TutorialStep { Camera, Calibrate, Cadence, Police, Lane, Jump, Crouch, HighKnee, Banana, Rail, Fork, Done }
    // What a step waits for: a framed body, a calibrated body, a held pace, the police shaken off, a lesson obstacle
    // (or broken rail, or both bananas) passed cleanly, or a lean at the cart fork.
    public enum TutorialPass { Framed, Calibrated, Paced, PoliceShaken, ObstaclePassed, ForkChosen, None }
    // The lesson hazards of the course; every random hazard, item and coin is removed.
    public enum TutorialHazard { None, LaneBlock, JumpRow, CrouchRow, StairChunk, Bananas, RailGap }

    // Lessons for one play style (about 15 s each, under 3 minutes). Steps the style, route or options lack are left out:
    // keyboard play needs no camera check, calibration, cadence or police; with "달리기 속도 조절" off the police step goes
    // (the cadence step then only shows that stepping turns into calories); a route without forks has no lean.
    // Members who finished the earlier tutorial get the short version once: only what this update changed (0~3, 8, 9).
    public sealed class TutorialScript
    {
        private readonly List<TutorialStep> steps = new List<TutorialStep>(12);
        public PlayStyle Style { get; }
        public bool CadenceSpeed { get; }
        public bool Short { get; }
        public IReadOnlyList<TutorialStep> Steps => steps;
        public int Index { get; private set; }
        public TutorialStep Current => steps[Index];
        public bool Finished => Current == TutorialStep.Done;
        // Retries of the current step and of the whole tutorial.
        public int Retries { get; private set; }
        public int TotalRetries { get; private set; }
        // From this many retries of one step, the demo window grows into "따라 하기".
        public const int FollowAlongRetries = 3;
        public bool FollowAlong => Retries >= FollowAlongRetries;
        public static readonly TutorialStep[] UpdateSteps = { TutorialStep.Camera, TutorialStep.Calibrate, TutorialStep.Cadence, TutorialStep.Police, TutorialStep.Banana, TutorialStep.Rail, TutorialStep.Fork };
        public TutorialScript(PlayStyle style, bool forks = true, bool cadenceSpeed = true, bool shortVersion = false)
        {
            Style = style; CadenceSpeed = cadenceSpeed; Short = shortVersion;
            bool camera = PlayStyleInfo.UsesCamera(style);
            foreach (TutorialStep step in Enum.GetValues(typeof(TutorialStep)))
            {
                if (!camera && (step == TutorialStep.Camera || step == TutorialStep.Calibrate || step == TutorialStep.Cadence || step == TutorialStep.Police)) continue;
                if (step == TutorialStep.Police && !cadenceSpeed) continue;
                if (step == TutorialStep.Fork && !forks) continue;
                if (shortVersion && step != TutorialStep.Done && Array.IndexOf(UpdateSteps, step) < 0) continue;
                steps.Add(step);
            }
        }
        public bool Has(TutorialStep step) => steps.Contains(step);
        // The step's pass condition was met: the next lesson starts with a fresh retry count.
        public bool Pass() { if (Finished) return false; Index++; Retries = 0; return true; }
        // A mistake restarts the same step in front of its obstacle.
        public void Fail() { if (Finished) return; Retries++; TotalRetries++; }
        public TutorialPass Condition => PassOf(Current);
        public MotionCommand? Awaited => AwaitedOf(Current);

        public static TutorialPass PassOf(TutorialStep step) => step == TutorialStep.Camera ? TutorialPass.Framed : step == TutorialStep.Calibrate ? TutorialPass.Calibrated
            : step == TutorialStep.Cadence ? TutorialPass.Paced : step == TutorialStep.Police ? TutorialPass.PoliceShaken
            : step == TutorialStep.Fork ? TutorialPass.ForkChosen : step == TutorialStep.Done ? TutorialPass.None : TutorialPass.ObstaclePassed;
        // Lane, banana and fork steps accept either side (Left stands for both); the broken rail needs the lean away from the gap.
        public static MotionCommand? AwaitedOf(TutorialStep step) => step == TutorialStep.Lane || step == TutorialStep.Fork || step == TutorialStep.Banana ? MotionCommand.Left
            : step == TutorialStep.Rail ? TutorialCourse.RailLean
            : step == TutorialStep.Jump ? MotionCommand.Jump : step == TutorialStep.Crouch ? MotionCommand.Crouch : step == TutorialStep.HighKnee ? MotionCommand.KneeStep : (MotionCommand?)null;
        public static bool Accepts(TutorialStep step, MotionCommand command)
        {
            if (step == TutorialStep.Lane || step == TutorialStep.Fork || step == TutorialStep.Banana) return command == MotionCommand.Left || command == MotionCommand.Right;
            var awaited = AwaitedOf(step);
            return awaited.HasValue && awaited.Value == command;
        }
        // The runner already shows the awaited action (from any input source). The banana step is passed only by getting past both peels.
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
        public static string Title(TutorialStep step) => step == TutorialStep.Camera ? "카메라 확인" : step == TutorialStep.Calibrate ? "보정"
            : step == TutorialStep.Cadence ? "제자리 달리기" : step == TutorialStep.Police ? "경찰" : step == TutorialStep.Lane ? "사이드 스텝"
            : step == TutorialStep.Jump ? "점프" : step == TutorialStep.Crouch ? "숙이기" : step == TutorialStep.HighKnee ? "계단"
            : step == TutorialStep.Banana ? "바나나" : step == TutorialStep.Rail ? "끊긴 레일 피하기" : step == TutorialStep.Fork ? "갈림길 고르기" : "튜토리얼 완료";
        public static string Instruction(TutorialStep step, PlayStyle style, bool cadenceSpeed = true)
        {
            bool keys = style == PlayStyle.Keyboard, upper = style == PlayStyle.UpperBody;
            switch (step)
            {
                case TutorialStep.Camera: return upper ? "허리 위가 화면에 다 들어오게 앉거나 서 주세요." : "머리부터 발끝까지 화면에 들어오게 서 주세요.";
                case TutorialStep.Calibrate: return "카메라 앞에 바르게 서 주세요. 자세를 맞추면 출발합니다.";
                // Cadence off: stepping earns no calories (only jumps, crouches, knee raises and lane changes do).
                case TutorialStep.Cadence: return !cadenceSpeed ? (upper ? "팔을 흔들어 보세요. 속도 조절 꺼짐: 팔 흔들기는 칼로리 제외" : "제자리 달리기를 해 보세요. 속도 조절 꺼짐: 걸음은 칼로리 제외")
                    : upper ? "팔을 번갈아 빠르게 흔들어 보세요. 빠를수록 빨리 달려요!" : "제자리 달리기를 해 보세요. 빠를수록 빨리 달려요!";
                case TutorialStep.Police: return "잠깐 멈춰 보세요. 느려지면 경찰이 다가와요!";
                case TutorialStep.Lane: return keys ? "A·D(←·→) 키로 옆 줄로 피하세요." : upper ? "상체를 옆으로 기울여 피하세요." : "옆으로 크게 한 걸음! 상체만 기울이면 안 돼요.";
                case TutorialStep.Jump: return keys ? "스페이스 키로 점프!" : upper ? "두 손을 머리 위로 들어 점프!" : "제자리에서 점프! 높이 뛰면 더 높이 떠요.";
                case TutorialStep.Crouch: return keys ? "S(↓) 키를 누르고 있으면 숙입니다." : upper ? "몸을 낮춰 숙이세요!" : "무릎을 굽혀 숙이세요! 깊게 앉으면 더 길게 미끄러져요.";
                case TutorialStep.HighKnee: return keys ? "W(↑) 키를 번갈아 연타해 계단을 오르세요!" : upper ? "두 팔을 머리 위로 뻗었다가 어깨 아래까지 당겨 내려 계단을 오르세요!" : "뛰지 말고 제자리에서 걷듯이, 무릎을 번갈아 높이 드세요!";
                case TutorialStep.Banana: return keys ? "빨갛게 빛나는 바나나를 A·D 키로 피하세요! 두 번 밟으면 잡혀요." : "빨갛게 빛나는 바나나를 옆으로 피하세요! 두 번 밟으면 잡혀요.";
                case TutorialStep.Rail:
                {
                    bool right = TutorialCourse.RailLean == MotionCommand.Right;
                    return keys ? (right ? "D(→)" : "A(←)") + " 키로 레일이 남은 쪽으로 기울이세요."
                        : (upper ? "몸을 " : "제자리에서 상체를 ") + (right ? "오른쪽" : "왼쪽") + "으로 기울여 끊긴 레일을 피하세요.";
                }
                // In the cart the full body leans in place; side steps do nothing there.
                case TutorialStep.Fork: return keys ? "위 안내를 보고 A 또는 D 키로 갈 코스를 고르세요." : upper ? "위 안내를 보고 몸을 기울여 갈 코스를 고르세요." : "위 안내를 보고 제자리에서 상체를 기울여 갈 코스를 고르세요.";
                default: return "기본 동작을 모두 익혔어요!";
            }
        }
        // Second line once the police came close: run again to shake them off.
        public const string PoliceAway = "다시 뛰어서 경찰을 떼어내세요!";
        // Jump and crouch are timed: before their call the line only prepares the player, so nobody acts too early.
        public static bool Timed(TutorialStep step) => step == TutorialStep.Jump || step == TutorialStep.Crouch;
        public static string Prepare(TutorialStep step, PlayStyle style)
        {
            bool keys = style == PlayStyle.Keyboard, upper = style == PlayStyle.UpperBody;
            if (step == TutorialStep.Jump) return keys ? "「점프!」 신호가 뜨면 스페이스 키를 누르세요." : upper ? "「점프!」 신호가 뜨면 두 손을 머리 위로 드세요." : "「점프!」 신호가 뜨면 제자리에서 점프하세요.";
            if (step == TutorialStep.Crouch) return keys ? "「숙이기!」 신호가 뜨면 S(↓) 키를 누르고 있으세요." : upper ? "「숙이기!」 신호가 뜨면 몸을 낮추세요." : "「숙이기!」 신호가 뜨면 무릎을 굽혀 숙이세요.";
            return Instruction(step, style);
        }
        // The demo clip of the character window (Mixamo/Blender clips of the monkey that exist today); null when there is none
        // (the camera check and calibration need none; the upper-body moves have no clip yet).
        public static string DemoClip(TutorialStep step, PlayStyle style)
        {
            if (style == PlayStyle.UpperBody) return null;
            switch (step)
            {
                case TutorialStep.Cadence: case TutorialStep.Police: return "AN_Monkey_Run";
                case TutorialStep.Lane: case TutorialStep.Banana: return "AN_Monkey_LaneStep_R";
                case TutorialStep.Jump: return "AN_Monkey_Jump";
                case TutorialStep.Crouch: return "AN_Monkey_CrouchSlide";
                case TutorialStep.HighKnee: return "AN_Monkey_HighKnee";
                case TutorialStep.Rail: case TutorialStep.Fork: return "AN_Monkey_CartRide";
                default: return null;
            }
        }
        // Shown for members who have not finished it and for every guest, once per session.
        public static bool ShouldOffer(bool member, bool tutorialDone, bool offeredThisSession) => !offeredThisSession && (!member || !tutorialDone);
        // Members who finished the earlier tutorial are offered the short version once (remembered on this PC).
        public static bool ShouldOfferUpdate(bool member, bool tutorialDone, bool updateSeen, bool offeredThisSession) => !offeredThisSession && member && tutorialDone && !updateSeen;
    }

    // The fixed tutorial map: a warm-up lap in the mine without a fork that holds the lessons, then an ordinary lap whose
    // first cart chunk keeps one broken rail and whose cart fork is the last lesson. Positions are route z in metres.
    public static class TutorialCourse
    {
        public const int Seed = 723, BlockedLane = 0;
        // Mine only: start, the pace chunk (cadence and police hold here), lane, jump, slide, a plain walk (time to get ready),
        // the mine stair, then the banana chunk. The next lap's mine start boards the cart right after.
        public static readonly int[] WarmupLap = { 0, 0, 0, 0, 0, 0, RouteSurface.MineStair, 0 };
        public const int PaceChunk = 1, LaneChunk = 2, JumpChunk = 3, CrouchChunk = 4, StairChunk = 6, BananaChunk = 7;
        public const float LaneBlockZ = 12f;
        // The cadence and police lessons hold the runner here (route z) until they pass.
        public static float PaceHoldZ => (PaceChunk + 1) * RouteSurface.Length - 4f;
        // Two peels in the blocked lane of the banana chunk: step aside for both.
        public static readonly float[] BananaZ = { 8f, 16f };
        public static int ForkLapStart => WarmupLap.Length;
        // The ordinary lap's first cart chunk (type 1 on every lap layout) keeps its second rail window broken on one side.
        public static int RailChunk => ForkLapStart + 1;
        public const int RailGapIndex = 1, RailMissingSide = -1;
        public static MotionCommand RailLean => RailMissingSide < 0 ? MotionCommand.Right : MotionCommand.Left;
        public static float RailGapZ => RailChunk * RouteSurface.Length + ObstacleLayout.RailGapZ[RailGapIndex];
        public static float ForkDecisionZ => ForkLapStart * RouteSurface.Length + CartFork.Decision;
        public static RoutePlan Plan(RunConfig config) => RoutePlan.Tutorial(config, Seed, WarmupLap);
        // The lessons of the current script (the short version leaves some out); every hazard when none is set.
        public static TutorialScript Script;
        private static bool On(TutorialStep step) => Script == null || Script.Has(step);
        public static TutorialHazard HazardAt(int sequence) => sequence == LaneChunk && On(TutorialStep.Lane) ? TutorialHazard.LaneBlock
            : sequence == JumpChunk && On(TutorialStep.Jump) ? TutorialHazard.JumpRow : sequence == CrouchChunk && On(TutorialStep.Crouch) ? TutorialHazard.CrouchRow
            : sequence == StairChunk ? TutorialHazard.StairChunk : sequence == BananaChunk && On(TutorialStep.Banana) ? TutorialHazard.Bananas
            : sequence == RailChunk && On(TutorialStep.Rail) ? TutorialHazard.RailGap : TutorialHazard.None;
        // The short update tutorial has no stair lesson but still runs over the stair chunk: a knee miss there is not the
        // current lesson's mistake (it used to restart the banana step) (QA 2026-11-25 M2).
        public static bool ForgivesStairMiss(TutorialScript script, int sequence) => sequence == StairChunk && script != null && !script.Has(TutorialStep.HighKnee);
        public static int ChunkOf(TutorialStep step) => step == TutorialStep.Cadence || step == TutorialStep.Police ? PaceChunk : step == TutorialStep.Lane ? LaneChunk
            : step == TutorialStep.Jump ? JumpChunk : step == TutorialStep.Crouch ? CrouchChunk : step == TutorialStep.HighKnee ? StairChunk
            : step == TutorialStep.Banana ? BananaChunk : step == TutorialStep.Rail ? RailChunk : -1;
        // Restart point after a mistake: past the previous lesson, well before this one.
        public static float RestartZ(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Cadence: case TutorialStep.Police: return PaceChunk * RouteSurface.Length;
                case TutorialStep.Lane: return LaneChunk * RouteSurface.Length - 4f;
                case TutorialStep.Jump: return JumpChunk * RouteSurface.Length - 8f;
                case TutorialStep.Crouch: return CrouchChunk * RouteSurface.Length - 8f;
                // Early in the walking chunk after the slide lesson, with time to read the stair call.
                case TutorialStep.HighKnee: return StairChunk * RouteSurface.Length - 20f;
                case TutorialStep.Banana: return BananaChunk * RouteSurface.Length - 6f;
                // On the cart, well before the broken rail; a fork retry starts past it so the rail is not judged again.
                case TutorialStep.Rail: return RailChunk * RouteSurface.Length + 2f;
                case TutorialStep.Fork: return RailGapZ + 4f;
                default: return 2f;
            }
        }
    }
}
