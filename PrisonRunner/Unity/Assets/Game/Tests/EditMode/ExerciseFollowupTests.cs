using System;
using Muhanok.Application;
using Muhanok.Domain;
using Muhanok.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Muhanok.Tests
{
    // Exercise follow-up (owner decisions 2026-11-25): weekly minutes on the account server, one ranking for runs with and
    // without the cadence option, and no stepping calories when the option is off.
    public sealed class ExerciseFollowupTests
    {
        [Test] public void CadenceOffCountsOnlyMotionMoments()
        {
            var w = new WorkoutEstimate { CadenceCounts = false };
            w.TickIntensity(60, true, true, CadenceStage.Sprint, false, false, 0);
            w.TickIntensity(60, true, true, CadenceStage.Run, false, true, 0);
            Assert.AreEqual(0, w.ActiveSeconds, "Walking, running and sprinting in place are 0 MET with the option off");
            Assert.AreEqual(0, w.EstimatedKcal);
            w.TickIntensity(10, true, true, CadenceStage.Sprint, true, false, 1);
            Assert.AreEqual(10, w.ActiveSeconds); Assert.AreEqual(10, w.MoveSeconds);
            Assert.AreEqual(IntensityMet.Move * 10, w.MetSeconds, 1e-9, "A jump moment counts at the motion MET, not the sprint MET");
            Assert.AreEqual(0, w.StageSeconds(CadenceStage.Sprint));
            Assert.AreEqual(10, w.ModerateSeconds, "A motion moment of a full body run counts toward the weekly goal (QA H1)");
            w.TickIntensity(4, true, true, CadenceStage.Standing, true, true, 2);
            Assert.AreEqual(IntensityMet.Move * 10 + (IntensityMet.Move + IntensityMet.BigMove) * 4, w.MetSeconds, 1e-9, "A deep crouch adds 1");
            w.TickIntensity(5, true, false, CadenceStage.Run, true, false, 1);
            Assert.AreEqual(14, w.ActiveSeconds, "Out of view still counts nothing");
            var upper = new WorkoutEstimate { CadenceCounts = false, StyleScale = IntensityMet.Scale(PlayStyle.UpperBody) };
            upper.TickIntensity(10, true, true, CadenceStage.Run, true, false, 1);
            Assert.AreEqual(IntensityMet.Move * .6 * 10, upper.MetSeconds, 1e-9);
            Assert.AreEqual(0, upper.ModerateSeconds, "The upper body's moments are 3 MET, under moderate (QA M5)");
            w.Reset(); Assert.AreEqual(0, w.MoveSeconds); Assert.IsFalse(w.CadenceCounts, "Reset keeps the option");
        }

        [Test] public void CadenceOnStillCountsTheStages()
        {
            var w = new WorkoutEstimate();
            Assert.IsTrue(w.CadenceCounts);
            w.TickIntensity(60, true, true, CadenceStage.Run, false, false, 0);
            Assert.AreEqual(60, w.StageSeconds(CadenceStage.Run)); Assert.AreEqual(60, w.ModerateSeconds); Assert.AreEqual(0, w.MoveSeconds);
        }

        [Test] public void ReportShowsTheMotionTimeWhenCadenceIsOff()
        {
            var off = new WorkoutEstimate { CadenceCounts = false };
            off.TickIntensity(95, true, true, CadenceStage.Run, true, false, 1);
            var bars = WorkoutReport.Intensity(off);
            Assert.AreEqual(1, bars.Count); Assert.AreEqual(WorkoutReport.MoveRowLabel, bars[0].label); Assert.AreEqual("1:35", WorkoutReport.Clock(bars[0].seconds));
            Assert.AreEqual("동작한 시간", WorkoutReport.IntensityTitle(off)); Assert.IsTrue(WorkoutReport.CadenceOff(off));
            var on = new WorkoutEstimate();
            Assert.AreEqual(3, WorkoutReport.Intensity(on).Count); Assert.AreEqual("강도별 시간", WorkoutReport.IntensityTitle(on));
            Assert.IsFalse(WorkoutReport.CadenceOff(null));
        }

        [Test] public void RunUploadCarriesModerateSecondsAndNoCadenceFlag()
        {
            var session = new RunSession(new RunConfig());
            session.Workout.TickIntensity(120, true, true, CadenceStage.Run, false, false, 0);
            session.Workout.TickIntensity(30, true, true, CadenceStage.Walk, false, false, 0);
            var run = AccountRun.Create(session, PlayStyle.FullBody, 0);
            Assert.AreEqual(120, run.moderate_seconds, 1e-9);
            string json = JsonUtility.ToJson(run);
            StringAssert.Contains("\"moderate_seconds\":120", json);
            StringAssert.DoesNotContain("cadence", json, "Runs with and without the option share one ranking");
        }

        [Test] public void StatsWithoutTheFieldKeepTheLocalWeek()
        {
            var old = JsonUtility.FromJson<StatsReply>("{\"ok\":true,\"week_kcal\":3}");
            Assert.AreEqual(-1, old.week_moderate_seconds, "An older server sends no weekly minutes");
            var current = JsonUtility.FromJson<StatsReply>("{\"ok\":true,\"week_moderate_seconds\":1800}");
            Assert.AreEqual(1800, current.week_moderate_seconds);
        }

        [Test] public void MemberWeekFollowsTheServerAndGuestsStayLocal()
        {
            var log = new ExerciseLog(); var now = new DateTime(2026, 11, 25, 3, 0, 0, DateTimeKind.Utc);
            log.Add("m", now, 900, 600, null); log.Add("", now, 900, 300, null);
            Assert.AreEqual(10, log.WeekModerateMinutes("m", now), 1e-9, "Before /stats answers: this PC's log");
            log.SyncWeek("m", now, -1); Assert.IsFalse(log.HasServerWeek("m", now), "An older server's -1 is ignored");
            log.SyncWeek("m", now, 3600); // 60 minutes over every PC of the member
            Assert.AreEqual(60, log.WeekModerateMinutes("m", now), 1e-9);
            log.Add("m", now, 300, 120, null);
            Assert.AreEqual(62, log.WeekModerateMinutes("m", now), 1e-9, "A run saved since adds to the server figure until the next /stats");
            log.SyncWeek("m", now, 3720); Assert.AreEqual(62, log.WeekModerateMinutes("m", now), 1e-9);
            log.SyncWeek("", now, 99999); Assert.AreEqual(5, log.WeekModerateMinutes("", now), 1e-9, "Guests stay on this PC");
            var nextWeek = now.AddDays(6);
            Assert.IsFalse(log.HasServerWeek("m", nextWeek)); Assert.AreEqual(0, log.WeekModerateMinutes("m", nextWeek), 1e-9, "Last week's server figure is not this week's");
        }

        [Test] public void RankingRowsCarryNoCadenceTag()
        {
            Assert.IsNull(typeof(RankingRow).GetField("CadenceSpeed")); Assert.IsNull(typeof(RankingRow).GetProperty("CadenceTag"));
            Assert.IsNull(typeof(RankingEntry).GetField("CadenceSpeed")); Assert.IsNull(typeof(AccountRun).GetField("cadence_speed"));
        }

        static float Apex(RunSession run, bool big)
        {
            run.Runner.Command(MotionCommand.Jump); run.Tick(.05f);
            if (big) run.Big(RunSession.BigMove.Jump);
            float apex = 0; for (int i = 0; i < 80; i++) { run.Tick(.02f); apex = Math.Max(apex, run.Runner.Y); }
            return apex;
        }
        static RunSession Running()
        {
            var run = new RunSession(new RunConfig()); run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            return run;
        }

        [Test] public void AirCoinsNeedABigJump()
        {
            var config = new RunConfig(); float height = BonusCoins.AirHeight(config);
            Assert.IsFalse(BonusCoins.HeightReach(Apex(Running(), false), height), "A normal jump stays under the air coins");
            Assert.IsTrue(BonusCoins.HeightReach(Apex(Running(), true), height), "A big jump reaches them");
            Assert.IsFalse(BonusCoins.HeightReach(0, height));
            Assert.AreEqual(BonusCoins.AirCount, 3); Assert.AreEqual(0, BonusCoins.AirZ(1), 1e-6, "Centred on the jump row");
        }

        [Test] public void TheFarHalfOfTheBarLineNeedsADeepSlide()
        {
            Assert.IsFalse(BonusCoins.Reachable(BonusCoin.UnderBar, false, false), "Standing never takes a coin under the bars");
            Assert.IsTrue(BonusCoins.Reachable(BonusCoin.UnderBar, true, false));
            Assert.IsFalse(BonusCoins.Reachable(BonusCoin.UnderBarDeep, true, false), "A plain slide stops halfway along the line");
            Assert.IsTrue(BonusCoins.Reachable(BonusCoin.UnderBarDeep, true, true));
            Assert.IsTrue(BonusCoins.Reachable(BonusCoin.Air, false, false)); Assert.IsTrue(BonusCoins.Reachable(BonusCoin.None, false, false));
            int deep = 0; for (int i = 0; i < BonusCoins.CrouchCount; i++) if (BonusCoins.CrouchKind(i) == BonusCoin.UnderBarDeep) { deep++; Assert.Greater(BonusCoins.CrouchZ(i), 0, "Deep coins lie past the bars"); }
            Assert.AreEqual(BonusCoins.CrouchDeep, deep);
            Assert.IsTrue(BonusCoins.HeightReach(0, BonusCoins.CrouchHeight), "Low enough for a sliding runner");
            var run = Running(); run.Runner.Command(MotionCommand.Crouch); run.Big(RunSession.BigMove.Crouch);
            Assert.IsTrue(BonusCoins.Reachable(BonusCoin.UnderBarDeep, run.Runner.Crouching, run.Runner.DeepSliding));
        }
    }
}
