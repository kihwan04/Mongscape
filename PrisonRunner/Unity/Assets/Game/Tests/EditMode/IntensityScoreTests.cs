using System;
using Muhanok.Application;
using Muhanok.Domain;
using Muhanok.Presentation;
using NUnit.Framework;

namespace Muhanok.Tests
{
    // Exercise plan stage 2: intensity MET from the cadence, score version 3 (a new ranking season), the result report.
    public sealed class IntensityScoreTests
    {
        [Test] public void MetFollowsTheCadenceStageAndBigMovesAddOne()
        {
            Assert.AreEqual(0, IntensityMet.Of(CadenceStage.Standing, true, 1));
            Assert.AreEqual(3.5, IntensityMet.Of(CadenceStage.Walk, false, 1));
            Assert.AreEqual(5, IntensityMet.Of(CadenceStage.Run, false, 1));
            Assert.AreEqual(7, IntensityMet.Of(CadenceStage.Sprint, false, 1));
            Assert.AreEqual(8, IntensityMet.Of(CadenceStage.Sprint, true, 1));
            Assert.AreEqual(3, IntensityMet.Of(CadenceStage.Run, false, IntensityMet.Scale(PlayStyle.UpperBody)), 1e-9, "Upper body is 0.6 of the table");
            Assert.AreEqual(0, IntensityMet.Scale(PlayStyle.Keyboard));
        }

        [Test] public void StandingStillCountsNothingAndOtherMovesCountAsWalking()
        {
            var w = new WorkoutEstimate();
            w.TickIntensity(10, true, true, CadenceStage.Standing, false, false, 0);
            Assert.AreEqual(0, w.ActiveSeconds, "No grace period: standing is not exercise");
            w.TickIntensity(2, true, true, CadenceStage.Standing, true, false, 1);
            Assert.AreEqual(2, w.ActiveSeconds); Assert.AreEqual(7, w.MetSeconds, 1e-9, "A jump with still feet counts as walking (3.5 MET)");
            w.TickIntensity(5, true, false, CadenceStage.Sprint, true, false, 0);
            w.TickIntensity(5, false, true, CadenceStage.Sprint, true, false, 0);
            w.TickIntensity(double.NaN, true, true, CadenceStage.Sprint, true, false, 0);
            Assert.AreEqual(2, w.ActiveSeconds, "Out of view, paused or a bad time slice add nothing");
        }

        [Test] public void CaloriesAndScoreGrowWithIntensity()
        {
            var walk = new WorkoutEstimate(); var sprint = new WorkoutEstimate();
            walk.TickIntensity(600, true, true, CadenceStage.Walk, false, false, 0);
            sprint.TickIntensity(600, true, true, CadenceStage.Sprint, false, false, 0);
            // 65 kg, 10 minutes: 3.5 MET -> 39.8 kcal, 7 MET -> 79.6 kcal.
            Assert.AreEqual(3.5 * 3.5 * 65 / 200 * 10, walk.EstimatedKcal, 1e-6);
            Assert.AreEqual(7 * 3.5 * 65 / 200 * 10, sprint.EstimatedKcal, 1e-6);
            Assert.Greater(RunScore.From(1000, sprint), RunScore.From(1000, walk));
            Assert.AreEqual(Math.Round(1000 + 7 * 3.5 * 65 / 200 * 10 * 100, MidpointRounding.AwayFromZero), RunScore.From(1000, sprint));
            Assert.AreEqual(600, sprint.StageSeconds(CadenceStage.Sprint)); Assert.AreEqual(0, sprint.StageSeconds(CadenceStage.Walk));
            Assert.AreEqual(600, sprint.ModerateSeconds); Assert.AreEqual(0, walk.ModerateSeconds, "Walking is below 5 MET");
        }

        [Test] public void UpperBodyScoresLessForTheSameCadenceAndItsArmsAreNotModerate()
        {
            var full = new WorkoutEstimate(); var upper = new WorkoutEstimate { StyleScale = IntensityMet.Scale(PlayStyle.UpperBody) };
            full.TickIntensity(60, true, true, CadenceStage.Run, false, false, 0); upper.TickIntensity(60, true, true, CadenceStage.Run, false, false, 0);
            Assert.AreEqual(full.EstimatedKcal * .6, upper.EstimatedKcal, 1e-9);
            // QA 2026-11-25 M5: moderate is judged after the play-style scale; arm swings at a run are 3 MET, a sprint 4.2.
            Assert.AreEqual(0, upper.ModerateSeconds, "Upper-body running is 3 MET");
            upper.TickIntensity(60, true, true, CadenceStage.Sprint, false, true, 0);
            Assert.AreEqual(0, upper.ModerateSeconds, "Even a big sprint moment (4.8 MET) is under 5");
            Assert.AreEqual(60, full.ModerateSeconds);
        }

        [Test] public void WeightChangesCaloriesButNotTheScore()
        {
            var light = new WorkoutEstimate(); light.SetWeight("50"); var heavy = new WorkoutEstimate(); heavy.SetWeight("110");
            foreach (var w in new[] { light, heavy }) w.TickIntensity(120, true, true, CadenceStage.Run, false, true, 0);
            Assert.Less(light.EstimatedKcal, heavy.EstimatedKcal);
            Assert.AreEqual(RunScore.From(300, light), RunScore.From(300, heavy));
            Assert.AreEqual(120, heavy.BigSeconds);
        }

        [Test] public void ActivityCaloriesAddUpToTheRun()
        {
            var w = new WorkoutEstimate();
            w.TickIntensity(30, true, true, CadenceStage.Run, false, false, 0); w.TickIntensity(10, true, true, CadenceStage.Sprint, true, false, 1);
            double sum = 0; for (int i = 0; i < 5; i++) sum += w.KcalFor(i);
            Assert.AreEqual(w.EstimatedKcal, sum, 1e-9); Assert.AreEqual(10, w.SecondsFor(1));
            var run = AccountRun.Create(new RunSession(new RunConfig()), PlayStyle.FullBody, 0);
            Assert.AreEqual(3, run.score_version, "Runs go to the server as score version 3");
        }

        [Test] public void MoveTallyCountsEachMoveOnceAndMarksRecentMovement()
        {
            var w = new WorkoutEstimate(); var tally = new MoveTally();
            tally.Observe(0, 40, -10, false, -10, 0, w);
            Assert.AreEqual(0, w.Moves(MoveKind.Step), "The first look only sets the starting point");
            tally.Observe(1, 43, -10, false, -10, 0, w); Assert.AreEqual(3, w.Moves(MoveKind.Step)); Assert.IsFalse(tally.Moving(1), "Steps are the cadence, not other movement");
            tally.Observe(2, 43, 1.9, false, -10, 0, w); tally.Observe(2.1, 43, 1.9, false, -10, 0, w);
            Assert.AreEqual(1, w.Moves(MoveKind.Jump)); Assert.IsTrue(tally.Moving(2.5)); Assert.IsFalse(tally.Moving(2.8));
            tally.Observe(3, 43, 1.9, true, -10, 0, w); tally.Observe(3.5, 43, 1.9, true, -10, 0, w); tally.Observe(4, 43, 1.9, false, -10, 0, w);
            Assert.AreEqual(1, w.Moves(MoveKind.Crouch)); Assert.IsTrue(tally.Moving(4.1), "Holding a crouch is movement");
            tally.Observe(5, 43, 1.9, false, 4.9, 1, w); tally.Observe(6, 43, 1.9, false, 5.8, 0, w);
            Assert.AreEqual(2, w.Moves(MoveKind.KneeStep)); Assert.AreEqual(2, w.Moves(MoveKind.SideStep));
            tally.Observe(7, 10, 1.9, false, 5.8, 0, w); Assert.AreEqual(3, w.Moves(MoveKind.Step), "A lower count (new classifier) adds nothing");
            w.Reset(); Assert.AreEqual(0, w.Moves(MoveKind.Jump));
        }

        [Test] public void OlderSeasonRecordsStaySavedButOnlyVersionThreeRanks()
        {
            var board = new Leaderboard(); var now = DateTime.Now.Ticks;
            board.Add(new RankingEntry { Name = "A", Score = 5000, ScoreVersion = 2, Mode = 0, AccountId = "m1", Ticks = now });
            board.Add(new RankingEntry { Name = "A", Score = 4000, ScoreVersion = 2, Mode = 0, AccountId = "m1", Ticks = now + 1 });
            board.Add(new RankingEntry { Name = "G", Score = 900, ScoreVersion = 2, Mode = 0, Ticks = now + 2 });
            Assert.IsTrue(board.HasOlderSeason(PlayStyle.FullBody)); Assert.IsFalse(board.HasOlderSeason(PlayStyle.UpperBody));
            Assert.IsEmpty(board.View(false, DateTime.Now, PlayStyle.FullBody), "The new season starts empty");
            Assert.AreEqual(1, board.Add(new RankingEntry { Name = "A", Score = 100, ScoreVersion = RunScore.Version, Mode = 0, AccountId = "m1", Ticks = now + 3 }));
            Assert.AreEqual(4, board.All.Count, "Nothing was deleted");
            Assert.AreEqual(5000, board.PreviousSeasonBest(PlayStyle.FullBody, "m1", "A"));
            Assert.AreEqual(900, board.PreviousSeasonBest(PlayStyle.FullBody, "", "G"));
            Assert.AreEqual(0, board.PreviousSeasonBest(PlayStyle.UpperBody, "m1", "A"));
        }

        [Test] public void ProfileShowsThePreviousSeasonRecord()
        {
            var stats = new StatsReply { ok = true, previous_best = new[] { 3210, 0, 55 } };
            Assert.AreEqual("이전 시즌 기록 3,210점", ProfileForm.Previous(stats, 0));
            Assert.AreEqual("", ProfileForm.Previous(stats, 1)); Assert.AreEqual("", ProfileForm.Previous(new StatsReply { previous_best = null }, 0));
        }

        [Test] public void ReportShowsIntensityMovesAndTheWeekWithoutGrades()
        {
            var w = new WorkoutEstimate();
            w.TickIntensity(90, true, true, CadenceStage.Walk, false, false, 0); w.TickIntensity(30, true, true, CadenceStage.Sprint, false, false, 0);
            var bars = WorkoutReport.Intensity(w);
            Assert.AreEqual(new[] { "걷기", "달리기", "전력" }, bars.ConvertAll(b => b.label).ToArray());
            Assert.AreEqual(.75f, bars[0].share, 1e-6); Assert.AreEqual(0, bars[1].share); Assert.AreEqual("1:30", WorkoutReport.Clock(bars[0].seconds));
            w.CountMove(MoveKind.Jump, 12);
            var moves = WorkoutReport.Moves(w);
            Assert.AreEqual(5, moves.Count); Assert.AreEqual("점프", moves[1].Key); Assert.AreEqual("12회", moves[1].Value);
            foreach (var m in moves) StringAssert.DoesNotContain("완벽", m.Key);
            var stats = new StatsReply { week_kcal = 120, week_seconds = 1800, last_week_kcal = 100, last_week_seconds = 1500 };
            Assert.AreEqual("120kcal · 30분", WorkoutReport.Week(stats)[0].Value);
            // A Sunday (KST) has the whole week: the daily averages compare like the totals.
            var sunday = new DateTime(2026, 11, 29, 3, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual("하루 평균 20% 더", WorkoutReport.WeekChange(stats, sunday));
            Assert.AreEqual("하루 평균 50% 덜", WorkoutReport.WeekChange(new StatsReply { week_kcal = 50, last_week_kcal = 100 }, sunday));
            Assert.AreEqual("지난주 기록 없음 · 이번 주가 시작이에요", WorkoutReport.WeekChange(new StatsReply { week_kcal = 5 }, sunday));
            // QA 2026-11-25 L1: on a Monday, 20 kcal against last week's 140 (20 a day) is the same pace, not "86% 덜".
            var monday = new DateTime(2026, 11, 23, 3, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual("하루 평균 같아요", WorkoutReport.WeekChange(new StatsReply { week_kcal = 20, last_week_kcal = 140 }, monday));
            Assert.AreEqual("하루 평균 50% 더", WorkoutReport.WeekChange(new StatsReply { week_kcal = 30, last_week_kcal = 140 }, monday));
            Assert.IsEmpty(WorkoutReport.Week(null));
        }
    }
}
