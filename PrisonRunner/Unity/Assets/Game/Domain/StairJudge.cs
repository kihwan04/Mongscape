using System;

namespace Muhanok.Domain
{
    // Judges a stair chunk: inside local z 2..22 the runner has to keep stepping, with no gap longer than KneeStepGap.
    // The first two metres are grace (the first step has to come by z 2). At most one failure per chunk.
    public sealed class StairJudge
    {
        public const float From = 2f, To = 22f;
        private readonly RunConfig config;
        private bool failed, inChunk;
        private float lastZ;
        // Seconds since this stair chunk was entered: entering counts as a fresh step, so the knee gauge starts full and
        // nobody has to begin stepping before the stairs. The runner's own step clock (animation, coins, lessons) is untouched.
        private float sinceEntry = float.MaxValue;
        public StairJudge(RunConfig tuning) { config = tuning; }
        // Inside the judged span of a stair chunk.
        public bool InStair { get; private set; }
        // 0 at the start of the judged span, 1 at its end.
        public float StairProgress { get; private set; }
        // 0 right after a step, 1 once the gap reaches KneeStepGap (a miss).
        public float KneeUrgency { get; private set; }
        public bool Failed => failed;
        // True once per stair chunk, on the tick the runner misses the step rhythm.
        public bool Tick(float dt, bool inStairChunk, float localZ, RunnerController runner)
        {
            bool entered = inStairChunk && (!inChunk || localZ < lastZ);
            if (!inStairChunk || runner.Riding) { inChunk = false; InStair = false; StairProgress = KneeUrgency = 0f; lastZ = 0f; return false; }
            if (entered) { failed = false; sinceEntry = 0f; } else sinceEntry += dt;
            inChunk = true; lastZ = localZ;
            InStair = localZ >= From && localZ <= To;
            StairProgress = InStair ? Math.Max(0f, Math.Min(1f, (localZ - From) / (To - From))) : 0f;
            float gap = Math.Max(.01f, config.KneeStepGap);
            float since = Math.Min(runner.SinceKneeStep, sinceEntry);
            KneeUrgency = InStair ? Math.Max(0f, Math.Min(1f, since / gap)) : 0f;
            if (InStair && !failed && since > gap) { failed = true; return true; }
            return false;
        }
        // For scripted runners (bots, tests): true when the next step is due, every `interval` simulated seconds inside a stair chunk.
        public static bool StepDue(RunnerController runner, float interval = .4f)
            => !runner.Riding && RouteSurface.IsStair(RouteSurface.TypeAt(RouteSurface.SequenceAt(runner.Z))) && runner.SinceKneeStep >= interval;
        public void Reset() { failed = inChunk = InStair = false; StairProgress = KneeUrgency = lastZ = 0f; sinceEntry = float.MaxValue; }
    }
}
