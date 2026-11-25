using System;
using System.Collections.Generic;
using Muhanok.Domain;
using Muhanok.Presentation;
using NUnit.Framework;

namespace Muhanok.Tests
{
    // Fixes from the exercise patch QA report of 2026-11-25 (H1-H2, M1-M7, L1-L17): pace on the stairs and out of view,
    // the weekly moderate minutes, faked steps and held crouches, the ranking strip rows and the short tutorial's stairs.
    public sealed class QaFixesTests
    {
        static readonly MotionTuning Tuning = new MotionTuning();

        // H2: on the stairs the pace rests (no slow-down, no lag, the police fall back) even with no steps at all.
        [Test] public void OnTheStairsThePaceRestsAndThePoliceFallBack()
        {
            var pace = new CadencePace();
            for (int i = 0; i < 80; i++) pace.Tick(.1f, true, 0, Tuning, 90, true, false);
            Assert.IsTrue(pace.Lagging); Assert.Greater(pace.Pressure, .5f); Assert.AreEqual(CadencePace.Walk, pace.Scale, 1e-4);
            for (int i = 0; i < 60; i++) pace.Tick(.1f, true, 0, Tuning, 90, true, true);
            Assert.IsFalse(pace.Lagging, "No lag on the stairs");
            Assert.AreEqual(0, pace.Pressure, 1e-4, "The police fall back on the stairs");
            Assert.AreEqual(CadencePace.Run, pace.Scale, 1e-4, "No slow-down below the running speed on the stairs");
            for (int i = 0; i < 20; i++) pace.Tick(.1f, true, 170, Tuning, 90, true, true);
            Assert.AreEqual(CadencePace.Sprint, pace.Scale, 1e-4, "A fast stair rhythm still speeds up");
        }

        // M6 / L15: out of view everything holds; stepping out of the camera is never better than standing.
        [Test] public void OutOfViewThePaceHoldsItsSpeedLagAndPolice()
        {
            var pace = new CadencePace();
            for (int i = 0; i < 80; i++) pace.Tick(.1f, true, 0, Tuning, 90, true, false);
            float scale = pace.Scale, pressure = pace.Pressure, lag = pace.LagFor;
            for (int i = 0; i < 100; i++) pace.Tick(.1f, true, 0, Tuning, 90, false, false);
            Assert.AreEqual(scale, pace.Scale); Assert.AreEqual(pressure, pace.Pressure); Assert.AreEqual(lag, pace.LagFor);
            // The option off (or keyboard) is still the plain base speed with no police.
            var off = new CadencePace();
            for (int i = 0; i < 80; i++) off.Tick(.1f, false, 0, Tuning, 90, false, false);
            Assert.AreEqual(CadencePace.Run, off.Scale); Assert.AreEqual(0, off.Pressure);
        }

        // H2: the coach never asks for a faster pace on the stairs.
        [Test] public void TheCoachNeverAsksForSpeedOnTheStairs()
        {
            var coach = new Coach();
            Assert.AreEqual(CoachLine.None, coach.Tick(100, .1f, new Coach.Input { Lagging = true, OnStairs = true }));
            Assert.AreEqual(CoachLine.PaceUp, coach.Tick(101, .1f, new Coach.Input { Lagging = true }));
        }

        // H1 / M5: moderate is judged after the play-style scale; a full body run without cadence counts its motion moments.
        [Test] public void ModerateMinutesFollowTheScaledIntensity()
        {
            var off = new WorkoutEstimate { CadenceCounts = false };
            off.TickIntensity(30, true, true, CadenceStage.Standing, true, false, 1);
            off.TickIntensity(30, true, true, CadenceStage.Standing, true, true, 2);
            Assert.AreEqual(60, off.ModerateSeconds, "Cadence off: the motion moments are moderate (QA H1)");
            Assert.AreEqual((5 * 30 + 6 * 30), off.MetSeconds, 1e-9);
            var upper = new WorkoutEstimate { StyleScale = IntensityMet.Scale(PlayStyle.UpperBody) };
            upper.TickIntensity(60, true, true, CadenceStage.Sprint, false, false, 0);
            Assert.AreEqual(0, upper.ModerateSeconds, "7 x 0.6 = 4.2 MET is not moderate (QA M5)");
            Assert.IsTrue(IntensityMet.IsModerate(5)); Assert.IsFalse(IntensityMet.IsModerate(4.8));
        }

        // Webcam item 3: a crouch held still stops counting as moving (and a deep one stops the bonus) after a moment.
        [Test] public void AHeldCrouchStopsCountingAsMoving()
        {
            var tally = new MoveTally(); var w = new WorkoutEstimate();
            tally.Observe(0, 0, -10, false, -10, 0, w);
            for (double t = .1; t < 5; t += .1) tally.Observe(t, 0, -10, true, -10, 0, w);
            Assert.IsFalse(tally.Moving(5), "Squatting still for seconds is resting");
            Assert.AreEqual(1, w.Moves(MoveKind.Crouch));
            var fresh = new MoveTally();
            fresh.Observe(0, 0, -10, false, -10, 0, w); fresh.Observe(.1, 0, -10, true, -10, 0, w); fresh.Observe(1, 0, -10, true, -10, 0, w);
            Assert.IsTrue(fresh.Moving(1), "A crouch under 1.5 s is still exercise");
            Assert.IsTrue(MoveTally.Fresh(0, 1.4)); Assert.IsFalse(MoveTally.Fresh(0, 1.5));
        }

        // M4: one row per guest name (its best) on the strip; "내 기록만" lists the member's own runs.
        [Test] public void TheStripHasOneRowPerGuestName()
        {
            var now = DateTime.Now; long t = now.Ticks;
            var entries = new List<RankingEntry>
            {
                new RankingEntry { Name = "탈옥왕", Score = 640, Ticks = t }, new RankingEntry { Name = "탈옥왕", Score = 455, Ticks = t },
                new RankingEntry { Name = "점프맨", Score = 300, Ticks = t }, new RankingEntry { Name = "회원", Score = 900, Ticks = t, AccountId = "m1" },
                new RankingEntry { Name = "지난주", Score = 999, Ticks = now.AddDays(-30).Ticks },
            };
            var rows = RankRace.LocalRows(entries, now.AddDays(-7), false, "");
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("탈옥왕", rows[0].Name); Assert.AreEqual(640, rows[0].Score);
            Assert.AreEqual("점프맨", rows[1].Name);
            var mine = RankRace.LocalRows(entries, now.AddDays(-7), true, "m1");
            Assert.AreEqual(1, mine.Count); Assert.AreEqual(RankRace.MineName, mine[0].Name); Assert.AreEqual(900, mine[0].Score);
        }

        // M2: the short update tutorial forgives a knee miss on its stair chunk; the full one does not.
        [Test] public void TheShortTutorialForgivesStairMisses()
        {
            var shortScript = new TutorialScript(PlayStyle.FullBody, true, true, true);
            var fullScript = new TutorialScript(PlayStyle.FullBody, true, true, false);
            Assert.IsTrue(TutorialCourse.ForgivesStairMiss(shortScript, TutorialCourse.StairChunk));
            Assert.IsFalse(TutorialCourse.ForgivesStairMiss(shortScript, TutorialCourse.BananaChunk));
            Assert.IsFalse(TutorialCourse.ForgivesStairMiss(fullScript, TutorialCourse.StairChunk));
            var keyboardShort = new TutorialScript(PlayStyle.Keyboard, true, true, true);
            Assert.IsTrue(TutorialCourse.ForgivesStairMiss(keyboardShort, TutorialCourse.StairChunk));
        }

        // Webcam items 1, 2 and H2 in the synthetic recordings: faked steps read as standing, real ones still count,
        // and the upper body's pull-down on the stairs is a walking cadence.
        [TestCase("synthetic-cheat-small-steps")] [TestCase("synthetic-upper-arms")] [TestCase("synthetic-upper-stairs")]
        public void FakedStepsReadAsStandingAndRealOnesCount(string name)
        {
            SyntheticPoseRecordings.Build(name, out var recording, out var labels);
            var result = PoseScorecard.Score(name, recording, labels);
            foreach (var line in result.Lines.Values)
                if (!double.IsNaN(line.Accuracy)) Assert.GreaterOrEqual(line.Accuracy, .9, name + " " + PoseScorecard.Korean(line.Action));
            if (name == "synthetic-upper-stairs") Assert.AreEqual(0, result.Lines[PoseAction.Knee].False);
        }

        // M1: the cadence-off note sits under the cadence button and clear of 시작하기 (640, 584, 450, 56), inside the panel.
        [Test] public void TheCadenceOffNoteKeepsClearOfTheStartButton()
        {
            // SetupScreen is internal to the presentation assembly.
            var field = typeof(WorkoutReport).Assembly.GetType("Muhanok.Presentation.SetupScreen").GetField("CadenceOffHintRect");
            var note = (UnityEngine.Rect)field.GetValue(null);
            Assert.LessOrEqual(note.yMax, 584f); Assert.GreaterOrEqual(note.y, 540f);
            Assert.LessOrEqual(note.xMax, 1130f - 20f, "Inside the setup panel");
        }

        // L2 / L10: the goal names what it counts; a guest's line is this run's.
        [Test] public void TheWeeklyGoalSaysWhatItCounts()
        {
            Assert.AreEqual("주간 중강도 37 / 150분", WorkoutReport.WeekGoal(37.6));
            Assert.AreEqual("이번 판 중강도 4분", WorkoutReport.RunModerate(4.9));
            StringAssert.Contains("중강도", WorkoutReport.WeekGoalTitle);
        }
    }
}
