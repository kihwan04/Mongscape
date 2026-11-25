using System;
using System.Collections.Generic;
using Muhanok.Domain;
using Muhanok.Presentation;
using NUnit.Framework;

namespace Muhanok.Tests
{
    // Exercise plan stage 5: map courses and their move mix, the fork's destinations shown before the choice, the weekly
    // goal and streak, today's challenge.
    public sealed class CourseAndWeekTests
    {
        [Test] public void EachCourseHasItsMixAndLabels()
        {
            Assert.AreEqual(ExerciseKind.Strength, MapCourse.KindOf(0)); Assert.AreEqual(ExerciseKind.Cardio, MapCourse.KindOf(-1)); Assert.AreEqual(ExerciseKind.Balance, MapCourse.KindOf(1));
            Assert.AreEqual("감옥 · 근력 코스", MapCourse.Course(0)); Assert.AreEqual("●●○", MapCourse.DotText(ExerciseKind.Balance));
            var prison = MapCourse.Mix(ExerciseKind.Strength);
            Assert.AreEqual(.5f, prison.Factor(HazardAction.Jump)); Assert.AreEqual(1.5f, prison.Factor(HazardAction.Crouch)); Assert.AreEqual(.5f, prison.Factor(HazardAction.Dodge));
            var zoo = MapCourse.Mix(ExerciseKind.Cardio); Assert.AreEqual(1.5f, zoo.Factor(HazardAction.Jump)); Assert.AreEqual(1.5f, zoo.Factor(HazardAction.Dodge));
            Assert.AreEqual(1f, MapCourse.Mix(ExerciseKind.Balance).Factor(HazardAction.Crouch));
            Assert.AreEqual(3, MapCourse.Stairs(2, 0)); Assert.AreEqual(3, MapCourse.Stairs(3, 0)); Assert.AreEqual(1, MapCourse.Stairs(1, -1)); Assert.AreEqual(2, MapCourse.Stairs(2, 1));
            Assert.Less(MapCourse.TargetCadence(ExerciseKind.Warmup, null), MapCourse.TargetCadence(ExerciseKind.Strength, null), "The warm-up asks for walking");
        }

        [Test] public void TheMixShiftsJumpAndDuckRows()
        {
            var slots = new List<ObstacleSlot> { new ObstacleSlot(ObstacleKind.Crate, 0, 7, true), new ObstacleSlot(ObstacleKind.Pipe, 0, 17, true) };
            int Count(ActionMix mix, HazardAction action)
            {
                int n = 0; var random = new Random(5);
                for (int i = 0; i < 2000; i++)
                {
                    var layout = new ObstacleLayout(); layout.Plan(random, 4, 1 + i, slots, new List<LanePlacement>(), new HazardOdds(0, 0, 0, 0), true, false, false, null, 0, mix);
                    for (int s = 0; s < slots.Count; s++) if (layout.Slots[s] && slots[s].Action == action) n++;
                }
                return n;
            }
            var prison = MapCourse.Mix(ExerciseKind.Strength); var zoo = MapCourse.Mix(ExerciseKind.Cardio);
            Assert.Greater(Count(prison, HazardAction.Crouch), Count(prison, HazardAction.Jump) * 1.5, "Prison: more ducks than jumps");
            Assert.Greater(Count(zoo, HazardAction.Jump), Count(zoo, HazardAction.Crouch) * 1.5, "Zoo: more jumps than ducks");
            Assert.Greater(Count(ActionMix.Neutral, HazardAction.Jump), 0); Assert.Greater(Count(prison, HazardAction.Jump), 0, "Every course keeps all four moves");
        }

        [Test] public void TheForkShowsWhereEachBranchLeadsAndTheChoiceMatches()
        {
            var config = new RunConfig { ExtendedRoutes = true };
            for (int seed = 1; seed < 30; seed++)
                for (int lane = -1; lane <= 1; lane++)
                {
                    var plan = new RoutePlan(config, seed);
                    int fork = CartFork.FirstChunk; Assert.IsTrue(plan.HasFork(fork));
                    var shown = new[] { plan.DestinationFor(fork, -1), plan.DestinationFor(fork, 0), plan.DestinationFor(fork, 1) };
                    CollectionAssert.AreEquivalent(new[] { -1, 0, 1 }, shown, "Three different courses");
                    plan.UpdateChoice(CartFork.Decision + .5f, lane);
                    Assert.AreEqual(shown[lane + 1], plan.DestinationAt(CartFork.DestinationChunk), "The sign told the truth");
                    Assert.AreEqual(MapCourse.KindOf(shown[lane + 1]), MapCourse.KindAt(plan, CartFork.DestinationChunk + 1));
                    Assert.AreEqual(ExerciseKind.Warmup, MapCourse.KindAt(plan, 1));
                    int stairs = 0; for (int i = 0; i < RoutePlan.ExtendedLapChunks; i++) if (RouteSurface.IsStair(plan.TypeAt(i))) stairs++;
                    Assert.AreEqual(MapCourse.Stairs(1, shown[lane + 1]), stairs, "Stairs follow the course at the start of the run");
                }
        }

        [Test] public void WeeklyGoalCountsModerateMinutesFromMonday()
        {
            var log = new ExerciseLog();
            var wednesday = new DateTime(2026, 11, 25, 3, 0, 0, DateTimeKind.Utc); // 12:00 KST Wednesday
            log.Add("m", wednesday, 900, 600, new double[] { 0, 600, 0, 0 });
            log.Add("m", wednesday.AddDays(-2), 600, 1200, null); // Monday
            log.Add("m", wednesday.AddDays(-3), 600, 6000, null); // last Sunday: not this week
            log.Add("other", wednesday, 9000, 9000, null);
            Assert.AreEqual(30, log.WeekModerateMinutes("m", wednesday), 1e-9);
            Assert.AreEqual(.2, log.WeekGoalProgress("m", wednesday), 1e-9);
            Assert.AreEqual("주간 목표 30 / 150분", WorkoutReport.WeekGoal(30.9));
        }

        [Test] public void StreakNeedsTenMinutesADayAndPaysOnceADay()
        {
            var log = new ExerciseLog(); var day1 = new DateTime(2026, 11, 20, 1, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(0, log.Add("m", day1, 700, 0, null), "One day is not a streak yet");
            Assert.AreEqual(1, log.Streak("m", day1));
            Assert.AreEqual(0, log.Add("m", day1.AddDays(1), 300, 0, null), "Five minutes do not count");
            Assert.AreEqual(ExerciseLog.StreakCoins, log.Add("m", day1.AddDays(1), 400, 0, null), "Ten minutes on the second day");
            Assert.AreEqual(0, log.Add("m", day1.AddDays(1), 900, 0, null), "Paid once a day");
            Assert.AreEqual(2, log.Streak("m", day1.AddDays(1)));
            Assert.AreEqual(2, log.Streak("m", day1.AddDays(2)), "Today not done yet: the streak still counts yesterday");
            Assert.AreEqual(0, log.Streak("m", day1.AddDays(3)), "A missed day ends it");
            Assert.AreEqual("연속 2일 · 코인 +10", WorkoutReport.StreakLine(2, 10)); Assert.AreEqual("", WorkoutReport.StreakLine(0, 0));
        }

        [Test] public void TheLeastDoneCourseIsRecommended()
        {
            var log = new ExerciseLog(); var now = new DateTime(2026, 11, 25, 3, 0, 0, DateTimeKind.Utc);
            log.Add("", now, 100, 0, new double[] { 0, 300, 0, 200 });
            Assert.AreEqual(ExerciseKind.Cardio, log.Recommend("", now));
            log.Add("", now, 100, 0, new double[] { 0, 0, 900, 0 });
            Assert.AreEqual(ExerciseKind.Balance, log.Recommend("", now));
        }

        [Test] public void TodaysChallengeIsOneMapPerKstDayWithItsOwnBoard()
        {
            var a = new DateTime(2026, 11, 25, 14, 59, 0, DateTimeKind.Utc); // 23:59 KST
            Assert.AreEqual(DailyChallenge.Seed(a), DailyChallenge.Seed(a.AddHours(-10)), "The same KST day");
            Assert.AreNotEqual(DailyChallenge.Seed(a), DailyChallenge.Seed(a.AddMinutes(2)), "A new KST day, a new map");
            Assert.AreEqual("20261125", DailyChallenge.Day(a)); Assert.AreEqual(11, DailyChallenge.Mode(PlayStyle.UpperBody));
            var board = new Leaderboard(); long now = DateTime.Now.Ticks;
            var challenge = new RankingEntry { Name = "C", Score = 500, ScoreVersion = RunScore.Version, Mode = 0, Challenge = "20261125", Ticks = now };
            board.Add(new RankingEntry { Name = "N", Score = 100, ScoreVersion = RunScore.Version, Mode = 0, Ticks = now + 1 });
            Assert.AreEqual(1, board.Add(challenge));
            CollectionAssert.AreEqual(new[] { "N" }, board.View(false, DateTime.Now, PlayStyle.FullBody).ConvertAll(e => e.Name), "Challenge runs stay off the ordinary board");
            CollectionAssert.AreEqual(new[] { "C" }, board.ChallengeView("20261125", PlayStyle.FullBody).ConvertAll(e => e.Name));
            Assert.IsEmpty(board.ChallengeView("20261126", PlayStyle.FullBody));
        }
    }
}
