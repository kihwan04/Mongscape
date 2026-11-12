using System;
using System.Collections.Generic;
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class StairChunkTests
    {
        private const int Lap = RoutePlan.ExtendedLapChunks;
        [TearDown] public void Reset() => RouteSurface.UsePlan(null);
        // Grace 0 and full 1536 m put lap 0 / 1 / 2 (starting at 0 / 768 / 1536 m) in the 1 / 2 / 3 stair buckets.
        private static RunConfig Curve() => new RunConfig { DifficultyGraceDistance = 0f, DifficultyFullDistance = 1536f };
        private static float DecisionAt(int lap) => lap * Lap * RouteSurface.Length + CartFork.Decision + 1;
        private static bool Transition(int type) => type == 3 || type == 6 || type == 7 || type == 11;
        private static int[] Choose(int seed, params int[] lanes)
        {
            var plan = new RoutePlan(Curve(), seed);
            for (int lap = 0; lap < lanes.Length; lap++) plan.UpdateChoice(DecisionAt(lap), lanes[lap]);
            var types = new int[lanes.Length * Lap];
            for (int i = 0; i < types.Length; i++) types[i] = plan.TypeAt(i);
            return types;
        }
        // The branch a lane choice leads to on a lap: -1 zoo, 0 prison, 1 mine.
        private static int Branch(int seed, int lap, int lane)
        {
            var plan = new RoutePlan(Curve(), seed);
            plan.UpdateChoice(DecisionAt(lap), lane);
            return plan.DestinationAt(lap * Lap);
        }

        [Test] public void StairBucketsFollowDifficultyProgress()
        {
            Assert.AreEqual(1, RoutePlan.StairsPerLap(0f)); Assert.AreEqual(1, RoutePlan.StairsPerLap(.33f));
            Assert.AreEqual(2, RoutePlan.StairsPerLap(.34f)); Assert.AreEqual(2, RoutePlan.StairsPerLap(.66f));
            Assert.AreEqual(3, RoutePlan.StairsPerLap(.67f)); Assert.AreEqual(3, RoutePlan.StairsPerLap(1f));
        }

        [Test] public void EveryBranchGetsStairsAtTheRightCountSpacingAndPlaces()
        {
            var seen = new HashSet<string>();
            for (int seed = 1; seed <= 40; seed++)
                for (int lane = -1; lane <= 1; lane++)
                    for (int lap = 0; lap < 3; lap++)
                    {
                        // Each lap is chosen on its own plan; earlier laps stay unchosen and are not read.
                        var plan = new RoutePlan(Curve(), seed);
                        plan.UpdateChoice(DecisionAt(lap), lane);
                        int branch = plan.DestinationAt(lap * Lap);
                        var types = new int[Lap]; for (int i = 0; i < Lap; i++) types[i] = plan.TypeAt(lap * Lap + i);
                        var stairs = new List<int>(); for (int i = 0; i < Lap; i++) if (RouteSurface.IsStair(types[i])) stairs.Add(i);
                        int expected = lap + 1;
                        string note = "seed " + seed + " lap " + lap + " branch " + branch;
                        if (branch == 0)
                        {
                            Assert.Contains(7, stairs, note); Assert.AreEqual(7, types[CartFork.DestinationChunk], note);
                            Assert.LessOrEqual(stairs.Count, expected, note);
                            foreach (int i in stairs) if (i != 7) Assert.AreEqual(RouteSurface.ZooStair, types[i], "Prison extras go into the zoo part: " + note);
                        }
                        else
                        {
                            Assert.AreEqual(expected, stairs.Count, note);
                            foreach (int i in stairs) Assert.AreEqual(branch > 0 ? RouteSurface.MineStair : RouteSurface.ZooStair, types[i], note);
                        }
                        seen.Add(branch + ":" + lap);
                        for (int a = 0; a < stairs.Count; a++)
                        {
                            int i = stairs[a];
                            Assert.Less(i, Lap - 1, "Never the return chunk: " + note);
                            for (int b = a + 1; b < stairs.Count; b++) Assert.GreaterOrEqual(Math.Abs(i - stairs[b]), RoutePlan.MinStairSpacing, note);
                            if (i == 7) continue;
                            Assert.GreaterOrEqual(i, CartFork.DestinationChunk + 1, "Never in the shared start: " + note);
                            Assert.IsFalse(Transition(types[i - 1]) || Transition(types[i + 1]), "Next to a transition chunk: " + note);
                        }
                        for (int i = 0; i < CartFork.DestinationChunk; i++) Assert.IsFalse(RouteSurface.IsStair(types[i]), "Shared start: " + note);
                        for (int i = 0; i < Lap; i++) if (types[i] == 4 || types[i] == 5) Assert.IsTrue(i >= 8 && i <= 21, "Corridor stays corridor: " + note);
                    }
            Assert.AreEqual(9, seen.Count, "Every branch was exercised on every lap");
        }

        [Test] public void SameSeedAndChoicesGiveTheSameLayoutAndSeedsVaryIt()
        {
            var layouts = new HashSet<string>();
            for (int seed = 1; seed <= 30; seed++)
            {
                var first = Choose(seed, 1, -1, 1); var again = Choose(seed, 1, -1, 1);
                CollectionAssert.AreEqual(first, again, "Seed " + seed);
                layouts.Add(string.Join(",", first));
            }
            Assert.Greater(layouts.Count, 10);
        }

        [Test] public void AuthoredAndTutorialLapsStayAsAuthored()
        {
            var tutorial = RoutePlan.Tutorial(Curve(), 3, new[] { 0, 1, 2, 3, 7, 4, 5, 6, 11, 12 });
            for (int i = 0; i < 10; i++) Assert.AreEqual(RoutePlan.AuthoredLap[i], tutorial.TypeAt(i));
            var plain = new RoutePlan(new RunConfig { ExtendedRoutes = false }, 5);
            for (int i = 0; i < 40; i++) Assert.IsFalse(RouteSurface.IsStair(plain.TypeAt(i)) && plain.TypeAt(i) != 7);
        }

        [Test] public void ALapWithTwoExtraStairsClimbsNineMetresMore()
        {
            int checkedLaps = 0;
            for (int seed = 1; seed <= 20; seed++)
                for (int lane = -1; lane <= 1; lane++)
                {
                    if (Branch(seed, 2, lane) != 1) continue;
                    var plan = new RoutePlan(Curve(), seed);
                    plan.UpdateChoice(DecisionAt(2), lane);
                    float climb = plan.BaseHeight(3 * Lap) - plan.BaseHeight(2 * Lap);
                    Assert.AreEqual(3 * RouteSurface.StairRise, climb, .0001f);
                    Assert.AreEqual(RouteSurface.StairRise + 9f, climb, .0001f, "Nine metres more than a one-stair lap");
                    checkedLaps++;
                }
            Assert.Greater(checkedLaps, 5);
        }

        [Test] public void HeightsStayInSyncAfterStairPlacement()
        {
            var plan = new RoutePlan(Curve(), 9); RouteSurface.UsePlan(plan);
            for (int lap = 0; lap < 4; lap++) plan.UpdateChoice(DecisionAt(lap), lap % 3 - 1);
            for (int i = 0; i < Lap * 4; i++) Assert.AreEqual(plan.BaseHeight(i) + RouteSurface.Rise(plan.TypeAt(i)), plan.BaseHeight(i + 1), .0001f);
        }

        [Test] public void StairBiomesAreMineOutdoorAndPrison()
        {
            Assert.AreEqual(13, RouteSurface.MineStair); Assert.AreEqual(14, RouteSurface.ZooStair);
            Assert.IsTrue(RouteSurface.IsMine(13)); Assert.IsFalse(RouteSurface.IsOutdoor(13));
            Assert.IsTrue(RouteSurface.IsOutdoor(14)); Assert.IsFalse(RouteSurface.IsMine(14));
            Assert.IsTrue(RouteSurface.IsStair(7) && !RouteSurface.IsOutdoor(7) && !RouteSurface.IsMine(7));
        }

        [TestCase(7)] [TestCase(13)] [TestCase(14)]
        public void StairChunkLayoutHasNoHazardsAtAll(int type)
        {
            var slots = new List<ObstacleSlot>();
            for (int i = 0; i < 6; i++) slots.Add(new ObstacleSlot(ObstacleKind.Barrier, i % 3 - 1, 7 + i / 3 * 12, false));
            var floor = new List<LanePlacement> { new LanePlacement(0, 10) };
            for (int seed = 0; seed < 200; seed++)
                foreach (bool extended in new[] { false, true })
                {
                    var layout = new ObstacleLayout();
                    layout.Plan(new Random(seed), type, 1 + seed % 30, slots, floor, new HazardOdds(1, 1, 1, 1), extended, false);
                    Assert.IsTrue(layout.Stair);
                    foreach (bool on in layout.Slots) Assert.IsFalse(on);
                    foreach (bool on in layout.FloorItems) Assert.IsFalse(on);
                    Assert.IsEmpty(layout.Blockers); Assert.IsEmpty(layout.Bananas); Assert.IsFalse(layout.Animal);
                    Assert.IsNull(layout.Violation());
                }
        }

        [Test] public void WalkingChunksAreNotStairs()
        {
            foreach (int type in new[] { 0, 4, 5, 12 })
            {
                var layout = new ObstacleLayout();
                layout.Plan(new Random(1), type, 3, new List<ObstacleSlot>(), new List<LanePlacement>(), new HazardOdds(.5f, .5f, .5f, .5f), true, false);
                Assert.IsFalse(layout.Stair, "type " + type);
            }
        }

        [Test] public void StairCoinsSitAtKneeHeightAlongTheClimbAndNeedKneeSteps()
        {
            Assert.AreEqual(4f, ObstacleLayout.StairCoinZ(0, 6), .001f); Assert.AreEqual(20f, ObstacleLayout.StairCoinZ(5, 6), .001f);
            Assert.AreEqual(.6f, ObstacleLayout.StairCoinHeight, .001f);
            Assert.IsFalse(ObstacleLayout.CoinReachable(true, false)); Assert.IsTrue(ObstacleLayout.CoinReachable(true, true));
            Assert.IsTrue(ObstacleLayout.CoinReachable(false, false));
        }

        // The runner stands on a walking chunk (never riding) while the judge is told where in the stair it is.
        private static RunnerController Standing() => new RunnerController(new RunConfig());
        private static int Walk(StairJudge judge, RunnerController runner, float from, float to, float stepEvery, bool inStair = true)
        {
            int failures = 0; float t = 0, nextStep = 0; const float dt = .05f;
            for (float z = from; z <= to; z += 9f * dt)
            {
                if (stepEvery > 0 && t >= nextStep) { runner.Command(MotionCommand.KneeStep); nextStep += stepEvery; }
                runner.Tick(dt, 0); t += dt;
                if (judge.Tick(dt, inStair, z, runner)) failures++;
            }
            return failures;
        }

        [Test] public void SteppingEveryHalfSecondNeverFails()
        {
            var judge = new StairJudge(new RunConfig()); var runner = Standing();
            Assert.AreEqual(0, Walk(judge, runner, 0, 24, .5f)); Assert.IsFalse(judge.Failed);
        }
        [Test] public void HoldingWithoutStepsFailsExactlyOncePerChunk()
        {
            var judge = new StairJudge(new RunConfig()); var runner = Standing();
            Assert.AreEqual(1, Walk(judge, runner, 0, 24, 0)); Assert.IsTrue(judge.Failed);
            // Leaving the stair and entering another one resets the judge.
            Walk(judge, runner, 0, 24, 0, false);
            Assert.AreEqual(1, Walk(judge, runner, 0, 24, 0));
        }
        [Test] public void TheFirstTwoMetresAreGraceAndTheFirstStepIsDueAtTwo()
        {
            var judge = new StairJudge(new RunConfig()); var runner = Standing();
            Assert.AreEqual(0, Walk(judge, runner, 0, 1.99f, 0), "No failure before z 2");
            Assert.IsFalse(judge.InStair);
            Assert.IsTrue(judge.Tick(.05f, true, 2f, runner), "No step yet at z 2");
        }
        [Test] public void AStepJustBeforeTheJudgedSpanKeepsTheRunnerSafeForTheGap()
        {
            var judge = new StairJudge(new RunConfig()); var runner = Standing();
            runner.Command(MotionCommand.KneeStep); runner.Tick(.5f, 0);
            Assert.IsFalse(judge.Tick(.05f, true, 2f, runner));
            runner.Tick(.3f, 0); Assert.IsTrue(judge.Tick(.05f, true, 2.5f, runner), "0.8 s since the step is a miss");
        }
        [Test] public void LocalZWrappingBackStartsANewChunk()
        {
            var judge = new StairJudge(new RunConfig()); var runner = Standing();
            Assert.IsTrue(judge.Tick(.05f, true, 10f, runner)); Assert.IsFalse(judge.Tick(.05f, true, 12f, runner));
            Assert.IsTrue(judge.Tick(.05f, true, 3f, runner), "z went back: another stair chunk");
        }
        [Test] public void HudValuesTrackProgressAndUrgency()
        {
            var judge = new StairJudge(new RunConfig()); var runner = Standing();
            runner.Command(MotionCommand.KneeStep); judge.Tick(.05f, true, 12f, runner);
            Assert.IsTrue(judge.InStair); Assert.AreEqual(.5f, judge.StairProgress, .001f); Assert.AreEqual(0f, judge.KneeUrgency, .001f);
            runner.Tick(.35f, 0); judge.Tick(.05f, true, 12.5f, runner); Assert.AreEqual(.5f, judge.KneeUrgency, .001f);
            judge.Tick(.05f, false, 12.5f, runner); Assert.IsFalse(judge.InStair); Assert.AreEqual(0f, judge.StairProgress);
        }

        private static RunSession Running()
        {
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.IntroTimeline);
            run.Flow.Transition(GameFlowState.FirstPersonTakeover); run.Flow.Transition(GameFlowState.Running); return run;
        }
        [Test] public void SessionCountsAMissedStairAsOneKneeMissStrike()
        {
            // Authored lap: chunk 4 is the prison stair (z 96..120).
            var run = Running(); run.Runner.SetStartZ(95f);
            bool slowed = false;
            for (int i = 0; i < 200 && run.Runner.Z < 119f; i++) { run.Tick(.05f); slowed |= run.SlipStrikes == 1 && run.Runner.Slowing; }
            Assert.AreEqual(1, run.SlipStrikes, "One strike, however long the climb lasts");
            Assert.IsTrue(slowed, "The miss slows the runner");
        }
        [Test] public void SessionLetsASteppingRunnerClimbCleanly()
        {
            var run = Running(); run.Runner.SetStartZ(95f);
            for (int i = 0; i < 400 && run.Runner.Z < 119f; i++) { if (StairJudge.StepDue(run.Runner)) run.Runner.Command(MotionCommand.KneeStep); run.Tick(.05f); }
            Assert.AreEqual(0, run.SlipStrikes); Assert.IsFalse(run.Runner.Slowing);
        }
        [Test] public void InTheTutorialAMissedStairIsACountedMissNotAStrike()
        {
            var run = Running(); run.IsTutorial = true; run.Runner.SetStartZ(95f);
            for (int i = 0; i < 200 && run.Runner.Z < 119f; i++) run.Tick(.05f);
            Assert.AreEqual(0, run.SlipStrikes); Assert.AreEqual(1, run.TutorialMisses);
        }
    }
}
