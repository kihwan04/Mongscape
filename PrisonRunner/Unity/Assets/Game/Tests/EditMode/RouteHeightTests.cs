using Muhanok.Domain;
using NUnit.Framework;
namespace Muhanok.Tests
{
    public sealed class RouteHeightTests
    {
        [TearDown] public void Reset() => RouteSurface.UsePlan(null);
        [Test] public void StairRiseIsFourAndAHalfMetres()
        {
            Assert.AreEqual(4.5f,RouteSurface.StairRise);
            foreach(int type in new[]{7,13,14})Assert.AreEqual(4.5f,RouteSurface.Rise(type));
            Assert.AreEqual(-3f,RouteSurface.Rise(11));
        }
        [Test] public void HeightIsTheRunningSumOfEarlierRises()
        {
            var plan=RoutePlan.Tutorial(new RunConfig{ExtendedRoutes=false},1,new[]{0,7,4,11,0,7,7,0,0});
            Assert.AreEqual(0f,plan.BaseHeight(0),.0001f);
            Assert.AreEqual(0f,plan.BaseHeight(1),.0001f);
            Assert.AreEqual(4.5f,plan.BaseHeight(2),.0001f);
            Assert.AreEqual(1.5f,plan.BaseHeight(4),.0001f);
            Assert.AreEqual(6f,plan.BaseHeight(6),.0001f);
            Assert.AreEqual(10.5f,plan.BaseHeight(7),.0001f,"After two further stair chunks the following chunks sit 9 m above the first stair top");
            Assert.AreEqual(10.5f,plan.BaseHeight(8),.0001f);
        }
        [Test] public void WithoutAPlanTheAuthoredLapRepeatsAndKeepsClimbing()
        {
            RouteSurface.UsePlan(null);
            Assert.AreEqual(0f,RouteSurface.BaseHeight(0),.0001f);
            Assert.AreEqual(4.5f,RouteSurface.BaseHeight(5),.0001f);
            Assert.AreEqual(1.5f,RouteSurface.BaseHeight(9),.0001f);
            Assert.AreEqual(1.5f,RouteSurface.BaseHeight(10),.0001f);
            Assert.AreEqual(3f,RouteSurface.BaseHeight(19),.0001f);
        }
        [Test] public void ForkChoiceRewritesTheTailAndItsHeights()
        {
            var plan=new RoutePlan(new RunConfig(),7);
            plan.UpdateChoice(CartFork.Decision,0);
            float sum=0; for(int i=0;i<RoutePlan.ExtendedLapChunks;i++)sum+=RouteSurface.Rise(plan.TypeAt(i));
            Assert.AreEqual(sum,plan.BaseHeight(RoutePlan.ExtendedLapChunks),.0001f);
            for(int i=0;i<RoutePlan.ExtendedLapChunks*3;i++)Assert.AreEqual(plan.BaseHeight(i)+RouteSurface.Rise(plan.TypeAt(i)),plan.BaseHeight(i+1),.0001f);
        }
        [TestCase(-1)] [TestCase(0)] [TestCase(1)]
        public void SurfaceHeightIsContinuousAcrossEverySeam(int lane)
        {
            var plan=new RoutePlan(new RunConfig(),11); RouteSurface.UsePlan(plan);
            for(int lap=0;lap<8;lap++)plan.UpdateChoice(lap*RoutePlan.ExtendedLapChunks*RouteSurface.Length+CartFork.Decision+1,lap%3-1+0*lane);
            for(int s=0;s<300;s++)
            {
                RouteSurface.Sample((s+1)*RouteSurface.Length-.0001f,out _,out float y0);
                RouteSurface.Sample((s+1)*RouteSurface.Length,out _,out float y1);
                Assert.Less(System.Math.Abs(y0-y1),1e-3f,"Height jump at chunk "+(s+1));
                Assert.AreEqual(plan.BaseHeight(s+1),y1,1e-4f);
            }
        }
    }
}
