using System;
using System.Globalization;

namespace Muhanok.Domain
{
    // 2024 Adult Compendium code 22240: total-body active game, moderate effort (4 MET).
    // This estimates gross expenditure; camera recognition does not measure metabolism.
    public sealed class WorkoutEstimate
    {
        public const double AssumedMet=4;
        // An empty weight field uses this, so every camera run gets a calorie estimate.
        public const double DefaultWeightKg=65;
        // Accepted weight range; the account server and AccountSession use the same one.
        public const double MinWeightKg=10, MaxWeightKg=350;
        public static bool ValidWeight(double kg)=>!double.IsNaN(kg)&&!double.IsInfinity(kg)&&kg>=MinWeightKg&&kg<=MaxWeightKg;
        public double WeightKg {get; private set;}=DefaultWeightKg;
        public double ActiveSeconds {get; private set;}
        public double Met {get;set;}=AssumedMet;
        public double EstimatedKcal=>Kcal(Met,WeightKg,ActiveSeconds);
        public double GoalKcal {get; private set;}=50;
        private readonly double[] activitySeconds=new double[5];
        public bool GoalReached=>EstimatedKcal>=GoalKcal;
        public double GoalProgress=>Math.Min(1,EstimatedKcal/GoalKcal);
        public bool SetGoal(double kcal) { if(double.IsNaN(kcal)||double.IsInfinity(kcal)||kcal<1||kcal>5000)return false; GoalKcal=kcal;return true; }
        public bool SetGoal(string value) => double.TryParse(value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out double kcal) && SetGoal(kcal);
        public double SecondsFor(int activity)=>activitySeconds[Math.Max(0,Math.Min(4,activity))];
        public double KcalFor(int activity)=>Kcal(Met,WeightKg,SecondsFor(activity));
        public static double Kcal(double met,double weightKg,double seconds)=>Math.Max(0,met)*3.5*Math.Max(0,weightKg)/200*Math.Max(0,seconds)/60;
        // Allocate one time slice to one activity. All use the play-style MET assumption, not invented per-joint measurements.
        public void TickActivity(double seconds,bool playing,bool tracked,bool active,int activity)
        {
            double before=ActiveSeconds; Tick(seconds,playing,tracked&&active);
            activitySeconds[Math.Max(0,Math.Min(4,activity))]+=ActiveSeconds-before;
        }
        public bool SetWeight(string value)
        {
            if(string.IsNullOrWhiteSpace(value)){WeightKg=DefaultWeightKg; Reset(); return true;}
            if(!double.TryParse(value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out double kg)
                ||!ValidWeight(kg)) return false;
            WeightKg=kg; Reset(); return true;
        }
        public void Tick(double seconds,bool playing,bool poseConnected,MotionCommand recognizedMotion)
        {
            if(!playing||!poseConnected||recognizedMotion==MotionCommand.Center
                ||double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0) return;
            ActiveSeconds+=seconds;
        }
        // Camera styles count seconds in which the classifier saw real exercise (including running in place).
        public void Tick(double seconds,bool playing,bool active)
        {
            if(!playing||!active||double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0) return;
            ActiveSeconds+=seconds;
        }
        public void Reset(){ActiveSeconds=0;Array.Clear(activitySeconds,0,activitySeconds.Length);}
    }

    // Run score shared by the HUD, pause, result and ranking: round(distance m + score kcal × 100).
    // Score kcal always uses DefaultWeightKg so a heavier player does not score more for the same exercise.
    // Keyboard runs have no active seconds, so their score is the distance.
    public static class RunScore
    {
        public const int Version=2;
        public const double KcalPoints=100;
        public static double ScoreKcal(double met,double activeSeconds)=>WorkoutEstimate.Kcal(met,WorkoutEstimate.DefaultWeightKg,activeSeconds);
        public static int From(double distance,double met,double activeSeconds)
        {
            double metres=double.IsNaN(distance)||double.IsInfinity(distance)?0:Math.Max(0,distance);
            double kcal=ScoreKcal(met,activeSeconds);if(double.IsNaN(kcal)||double.IsInfinity(kcal))kcal=0;
            return (int)Math.Min(int.MaxValue,Math.Round(metres+kcal*KcalPoints,MidpointRounding.AwayFromZero));
        }
        public static int From(double distance,WorkoutEstimate workout)=>workout==null?From(distance,0,0):From(distance,workout.Met,workout.ActiveSeconds);
    }
}
