using System;
using Muhanok.Domain;
using NUnit.Framework;
namespace Muhanok.Tests
{
    public sealed class ExpansionRulesTests
    {
        [TestCase(-1)] [TestCase(0)] [TestCase(1)]
        public void PhysicalForkStaysOnSelectedPathAcrossTheNextLap(int branch)
        {
            var plan=new RoutePlan(new RunConfig(),1);
            plan.UpdateChoice(CartFork.Decision,branch);
            Assert.AreEqual(0,plan.SelectedForkOffset(CartFork.Start));
            Assert.AreEqual(branch*2.2f,plan.SelectedForkOffset(CartFork.Decision));
            Assert.AreEqual(CartFork.Offset(132,branch),plan.SelectedForkOffset(132));
            Assert.AreEqual(branch*CartFork.Spacing,plan.SelectedForkOffset(192));
            Assert.AreEqual(branch*CartFork.Spacing,plan.SelectedForkOffset(216));
            Assert.AreEqual(branch*CartFork.Spacing,plan.SelectedForkOffset(RoutePlan.ExtendedLapChunks*RouteSurface.Length));
            try
            {
                RouteSurface.UsePlan(plan);
                for(int i=120;i<=1200;i+=24)
                {
                    RouteSurface.Sample(i-.001f,out float x0,out _);RouteSurface.Sample(i+.001f,out float x1,out _);
                    Assert.AreEqual(x0,x1,.0001f,"Track boundary discontinuity at "+i);
                }
            }
            finally { RouteSurface.UsePlan(null); }
            Assert.IsTrue(plan.IsForkTrackChunk(5));Assert.IsTrue(plan.IsForkTrackChunk(4));Assert.IsFalse(plan.IsForkTrackChunk(6));
        }
        [TestCase(-1)] [TestCase(0)] [TestCase(1)]
        public void HiddenChoiceChangesUpcomingMapWithoutBreakingHeight(int lane)
        {
            var p=new RoutePlan(new RunConfig(),7);
            Assert.IsTrue(p.ForkWindow(110)); Assert.IsFalse(p.ForkLocked(110));
            Assert.AreEqual(int.MinValue,p.DestinationAt(6));
            p.UpdateChoice(CartFork.Decision-.01f,lane); Assert.IsFalse(p.ForkLocked(CartFork.Decision-.01f));
            p.UpdateChoice(CartFork.Decision,lane); Assert.IsTrue(p.ForkLocked(CartFork.Decision));
            Assert.AreEqual(lane,p.ChoiceAt(6));
            int count=0;for(int i=CartFork.DestinationChunk;i<RoutePlan.ExtendedLapChunks;i++)if(p.TypeAt(i)==4||p.TypeAt(i)==5)count++;
            int destination=p.DestinationAt(6);
            Assert.AreEqual(destination==0?14:0,count);
            Assert.AreEqual(destination<0?12:destination>0?0:7,p.TypeAt(CartFork.DestinationChunk),"The three destinations must enter three actual biomes");
            for(int i=0;i<2*RoutePlan.ExtendedLapChunks-1;i++)Assert.AreEqual(p.BaseHeight(i)+RouteSurface.Rise(p.TypeAt(i)),p.BaseHeight(i+1),.001f);
            Assert.AreEqual(0,p.CycleAt(RoutePlan.ExtendedLapChunks-1)); Assert.AreEqual(1,p.CycleAt(RoutePlan.ExtendedLapChunks));
            p.UpdateChoice(150,-lane);Assert.AreEqual(lane,p.ChoiceAt(8),"Commitment must be stable");
        }
        [Test] public void EveryExtendedBranchLapIsThirtyTwoChunksWithTheFixedPrisonLayout()
        {
            Assert.AreEqual(32,RoutePlan.ExtendedLapChunks);
            for(int lane=-1;lane<=1;lane++)
            {
                var p=new RoutePlan(new RunConfig(),3);p.UpdateChoice(CartFork.Decision,lane);
                int destination=p.DestinationAt(6);
                Assert.AreEqual(0,p.CycleAt(31));Assert.AreEqual(1,p.CycleAt(32));
                if(destination==0)
                {
                    var expected=new System.Collections.Generic.List<int>{0,1,2,1,2,1,3,7};
                    for(int i=0;i<14;i++)expected.Add(4+i%2);
                    expected.Add(6);expected.Add(11);for(int i=0;i<7;i++)expected.Add(12);expected.Add(3);
                    Assert.AreEqual(32,expected.Count);
                    for(int i=0;i<32;i++)Assert.AreEqual(expected[i],p.TypeAt(i),"prison chunk "+i);
                }
                else
                {
                    Assert.AreEqual(destination<0?3:0,p.TypeAt(31));
                    // The first lap has one stair chunk (mine 13, zoo 14) in place of a walking chunk; everything else keeps the biome's walk.
                    int stairs=0;
                    for(int i=CartFork.DestinationChunk;i<31;i++)
                    {
                        if(RouteSurface.IsStair(p.TypeAt(i))){stairs++;Assert.AreEqual(destination<0?RouteSurface.ZooStair:RouteSurface.MineStair,p.TypeAt(i));continue;}
                        Assert.AreEqual(destination<0?12:0,p.TypeAt(i));
                    }
                    Assert.AreEqual(1,stairs);
                }
            }
        }
        [Test] public void ForkChunksStayInsideTheShortenedLap()
        {
            Assert.AreEqual(4,CartFork.FirstChunk);Assert.AreEqual(5,CartFork.LastChunk);Assert.AreEqual(6,CartFork.ConnectorChunk);Assert.AreEqual(7,CartFork.DestinationChunk);
        }
        [Test] public void SecondLapCanChooseIndependently()
        {
            var p=new RoutePlan(new RunConfig(),1);p.UpdateChoice(144,-1);p.UpdateChoice(RoutePlan.ExtendedLapChunks*RouteSurface.Length+144,1);
            Assert.AreEqual(-1,p.ChoiceAt(10));Assert.AreEqual(1,p.ChoiceAt(RoutePlan.ExtendedLapChunks+10));
        }
        [TestCase(-1)] [TestCase(0)] [TestCase(1)]
        public void ChosenRailRejectsEverySideCommandAndDisembarksWithoutLateralJump(int branch)
        {
            var config=new RunConfig();var plan=new RoutePlan(config,17);plan.UpdateChoice(CartFork.Decision,branch);
            RouteSurface.UsePlan(plan);
            try
            {
                var runner=new RunnerController(config);runner.SetStartZ(CartFork.Decision+6);runner.Tick(.01f,0);
                foreach(var command in new[]{MotionCommand.Left,MotionCommand.Center,MotionCommand.Right,MotionCommand.Crouch,MotionCommand.Jump})runner.Command(command);
                runner.Tick(.2f,0);Assert.AreEqual(branch,runner.Lane);Assert.AreEqual(0,runner.CartLean);Assert.AreEqual(0,runner.WorldLaneOffset);
                runner.SetStartZ(CartFork.End-.1f);runner.Tick(.1f,5);Assert.IsFalse(runner.Riding);Assert.AreEqual(0,runner.X);Assert.AreEqual(0,runner.WorldLaneOffset);
                runner.Command(MotionCommand.Left);runner.Tick(.25f,0);Assert.AreEqual(-2.2f,runner.X,.001f,"Walking controls must resume after the chosen rail");
            }
            finally{RouteSurface.UsePlan(null);}
        }
        [Test] public void DestinationsAreShuffledAndEveryDirectionCanDrawEveryMap()
        {
            for(int lane=-1;lane<=1;lane++)
            {
                var seen=new System.Collections.Generic.HashSet<int>();
                for(int seed=1;seed<=40;seed++)
                {
                    var a=new RoutePlan(new RunConfig(),seed);var b=new RoutePlan(new RunConfig(),seed);
                    a.UpdateChoice(144,lane);b.UpdateChoice(144,lane);
                    Assert.AreEqual(a.DestinationAt(6),b.DestinationAt(6));seen.Add(a.DestinationAt(6));
                }
                CollectionAssert.AreEquivalent(new[]{-1,0,1},seen);
            }
            for(int seed=0;seed<20;seed++)
            {
                var outcomes=new System.Collections.Generic.HashSet<int>();
                for(int lane=-1;lane<=1;lane++){var p=new RoutePlan(new RunConfig(),seed);p.UpdateChoice(144,lane);outcomes.Add(p.DestinationAt(6));}
                Assert.AreEqual(3,outcomes.Count,"Three doors must have distinct hidden destinations");
            }
        }
        [Test] public void CompactForkHasNoStraightWaitAfterTheSplit()
        {
            Assert.AreEqual(48,CartFork.End-CartFork.Start);
            Assert.AreEqual(CartFork.SplitEnd,CartFork.End);
            Assert.AreEqual(CartFork.End,CartFork.ConnectorChunk*RouteSurface.Length);
            Assert.AreEqual(24,CartFork.End-CartFork.Decision);
            Assert.AreEqual(168,CartFork.DestinationChunk*RouteSurface.Length);
        }
        [Test] public void ForkCurvesJoinWithGentleVelocityAndAcceleration()
        {
            const float step=.25f;
            foreach(float join in new[]{CartFork.Start,CartFork.Decision,CartFork.End})
            {
                float mid=CartFork.Offset(join,1),left=CartFork.Offset(join-step,1),right=CartFork.Offset(join+step,1);
                Assert.Less(System.Math.Abs((right-left)/(2*step)),.005f,"Lateral velocity at "+join);
                float a=CartFork.Offset(join+2*step,1)-2*right+mid;
                float b=mid-2*left+CartFork.Offset(join-2*step,1);
                Assert.Less(System.Math.Abs(a/(step*step)),.02f,"Entry acceleration at "+join);
                Assert.Less(System.Math.Abs(b/(step*step)),.02f,"Exit acceleration at "+join);
            }
            float last=0;
            for(float z=CartFork.Start;z<=CartFork.End;z+=.25f){float x=CartFork.Offset(z,1);Assert.GreaterOrEqual(x+.00002f,last);last=x;}
        }
        [Test] public void ManualCalorieGoalValidatesAndNeverCapsExercise()
        {
            var w=new WorkoutEstimate();Assert.IsTrue(w.SetWeight("70"));Assert.IsTrue(w.SetGoal("1.5"));
            foreach(string bad in new[]{"","0","-5","NaN","Infinity","5001","abc"})Assert.IsFalse(w.SetGoal(bad));
            w.TickActivity(60,true,true,true,0);Assert.IsTrue(w.GoalReached);double before=w.EstimatedKcal;
            w.TickActivity(60,true,true,true,1);Assert.Greater(w.EstimatedKcal,before);Assert.AreEqual(120,w.ActiveSeconds);
            Assert.AreEqual(1.5,w.GoalKcal);Assert.AreEqual(1,w.GoalProgress);
        }
        [Test] public void ExtendedChallengeBuildsWithinALapAndKeepsRestAndPauseRules()
        {
            var config=new RunConfig{IntervalMode=true,ExtendedRoutes=true,SprintSpeed=6,MaxSprintSpeed=12,RestSpeed=5,MaxRestSpeed=7};
            var plan=new RoutePlan(config,1);RouteSurface.UsePlan(plan);
            try
            {
                var fresh=new RunSession(config);fresh.Runner.SetStartZ(30);Assert.AreEqual(5,fresh.TargetSpeed,.001f,"The cart starts at its slowest rest speed");
                var lap=new RunSession(config);lap.Score.Advance(500);float edge=RoutePlan.ExtendedLapChunks*RouteSurface.Length;
                lap.Runner.SetStartZ(edge-1);float lapEnd=lap.TargetSpeed;lap.Runner.SetStartZ(edge+1);
                Assert.AreEqual(lapEnd,lap.TargetSpeed,.01f,"No speed step where a new lap starts");
                var run=new RunSession(config);Assert.AreEqual(6,run.TargetSpeed,.001f);
                run.Score.Advance(1075);Assert.AreEqual(9f,run.TargetSpeed,.001f);
                run.Score.Advance(10000);Assert.AreEqual(12,run.TargetSpeed,.001f);
                run.Runner.SetStartZ(30);Assert.AreEqual(7,run.TargetSpeed,.001f,"The cart rest speed ramps with the run");
                run.Paused=true;float before=run.Score.Distance;run.Tick(10);Assert.AreEqual(before,run.Score.Distance);
            }
            finally{RouteSurface.UsePlan(null);}
        }
        [Test] public void DwellCancelsOnTrackingLossAndFiresOnlyOncePerHover()
        {
            var d=new GestureDwell();Assert.IsFalse(d.Tick("start",0,true));for(int i=1;i<9;i++)Assert.IsFalse(d.Tick("start",i*.1,true));Assert.IsTrue(d.Tick("start",.91,true));
            Assert.IsFalse(d.Tick("start",1.1,true));d.Tick(null,1.2,false);
            Assert.IsFalse(d.Tick("start",1.3,true));Assert.IsFalse(d.Tick("start",1.4,false));
            Assert.IsFalse(d.Tick("start",2,true));for(int i=1;i<9;i++)Assert.IsFalse(d.Tick("start",2+i*.1,true));Assert.IsTrue(d.Tick("start",2.91,true));
        }
        [Test] public void FiveSuccessfulMovesEarnComboAndRetryClearsIt()
        {
            var run=new RunSession(new RunConfig());
            for(int i=0;i<5;i++)run.RecordDodge();
            Assert.AreEqual(5,run.DodgeStreak);Assert.AreEqual(5,run.BestDodgeStreak);
            Assert.AreEqual(0,run.Score.Score,"Combos are recorded but do not score");
            run.Reset();Assert.AreEqual(0,run.DodgeStreak);Assert.AreEqual(0,run.BestDodgeStreak);
        }
        [Test] public void ActivitiesSumToTotalAndDoNotCountIdleKeyboardOrPause()
        {
            var w=new WorkoutEstimate();w.SetWeight("70");w.SetGoal(4);
            for(int i=0;i<5;i++)w.TickActivity(12,true,true,true,i);
            double total=0;for(int i=0;i<5;i++)total+=w.KcalFor(i);
            Assert.AreEqual(4.9,total,.00001);Assert.AreEqual(total,w.EstimatedKcal,.00001);Assert.IsTrue(w.GoalReached);
            w.TickActivity(90,false,true,true,1);w.TickActivity(90,true,false,true,1);w.TickActivity(90,true,true,false,1);
            Assert.AreEqual(60,w.ActiveSeconds);w.Reset();Assert.AreEqual(0,w.KcalFor(1));Assert.IsFalse(w.GoalReached);
            Assert.IsFalse(w.SetGoal(double.NaN));Assert.IsFalse(w.SetGoal(0));
        }
    }
}
