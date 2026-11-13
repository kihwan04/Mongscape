using System;
using System.Collections.Generic;

namespace Muhanok.Domain
{
    // Chunk order for one run. The first lap keeps the authored order; later laps form intervals:
    // a mine-cart rest followed by a sprint whose length follows the configured sprint time.
    // Height rules: 0-3 ground, 7 climbs, 4/5/6 stay up, 11 descends, 12 ground, 15 (prison exit) stays up.
    public sealed class RoutePlan
    {
        public static readonly int[] AuthoredLap = { 0, 1, 2, 3, 7, 4, 5, 6, 11, 12 };
        private readonly List<int> types = new List<int>(64);
        private readonly List<int> cycles = new List<int>(64);
        // bases[i] = summed rise of chunks 0..i-1 (entry height of chunk i); always one longer than types.
        private readonly List<float> bases = new List<float>(65) { 0f };
        private readonly Random random;
        private readonly RunConfig config;
        private readonly int seed;
        private readonly bool interval;
        private int cycle;
        // Leading laps without a cart fork (the tutorial's warm-up lap).
        private readonly int plainLaps;
        private bool ForkCycle(int c) => c >= plainLaps;
        private readonly List<int> starts = new List<int>();
        private readonly Dictionary<int, int> choices = new Dictionary<int, int>();
        private readonly List<int[]> destinations = new List<int[]>();
        public bool Extended => config != null && config.ExtendedRoutes;
        public const int ExtendedLapChunks = 32;
        public int LocalSequence(int sequence) { Ensure(sequence); return sequence - starts[cycles[sequence]]; }
        public bool IsForkTrackChunk(int sequence) => Extended && LocalSequence(sequence) >= CartFork.FirstChunk && LocalSequence(sequence) <= CartFork.LastChunk && ForkCycle(cycles[sequence]);
        public bool RailChoiceLocked(float z)=>Extended&&ForkLocked(z)&&LapDistance(z)>=CartFork.Decision&&LapDistance(z)<CartFork.End;
        public float LapDistance(float z){int s=RouteSurface.SequenceAt(z);Ensure(s);return z-starts[cycles[s]]*RouteSurface.Length;}
        public float PrefetchLimit(float z) => !Extended||ForkLocked(z)||!ForkCycle(CycleAt(RouteSurface.SequenceAt(z)))?float.MaxValue:z-LapDistance(z)+CartFork.End;
        public float ChoiceLaneOffset(float z,float laneX)
        {
            if(!ForkWindow(z))return 0;
            return laneX*Math.Max(.16f/2.2f,CartFork.Offset(LapDistance(z),1)/2.2f);
        }
        public float CartLaneOffset(float z,float laneX,float balance)
        {
            if(ForkWindow(z))return ChoiceLaneOffset(z,laneX);
            if(Extended&&ForkLocked(z))
            {
                float t=Math.Max(0,Math.Min(1,(LapDistance(z)-CartFork.Decision)/12));
                return balance*t*t*(3-2*t);
            }
            return balance;
        }
        public float CycleOffset(int sequence)
        {
            int c = CycleAt(sequence); float offset = 0;
            for (int i = 0; i < c; i++) if (choices.TryGetValue(i, out int direction)) offset += direction * CartFork.Spacing;
            return offset;
        }
        public float SelectedForkOffset(float z)
        {
            if (!Extended) return 0;
            int s = Math.Max(0, (int)(z / RouteSurface.Length)); Ensure(s);
            return CycleOffset(s) + (ForkLocked(z)?CartFork.Offset(z - starts[cycles[s]] * RouteSurface.Length, ChoiceAt(s)):0);
        }
        public int ChoiceAt(int sequence) { int c = CycleAt(sequence); return choices.TryGetValue(c, out int lane) ? lane : 0; }
        public bool ForkWindow(float z)
        {
            if (!Extended) return false;
            int s = Math.Max(0, (int)(z / RouteSurface.Length)); Ensure(s);
            if (!ForkCycle(cycles[s])) return false;
            float local = z - starts[cycles[s]] * RouteSurface.Length;
            return local >= CartFork.Start && local < CartFork.Decision;
        }
        public bool ForkLocked(float z)
        {
            if (!Extended) return false;
            int s = Math.Max(0, (int)(z / RouteSurface.Length)); Ensure(s);
            return choices.ContainsKey(cycles[s]);
        }
        // Cart chunks that keep both rails: the fork track and the one in front of its window (a broken rail there
        // would ask for a lean right before the branch choice). The earlier cart chunks keep their broken rails.
        public bool IsForkChunk(int sequence)
        {
            if (!Extended) return false; Ensure(sequence);
            if (!ForkCycle(cycles[sequence])) return false;
            int local = sequence - starts[cycles[sequence]];
            return local >= CartFork.FirstChunk - 1 && local <= CartFork.LastChunk;
        }
        public void UpdateChoice(float z, int lane)
        {
            if (!Extended) return;
            int s = Math.Max(0, (int)(z / RouteSurface.Length)); Ensure(s);
            int c = cycles[s], start = starts[c];
            float local = z - start * RouteSurface.Length;
            if (local < CartFork.Decision || choices.ContainsKey(c) || !ForkCycle(c)) return;
            lane = Math.Max(-1, Math.Min(1, lane)); choices[c] = lane;
            // No destination is exposed before commitment; all three directions are shuffled each lap.
            int[] lap = ExtendedLap(destinations[c][lane + 1]);
            PlaceStairs(c, start, lap);
            for (int i = CartFork.ConnectorChunk; i < lap.Length; i++) types[start + i] = lap[i];
            RebuildBases(start + CartFork.ConnectorChunk);
        }
        public int DestinationAt(int sequence)
        {
            int c = CycleAt(sequence);
            return choices.TryGetValue(c, out int lane) ? destinations[c][lane + 1] : int.MinValue;
        }
        public static string DestinationName(int destination) => destination < 0 ? "동물원" : destination > 0 ? "광산" : "감옥";
        private static int[] ExtendedLap(int lane)
        {
            var lap = new List<int>(ExtendedLapChunks) { 0, 1, 2, 1, 2, 1, 3 };
            if (lane!=0)
            {
                while(lap.Count<ExtendedLapChunks-1)lap.Add(lane<0?12:0);
                lap.Add(lane<0?3:0);return lap.ToArray();
            }
            // Prison branch: entrance stair, corridor up to the checkpoint, then the prison-to-mine exit. No outdoor chunk follows.
            lap.Add(7);
            for (int i = 0; lap.Count < ExtendedLapChunks - 2; i++) lap.Add(4 + i % 2);
            lap.Add(6); lap.Add(RouteSurface.PrisonExit);
            return lap.ToArray();
        }
        // Stair chunks per lap follow the difficulty curve: 1, then 2, then 3. The prison entrance stair counts as one;
        // extra prison stairs reuse the type-7 stair chunk inside the corridor.
        public const int MinStairSpacing = 6;
        public static int StairsPerLap(float progress) => progress < 1f / 3f ? 1 : progress < 2f / 3f ? 2 : 3;
        private static bool Transition(int type) => type == 3 || type == 6 || type == 7 || type == 11 || type == RouteSurface.PrisonExit;
        // Turns some walking chunks of the chosen branch into the biome's stair (mine 0 -> 13, zoo 12 -> 14, prison corridor 4/5 -> 7).
        // Seeded from (seed, lap), so the same seed and choices always give the same layout; chunks before the destination entrance,
        // the lap's return chunk and neighbours of transition chunks are never used.
        private void PlaceStairs(int lapIndex, int lapStart, int[] lap)
        {
            if (config == null) return;
            float progress = new DifficultyDirector().Progress(lapStart * RouteSurface.Length, config);
            int want = StairsPerLap(progress);
            var stairs = new List<int>();
            for (int i = 0; i < lap.Length; i++) if (RouteSurface.IsStair(lap[i])) stairs.Add(i);
            var rng = new Random(unchecked(seed * 486187739 + lapIndex * 1013904223 + 12345));
            var candidates = new List<int>();
            for (int i = CartFork.DestinationChunk + 1; i < lap.Length - 1; i++)
                if ((lap[i] == 0 || lap[i] == 12 || lap[i] == 4 || lap[i] == 5) && !Transition(lap[i - 1]) && !Transition(lap[i + 1])) candidates.Add(i);
            for (int i = candidates.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); int swap = candidates[i]; candidates[i] = candidates[j]; candidates[j] = swap; }
            foreach (int i in candidates)
            {
                if (stairs.Count >= want) break;
                bool apart = true;
                foreach (int other in stairs) apart &= Math.Abs(other - i) >= MinStairSpacing;
                if (!apart) continue;
                lap[i] = lap[i] == 0 ? RouteSurface.MineStair : lap[i] == 12 ? RouteSurface.ZooStair : 7; stairs.Add(i);
            }
        }
        public RoutePlan(RunConfig tuning, int seed)
        {
            this.seed = seed;
            config = tuning; interval = tuning != null && tuning.IntervalMode; random = new Random(seed);
            Append(Extended ? ExtendedLap(0) : AuthoredLap);
        }
        private RoutePlan(RunConfig tuning, int seed, int[] warmup)
        {
            this.seed = seed;
            config = tuning; interval = tuning != null && tuning.IntervalMode; random = new Random(seed); plainLaps = 1;
            Append(warmup);
        }
        // Tutorial: a fixed warm-up lap without a fork, followed by the ordinary laps.
        public static RoutePlan Tutorial(RunConfig tuning, int seed, int[] warmup) => new RoutePlan(tuning, seed, warmup ?? AuthoredLap);
        public static RoutePlan Authored() => new RoutePlan(null, 0);
        public float BaseHeight(int sequence) { Ensure(sequence); return bases[Math.Max(0, sequence)]; }
        private void RebuildBases(int from)
        {
            if (bases.Count > from + 1) bases.RemoveRange(from + 1, bases.Count - from - 1);
            for (int i = from; i < types.Count; i++) bases.Add(bases[i] + RouteSurface.Rise(types[i]));
        }
        public int TypeAt(int sequence) { Ensure(sequence); return types[Math.Max(0, sequence)]; }
        public int CycleAt(int sequence) { Ensure(sequence); return cycles[Math.Max(0, sequence)]; }
        private void Ensure(int sequence) { while (Math.Max(0, sequence) >= types.Count) AppendLap(); }
        private void Append(IEnumerable<int> lap)
        {
            var shuffled = new[] { -1, 0, 1 };
            for (int i = 2; i > 0; i--) { int j = random.Next(i + 1); int swap = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = swap; }
            destinations.Add(shuffled);
            starts.Add(types.Count); foreach (int type in lap) { types.Add(type); cycles.Add(cycle); } cycle++;
            RebuildBases(bases.Count - 1);
        }
        private void AppendLap()
        {
            if (Extended) { Append(ExtendedLap(0)); return; }
            if (!interval) { Append(AuthoredLap); return; }
            var lap = new List<int>(24) { 0 };
            int rest = Math.Max(2, (int)Math.Round(config.RestSeconds * config.RestSpeed / RouteSurface.Length));
            for (int i = 0; i < rest; i++) lap.Add(i % 2 == 0 ? 1 : 2);
            float sprintSpeed = Math.Min(config.MaxSprintSpeed, config.SprintSpeed + config.SprintSpeedStep * cycle);
            // Fixed sprint chunks: transition, stairs, checkpoint, descent and the next mine start.
            int flexible = Math.Max(2, (int)Math.Round(config.SprintSeconds * sprintSpeed / RouteSurface.Length) - 5);
            int zoo = Math.Min(3, Math.Max(1, flexible / 3));
            int prison = Math.Max(1, flexible - zoo);
            lap.Add(3); lap.Add(7);
            // Alternate the two corridor variants so no type repeats more than the chunk pool holds.
            int first = random.Next(2);
            for (int i = 0; i < prison; i++) lap.Add(4 + (first + i) % 2);
            lap.Add(6); lap.Add(11);
            for (int i = 0; i < zoo; i++) lap.Add(12);
            Append(lap);
        }
    }
}
