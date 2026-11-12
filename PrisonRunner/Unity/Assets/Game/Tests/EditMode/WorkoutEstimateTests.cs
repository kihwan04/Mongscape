using System;
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class WorkoutEstimateTests
    {
        [Test] public void EmptyWeightUsesTheDefaultWeight()
        {
            var w=new WorkoutEstimate();Assert.IsTrue(w.SetWeight("70"));Assert.IsTrue(w.SetWeight(""));
            Assert.AreEqual(WorkoutEstimate.DefaultWeightKg,w.WeightKg);Assert.AreEqual(65,w.WeightKg);
            w.Tick(60,true,true,MotionCommand.KneeStep);Assert.Greater(w.EstimatedKcal,0);Assert.AreEqual(4.55,w.EstimatedKcal,.0001);
        }
        [TestCase("NaN")][TestCase("Infinity")][TestCase("0")][TestCase("500")][TestCase("70kg")]
        public void InvalidWeightIsRejected(string input){var w=new WorkoutEstimate();Assert.IsFalse(w.SetWeight(input));Assert.AreEqual(WorkoutEstimate.DefaultWeightKg,w.WeightKg);}
        [Test] public void WeightAndActualRecognizedTimeScaleEstimate()
        {
            var w=new WorkoutEstimate(); Assert.IsTrue(w.SetWeight("70"));
            w.Tick(60,true,true,MotionCommand.KneeStep); Assert.AreEqual(4.9,w.EstimatedKcal,.0001);
            w.Tick(60,true,true,MotionCommand.Jump); Assert.AreEqual(9.8,w.EstimatedKcal,.0001);
        }
        [Test] public void LostPoseIdleKeyboardAndPauseAreExcluded()
        {
            var w=new WorkoutEstimate();w.SetWeight("70");
            w.Tick(60,true,false,MotionCommand.Jump);w.Tick(60,false,true,MotionCommand.Jump);
            w.Tick(60,true,true,MotionCommand.Center);w.Tick(double.NaN,true,true,MotionCommand.Jump);
            Assert.AreEqual(0,w.ActiveSeconds);Assert.AreEqual(0,w.EstimatedKcal);
        }
        [Test] public void RetryClearsEstimateButKeepsEnteredWeight()
        {
            var run=new RunSession(new RunConfig());run.Workout.SetWeight("70");run.Workout.Tick(60,true,true,MotionCommand.Left);
            run.Reset();Assert.AreEqual(70,run.Workout.WeightKg);Assert.AreEqual(0,run.Workout.EstimatedKcal);
            run.Workout.SetWeight("");Assert.AreEqual(WorkoutEstimate.DefaultWeightKg,run.Workout.WeightKg);
        }
        [Test] public void ScoreIsDistancePlusFixedWeightKcalTimesHundred()
        {
            var w=new WorkoutEstimate{Met=PlayStyleInfo.Met(PlayStyle.FullBody)};w.Tick(180,true,true);
            Assert.AreEqual(13.65,RunScore.ScoreKcal(w.Met,w.ActiveSeconds),.0001);
            Assert.AreEqual(2605,RunScore.From(1240,w),"1,240 m + 13.65 kcal x 100");
            Assert.AreEqual((int)Math.Round(100.4+RunScore.ScoreKcal(4,30)*100,MidpointRounding.AwayFromZero),RunScore.From(100.4,4,30));
            Assert.AreEqual(100,RunScore.From(99.5,0,0));Assert.AreEqual(0,RunScore.From(-5,4,0));Assert.AreEqual(12,RunScore.From(12,null));
        }
        [Test] public void EnteredWeightChangesCaloriesButNotScore()
        {
            var light=new WorkoutEstimate();light.SetWeight("50");var heavy=new WorkoutEstimate();heavy.SetWeight("120");var empty=new WorkoutEstimate();empty.SetWeight("");
            foreach(var w in new[]{light,heavy,empty})w.Tick(120,true,true);
            Assert.Less(light.EstimatedKcal,heavy.EstimatedKcal);
            Assert.AreEqual(RunScore.From(500,empty),RunScore.From(500,light));Assert.AreEqual(RunScore.From(500,empty),RunScore.From(500,heavy));
        }
        [Test] public void MoreExerciseScoresMoreAndKeyboardScoreIsDistance()
        {
            var little=new WorkoutEstimate();little.Tick(30,true,true);var lots=new WorkoutEstimate();lots.Tick(90,true,true);
            Assert.Greater(RunScore.From(800,lots),RunScore.From(800,little));Assert.Greater(RunScore.From(800,little),800);
            var run=new RunSession(new RunConfig());run.Workout.Met=PlayStyleInfo.Met(PlayStyle.Keyboard);run.Score.Advance(321.4f);
            Assert.AreEqual(0,run.Workout.ActiveSeconds);Assert.AreEqual(321,run.Score.Score,"Keyboard has no active seconds, so score = distance");
            run.Workout.Tick(60,true,true);Assert.AreEqual(RunScore.From(run.Score.Distance,run.Workout),run.Score.Score,"The live score uses the same formula");
        }
        [Test] public void RequestedFourAreasRunInOrderAcrossCycles()
        {
            var expected=new[]{0,1,2,3,7,4,5,6,11,12};
            for(int lap=0;lap<3;lap++)for(int i=0;i<expected.Length;i++)Assert.AreEqual(expected[i],RouteSurface.TypeAt(lap*10+i));
        }
    }
}
