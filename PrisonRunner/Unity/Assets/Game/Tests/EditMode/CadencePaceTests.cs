using System.Collections.Generic;
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    // Exercise plan stage 3: cadence -> runner speed and police, time-based hazard spacing, the running ranking strip.
    public sealed class CadencePaceTests
    {
        static readonly MotionTuning Tuning = new MotionTuning();

        [Test] public void SpeedFollowsTheCadenceStagesWithSmoothBlends()
        {
            Assert.AreEqual(.7f, CadencePace.Factor(0, Tuning)); Assert.AreEqual(.7f, CadencePace.Factor(80, Tuning));
            Assert.AreEqual(1f, CadencePace.Factor(120, Tuning)); Assert.AreEqual(1f, CadencePace.Factor(140, Tuning));
            Assert.AreEqual(1.3f, CadencePace.Factor(160, Tuning)); Assert.AreEqual(1.3f, CadencePace.Factor(220, Tuning));
            float previous = CadencePace.Factor(0, Tuning);
            for (int spm = 0; spm <= 200; spm += 2) { float f = CadencePace.Factor(spm, Tuning); Assert.GreaterOrEqual(f, previous, "No dips at " + spm); Assert.Less(f - previous, .08f, "No jumps at " + spm); previous = f; }
            Assert.AreEqual(.85f, CadencePace.Factor(100, Tuning), .01f, "Halfway between walking and running at 100");
        }

        [Test] public void TheScaleEasesTowardTheCadenceAndOffKeepsBaseSpeed()
        {
            var pace = new CadencePace();
            for (int i = 0; i < 10; i++) pace.Tick(.1f, true, 170, Tuning, 90);
            Assert.AreEqual(1.3f, pace.Scale, .001f, "0.6 a second: a full second reaches the sprint factor from 1");
            pace.Tick(.1f, true, 0, Tuning, 90); Assert.AreEqual(1.24f, pace.Scale, .001f, "One frame without steps does not drop the speed at once");
            var off = new CadencePace();
            for (int i = 0; i < 50; i++) off.Tick(.1f, false, 0, Tuning, 90);
            Assert.AreEqual(1f, off.Scale); Assert.AreEqual(0, off.Pressure); Assert.IsFalse(off.Lagging);
        }

        [Test] public void PoliceCloseInOnlyAfterThreeSecondsBelowTargetAndNeverCatch()
        {
            var pace = new CadencePace();
            for (int i = 0; i < 29; i++) pace.Tick(.1f, true, 60, Tuning, 90);
            Assert.IsFalse(pace.Lagging); Assert.AreEqual(0, pace.Pressure, "Under 3 s: nothing yet");
            for (int i = 0; i < 300; i++) pace.Tick(.1f, true, 60, Tuning, 90);
            Assert.IsTrue(pace.Lagging); Assert.AreEqual(CadencePace.MaxPressure, pace.Pressure, 1e-5, "Capped below a capture");
            pace.Tick(.1f, true, 130, Tuning, 90); Assert.IsFalse(pace.Lagging, "Back on pace at once");
            for (int i = 0; i < 20; i++) pace.Tick(.1f, true, 130, Tuning, 90);
            Assert.AreEqual(0, pace.Pressure, 1e-5, "The police fall back within two seconds");
            Assert.AreEqual(90, CadencePace.Target(Tuning));
        }

        [Test] public void RunnerSpeedUsesTheCadenceOnFootOnly()
        {
            var run = new RunSession(new RunConfig()); run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            float baseSpeed = run.CurrentSpeed;
            run.CadenceScale = 1.3f; Assert.AreEqual(baseSpeed * 1.3f, run.CurrentSpeed, 1e-4);
            run.CadenceScale = .7f; Assert.AreEqual(baseSpeed * .7f, run.CurrentSpeed, 1e-4);
            run.PacePressure = .5f; Assert.AreEqual(.5f, run.PolicePressure, 1e-6); Assert.IsFalse(run.Chase.IsCaught, "Pace pressure is separate from the capture pressure");
            run.Reset(); Assert.AreEqual(1f, run.CadenceScale); Assert.AreEqual(0, run.PacePressure);
        }

        [Test] public void JumpsKeepTheirTimeGapAtAnySpeed()
        {
            var pacer = new HazardPacer();
            Assert.IsTrue(pacer.TryJump(100, 10));
            Assert.IsFalse(pacer.TryJump(111, 10), "1.1 s after the last jump");
            Assert.IsTrue(pacer.TryJump(112, 10), "1.2 s");
            var fast = new HazardPacer(); fast.TryJump(100, 20);
            Assert.IsFalse(fast.TryJump(112, 20), "The same 12 m is 0.6 s at 20 m/s");
            Assert.IsTrue(fast.TryJump(124, 20));
            Assert.AreEqual(1, fast.Dropped);
        }

        [Test] public void AtMostThreeJumpsInARowThenABreak()
        {
            var pacer = new HazardPacer();
            Assert.IsTrue(pacer.TryJump(0, 10)); Assert.IsTrue(pacer.TryJump(13, 10)); Assert.IsTrue(pacer.TryJump(26, 10));
            Assert.AreEqual(3, pacer.Streak);
            Assert.IsFalse(pacer.TryJump(40, 10), "A fourth jump 1.4 s later waits for the break");
            Assert.IsTrue(pacer.TryJump(53, 10), "2.7 s after the third is a new streak");
            Assert.AreEqual(1, pacer.Streak);
            pacer.TryJump(66, 10); pacer.TryJump(79, 10); pacer.Other(85);
            Assert.IsTrue(pacer.TryJump(92, 10), "A duck in between breaks the streak");
            Assert.AreEqual(1, pacer.Streak);
        }

        [Test] public void PlannedChunksNeverPlaceJumpsCloserThanTheGap()
        {
            var pacer = new HazardPacer();
            var rows = new List<(float z, HazardAction action)>();
            for (int seed = 1; seed < 200; seed++)
            {
                var random = new System.Random(seed); pacer.Reset(); rows.Clear();
                var slots = new List<ObstacleSlot> { new ObstacleSlot(ObstacleKind.Crate, 0, 7, true), new ObstacleSlot(ObstacleKind.Pipe, 0, 19, true), new ObstacleSlot(ObstacleKind.Crate, 0, 13, true) };
                for (int sequence = 1; sequence < 30; sequence++)
                {
                    var layout = new ObstacleLayout();
                    layout.Plan(random, 4, sequence, slots, new List<LanePlacement>(), new HazardOdds(1, 0, 0, 0), true, false, false, pacer, 18f);
                    for (int i = 0; i < slots.Count; i++) if (layout.Slots[i]) rows.Add((sequence * RouteSurface.Length + layout.ZOf(i), slots[i].Action));
                    Assert.IsNull(layout.Violation());
                }
                float last = float.NegativeInfinity;
                foreach (var row in rows) if (row.action == HazardAction.Jump) { Assert.GreaterOrEqual((row.z - last) / 18f, HazardPacer.JumpGapSeconds - 1e-4, "seed " + seed); last = row.z; }
            }
        }

        [Test] public void DuckRowsAheadRaiseTheJumpLine()
        {
            var classifier = new MotionClassifier(Tuning);
            float plain = classifier.JumpThreshold;
            classifier.SetDuckAhead(true);
            Assert.AreEqual(plain * Tuning.JumpDuckFactor, classifier.JumpThreshold, 1e-5);
            classifier.SetDuckAhead(false); Assert.AreEqual(plain, classifier.JumpThreshold, 1e-5);
        }

        [Test] public void TheStripShowsTwoAboveOneBelowAndQueuesEveryRowPassed()
        {
            var race = new RankRace();
            race.Load(new[] { new RankRace.Row("A", 4210), new RankRace.Row("B", 3980), new RankRace.Row("C", 3700), new RankRace.Row("D", 1000) });
            race.Update(3830);
            Assert.AreEqual(2, race.Pending, "D and C were passed on the way up"); while (race.TakeOvertake(out _)) { }
            Assert.AreEqual(3, race.Rank); CollectionAssert.AreEqual(new[] { 0, 1, -1, 2 }, race.Window());
            Assert.AreEqual(151, race.ToNext, "3980 - 3830 + 1 to pass B"); Assert.IsFalse(race.Close);
            race.Update(3940); Assert.IsTrue(race.Close, "Within 50 points");
            race.Update(4300);
            Assert.AreEqual(1, race.Rank); Assert.IsTrue(race.First); Assert.AreEqual(2, race.Pending);
            Assert.IsTrue(race.TakeOvertake(out var first)); Assert.AreEqual("B", first.Name, "Nearest row first");
            Assert.IsTrue(race.TakeOvertake(out var second)); Assert.AreEqual("A", second.Name);
            Assert.IsFalse(race.TakeOvertake(out _));
            race.Update(4210 - 1); Assert.AreEqual(2, race.Rank, "Dropping back passes nobody"); Assert.AreEqual(0, race.Pending);
            race.Update(4210); Assert.AreEqual(2, race.Rank, "A tie stays above");
        }

        [Test] public void AnEmptyBoardGetsPracticeRows()
        {
            var race = new RankRace(); race.Load(null);
            Assert.IsTrue(race.Practice); Assert.AreEqual(RankRace.PracticeScores.Length, race.Others.Count);
            Assert.AreEqual(RankRace.PracticeName, race.Others[0].Name); Assert.AreEqual(RankRace.PracticeScores.Length + 1, race.Rank);
            race.Update(350); Assert.AreEqual(1, race.Pending);
        }
    }
}
