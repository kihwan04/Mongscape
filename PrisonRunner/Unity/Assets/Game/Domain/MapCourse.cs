using System;

namespace Muhanok.Domain
{
    // What a stretch of the route trains (exercise plan 3): the mine start warms up, and the cart fork chooses the day's
    // exercise: prison = strength, zoo = cardio, mine after the fork = balance.
    public enum ExerciseKind { Warmup, Strength, Cardio, Balance }

    // Relative share of the four base moves (jump : duck : knee : lane); 2 is the neutral share.
    public readonly struct ActionMix
    {
        public readonly float Jump, Crouch, Knee, Lane;
        public ActionMix(float jump, float crouch, float knee, float lane) { Jump = jump; Crouch = crouch; Knee = knee; Lane = lane; }
        public static readonly ActionMix Neutral = new ActionMix(2, 2, 2, 2);
        // Multiplier on the base odds for one move (1 at the neutral share).
        public float Factor(HazardAction action) => (action == HazardAction.Jump ? Jump : action == HazardAction.Crouch ? Crouch : Lane) / 2f;
        public float KneeFactor => Knee / 2f;
    }

    public static class MapCourse
    {
        // Destinations as RoutePlan numbers them: -1 zoo, 0 prison, 1 mine.
        public static ExerciseKind KindOf(int destination) => destination == 0 ? ExerciseKind.Strength : destination < 0 ? ExerciseKind.Cardio : ExerciseKind.Balance;
        public static string Name(int destination) => RoutePlan.DestinationName(destination);
        public static string Label(ExerciseKind kind) => kind == ExerciseKind.Strength ? "근력" : kind == ExerciseKind.Cardio ? "유산소" : kind == ExerciseKind.Balance ? "균형" : "준비운동";
        public static string Moves(ExerciseKind kind) => kind == ExerciseKind.Strength ? "숙이기 · 무릎" : kind == ExerciseKind.Cardio ? "점프 · 옆걸음" : kind == ExerciseKind.Balance ? "골고루" : "천천히 제자리 달리기";
        // Intensity dots out of three.
        public static int Dots(ExerciseKind kind) => kind == ExerciseKind.Strength || kind == ExerciseKind.Cardio ? 3 : kind == ExerciseKind.Balance ? 2 : 1;
        public static string DotText(ExerciseKind kind) { int n = Dots(kind); return new string('●', n) + new string('○', 3 - n); }
        // "감옥 · 근력 코스" (the banner once the fork is passed).
        public static string Course(int destination) => Name(destination) + " · " + Label(KindOf(destination)) + " 코스";
        // Plan 3: mine start 2:1:1:2, prison 1:3:3:1, zoo 3:1:1:3, mine after the fork 2:2:2:2.
        public static ActionMix Mix(ExerciseKind kind) => kind == ExerciseKind.Warmup ? new ActionMix(2, 1, 1, 2)
            : kind == ExerciseKind.Strength ? new ActionMix(1, 3, 3, 1) : kind == ExerciseKind.Cardio ? new ActionMix(3, 1, 1, 3) : ActionMix.Neutral;
        // Route type alone (routes without a fork): prison corridors and stairs train strength, the zoo cardio, the rest warms up.
        public static ExerciseKind KindOfType(int type) => type == 4 || type == 5 || type == 6 || type == 7 || type == RouteSurface.PrisonExit ? ExerciseKind.Strength
            : type == 12 || type == RouteSurface.ZooStair ? ExerciseKind.Cardio : ExerciseKind.Warmup;
        // The stretch a route chunk belongs to: before the destination of a lap with a fork, the warm-up; after it, the chosen course.
        public static ExerciseKind KindAt(RoutePlan plan, int sequence)
        {
            if (plan == null) return ExerciseKind.Warmup;
            if (!plan.Extended) return KindOfType(plan.TypeAt(sequence));
            if (plan.LocalSequence(sequence) < CartFork.DestinationChunk) return ExerciseKind.Warmup;
            int destination = plan.DestinationAt(sequence);
            return destination == int.MinValue ? KindOfType(plan.TypeAt(sequence)) : KindOf(destination);
        }
        // Cadence the police expect: the warm-up asks for walking in place (3-4 MET), the courses for a jog.
        public static float TargetCadence(ExerciseKind kind, MotionTuning tuning)
        {
            tuning ??= new MotionTuning();
            return kind == ExerciseKind.Warmup ? tuning.RunCadence - 30f : CadencePace.Target(tuning);
        }
        // Extra stairs on the course: strength +1 (at most 3), cardio -1 (at least 1); every map keeps all four moves.
        public static int Stairs(int baseCount, int destination)
            => Math.Max(1, Math.Min(3, baseCount + (destination == 0 ? 1 : destination < 0 ? -1 : 0)));
    }

    // Today's challenge (exercise plan 10): everyone runs the same map (the seed of the KST date) and has a ranking for the day.
    public static class DailyChallenge
    {
        // Challenge runs are kept apart on the server by play style + Offset (10 full body, 11 upper body, 12 keyboard).
        public const int ModeOffset = 10;
        public static DateTime KstDate(DateTime utcNow) => utcNow.AddHours(9).Date;
        public static string Day(DateTime utcNow) => KstDate(utcNow).ToString("yyyyMMdd");
        public static int Seed(DateTime utcNow)
        {
            var d = KstDate(utcNow);
            unchecked { int h = 17; h = h * 31 + d.Year; h = h * 31 + d.Month; h = h * 31 + d.Day; return (h & 0x7fffffff) % 2000000000 + 1; }
        }
        public static int Mode(PlayStyle style) => (int)style + ModeOffset;
    }
}
