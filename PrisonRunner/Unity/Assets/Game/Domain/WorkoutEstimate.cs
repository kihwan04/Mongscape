using System;
using System.Globalization;

namespace Muhanok.Domain
{
    // Moments of a run, counted for the result report: steps, jumps, crouches, knee steps (stairs) and lane changes.
    public enum MoveKind { Step, Jump, Crouch, KneeStep, SideStep }

    // MET of the moment from the cadence (steps a minute over the last 3 s), not one figure for the whole run.
    // 2024 Adult Compendium: walking in place about 3.5, a moderate active game 5, running in place about 8 (kept at 7).
    // A big movement (deep squat, high jump, hips-high knee, wide side step) adds 1. The upper body uses 0.6 of each.
    // A moment counts toward the weekly moderate minutes when its MET after the play-style scale is Moderate or more
    // (QA 2026-11-25 M5: the upper body's fast arm swings, 7 x 0.6 = 4.2, are not moderate exercise).
    public static class IntensityMet
    {
        public const double Walk = 3.5, Run = 5, Sprint = 7, BigMove = 1, UpperScale = .6;
        // "달리기 속도 조절" off: stepping counts nothing; a jump, crouch, knee raise or lane change moment counts at this MET
        // (a moderate active game, +1 when big), so a full body run without the cadence still fills the weekly goal (QA H1).
        public const double Move = 5;
        // MET (after the play-style scale) from which a moment counts toward the weekly moderate minutes.
        public const double Moderate = 5;
        public static bool IsModerate(double met) => met >= Moderate - 1e-9;
        public static double Base(CadenceStage stage) => stage == CadenceStage.Sprint ? Sprint : stage == CadenceStage.Run ? Run : stage == CadenceStage.Walk ? Walk : 0;
        // Keyboard play has no measured exercise.
        public static double Scale(PlayStyle style) => style == PlayStyle.FullBody ? 1 : style == PlayStyle.UpperBody ? UpperScale : 0;
        public static double Of(CadenceStage stage, bool big, double scale) => stage == CadenceStage.Standing ? 0 : (Base(stage) + (big ? BigMove : 0)) * Math.Max(0, scale);
    }

    // Calories: MET x 3.5 x weight / 200 per minute, summed over the moments the camera saw exercise.
    // This estimates gross expenditure; camera recognition does not measure metabolism.
    public sealed class WorkoutEstimate
    {
        // The flat MET used by the plain Tick overloads (tools and older tests). Runs tick by intensity instead.
        public const double AssumedMet=4;
        // An empty weight field uses this, so every camera run gets a calorie estimate.
        public const double DefaultWeightKg=65;
        // Accepted weight range; the account server and AccountSession use the same one.
        public const double MinWeightKg=10, MaxWeightKg=350;
        public static bool ValidWeight(double kg)=>!double.IsNaN(kg)&&!double.IsInfinity(kg)&&kg>=MinWeightKg&&kg<=MaxWeightKg;
        public double WeightKg {get; private set;}=DefaultWeightKg;
        public double ActiveSeconds {get; private set;}
        // MET x seconds over the run: calories and the score both come from it.
        public double MetSeconds {get; private set;}
        public double Met {get;set;}=AssumedMet;
        // Play-style share of the intensity MET (1 full body, 0.6 upper body, 0 keyboard).
        public double StyleScale {get;set;}=1;
        public double EstimatedKcal=>KcalOf(MetSeconds,WeightKg);
        // Average MET over the active time (0 before any).
        public double AverageMet=>ActiveSeconds>0?MetSeconds/ActiveSeconds:0;
        public double GoalKcal {get; private set;}=50;
        private readonly double[] activitySeconds=new double[5], activityMetSeconds=new double[5];
        // Active seconds by cadence stage (index = CadenceStage; Standing stays 0) and those at moderate intensity or more.
        private readonly double[] stageSeconds=new double[4];
        public double ModerateSeconds {get; private set;}
        // False when "달리기 속도 조절" is off: walk, run and sprint MET are 0 and only the moments of a jump, crouch,
        // knee raise or lane change count (MoveSeconds), so the result report shows that time instead of the stages.
        public bool CadenceCounts {get;set;}=true;
        public double MoveSeconds {get; private set;}
        // Seconds in which the movement was big (stage 4 feeds it; the +1 MET).
        public double BigSeconds {get; private set;}
        private readonly int[] moves=new int[5];
        public bool GoalReached=>EstimatedKcal>=GoalKcal;
        public double GoalProgress=>Math.Min(1,EstimatedKcal/GoalKcal);
        public bool SetGoal(double kcal) { if(double.IsNaN(kcal)||double.IsInfinity(kcal)||kcal<1||kcal>5000)return false; GoalKcal=kcal;return true; }
        public bool SetGoal(string value) => double.TryParse(value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out double kcal) && SetGoal(kcal);
        public double SecondsFor(int activity)=>activitySeconds[Math.Max(0,Math.Min(4,activity))];
        public double KcalFor(int activity)=>KcalOf(activityMetSeconds[Math.Max(0,Math.Min(4,activity))],WeightKg);
        public double StageSeconds(CadenceStage stage)=>stageSeconds[Math.Max(0,Math.Min(3,(int)stage))];
        public int Moves(MoveKind kind)=>moves[Math.Max(0,Math.Min(4,(int)kind))];
        public void CountMove(MoveKind kind,int count=1){ if(count>0) moves[Math.Max(0,Math.Min(4,(int)kind))]+=count; }
        public static double Kcal(double met,double weightKg,double seconds)=>Math.Max(0,met)*3.5*Math.Max(0,weightKg)/200*Math.Max(0,seconds)/60;
        public static double KcalOf(double metSeconds,double weightKg)=>Kcal(1,weightKg,metSeconds);
        // Allocate one time slice to one activity at the flat MET (older callers and tools).
        public void TickActivity(double seconds,bool playing,bool tracked,bool active,int activity)
        {
            if(!playing||!tracked||!active||!Valid(seconds)) return;
            Add(seconds,Met,true,(int)CadenceStage.Run,false,activity);
        }
        // One camera frame of a run. stage comes from the cadence; moving says another motion (jump, crouch, knee, lane)
        // happened just now, which counts as walking when the feet are still. Standing still or out of view counts nothing
        // (no grace period). big: the movement was big enough for the +1 MET.
        public void TickIntensity(double seconds,bool playing,bool tracked,CadenceStage stage,bool moving,bool big,int activity)
        {
            if(!playing||!tracked||!Valid(seconds)) return;
            if(!CadenceCounts)
            {
                // Cadence off: no MET for stepping in place, only for the motion moments.
                if(!moving) return;
                double moveMet=(IntensityMet.Move+(big?IntensityMet.BigMove:0))*Math.Max(0,StyleScale);
                Add(seconds,moveMet,IntensityMet.IsModerate(moveMet),-1,big,activity);
                return;
            }
            if(stage==CadenceStage.Standing) { if(!moving) return; stage=CadenceStage.Walk; }
            double met=IntensityMet.Of(stage,big,StyleScale);
            Add(seconds,met,IntensityMet.IsModerate(met),(int)stage,big,activity);
        }
        // stage -1: a motion moment of a run without cadence (MoveSeconds).
        // moderate: the moment counts toward the weekly moderate minutes (the flat-MET ticks of tools and tests always do).
        private void Add(double seconds,double met,bool moderate,int stage,bool big,int activity)
        {
            int a=Math.Max(0,Math.Min(4,activity));
            ActiveSeconds+=seconds; MetSeconds+=met*seconds;
            activitySeconds[a]+=seconds; activityMetSeconds[a]+=met*seconds;
            if(stage>=0) stageSeconds[stage]+=seconds; else MoveSeconds+=seconds;
            if(moderate) ModerateSeconds+=seconds;
            if(big) BigSeconds+=seconds;
        }
        private static bool Valid(double seconds)=>!double.IsNaN(seconds)&&!double.IsInfinity(seconds)&&seconds>0;
        public bool SetWeight(string value)
        {
            if(string.IsNullOrWhiteSpace(value)){WeightKg=DefaultWeightKg; Reset(); return true;}
            if(!double.TryParse(value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out double kg)
                ||!ValidWeight(kg)) return false;
            WeightKg=kg; Reset(); return true;
        }
        public void Tick(double seconds,bool playing,bool poseConnected,MotionCommand recognizedMotion)
        {
            if(!playing||!poseConnected||recognizedMotion==MotionCommand.Center||!Valid(seconds)) return;
            Add(seconds,Met,true,(int)CadenceStage.Run,false,0);
        }
        // Flat-MET seconds (tools and tests).
        public void Tick(double seconds,bool playing,bool active)
        {
            if(!playing||!active||!Valid(seconds)) return;
            Add(seconds,Met,true,(int)CadenceStage.Run,false,0);
        }
        public void Reset()
        {
            ActiveSeconds=MetSeconds=ModerateSeconds=BigSeconds=MoveSeconds=0;
            Array.Clear(activitySeconds,0,activitySeconds.Length); Array.Clear(activityMetSeconds,0,activityMetSeconds.Length);
            Array.Clear(stageSeconds,0,stageSeconds.Length); Array.Clear(moves,0,moves.Length);
        }
    }

    // Run score shared by the HUD, pause, result and ranking: round(distance m + score kcal × 100).
    // Score kcal always uses DefaultWeightKg so a heavier player does not score more for the same exercise.
    // Keyboard runs have no active seconds, so their score is the distance.
    // Version 3: the kcal follow the intensity MET of each moment (version 2 used a flat MET per play style).
    // Ranked records of an older version are kept but not ranked (a new ranking season).
    public static class RunScore
    {
        public const int Version=3, PreviousVersion=2;
        public const double KcalPoints=100;
        public static double ScoreKcal(double met,double activeSeconds)=>WorkoutEstimate.Kcal(met,WorkoutEstimate.DefaultWeightKg,activeSeconds);
        public static int From(double distance,double met,double activeSeconds)=>FromMetSeconds(distance,Math.Max(0,met)*Math.Max(0,activeSeconds));
        public static int FromMetSeconds(double distance,double metSeconds)
        {
            double metres=double.IsNaN(distance)||double.IsInfinity(distance)?0:Math.Max(0,distance);
            double kcal=WorkoutEstimate.KcalOf(metSeconds,WorkoutEstimate.DefaultWeightKg);if(double.IsNaN(kcal)||double.IsInfinity(kcal))kcal=0;
            return (int)Math.Min(int.MaxValue,Math.Round(metres+kcal*KcalPoints,MidpointRounding.AwayFromZero));
        }
        public static int From(double distance,WorkoutEstimate workout)=>workout==null?From(distance,0,0):FromMetSeconds(distance,workout.MetSeconds);
    }
}
