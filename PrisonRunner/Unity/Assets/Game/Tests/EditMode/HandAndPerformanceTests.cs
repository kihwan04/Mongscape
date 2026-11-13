using System;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using NUnit.Framework;
namespace Muhanok.Tests
{
    public sealed class HandAndPerformanceTests
    {
        private sealed class CameraStub : IPoseEstimator
        {
            public bool Disposed,Started;
            public PoseStatus Status=>PoseStatus.NoCamera;
            public string Message=>"test";
            public UnityEngine.Texture CameraTexture=>null;
            public string Diagnostics=>"";
            public void Start(){Started=true;}
            public void Stop(){}
            public void Tick(double now){}
            public bool TryTakeFrame(PoseFrame target)=>false;
            public void Dispose(){Disposed=true;}
        }
        [Test] public void RefreshRestartsCameraDiscoveryAfterHotPlugAndKeepsStyleAndInferenceLimit()
        {
            var before=PoseEstimatorRegistry.Factory;CameraStub last=null;PoseCameraOptions options=null;
            try
            {
                PoseEstimatorRegistry.Factory=o=>{options=o;return last=new CameraStub();};
                using(var tracker=new PoseTracker(new PoseGameInput(),new PoseCameraOptions(),new MotionTuning(),()=>0,()=>false))
                {
                    tracker.SetStyle(PlayStyle.UpperBody);var first=last;
                    tracker.SetInferenceRate(20);Assert.AreEqual(20,options.MaxInferenceRate);
                    tracker.RefreshCamera();Assert.IsTrue(first.Disposed);Assert.AreNotSame(first,last);
                    Assert.IsTrue(last.Started);Assert.AreEqual(PlayStyle.UpperBody,tracker.Style);Assert.AreEqual(20,options.MaxInferenceRate);
                    tracker.SetStyle(PlayStyle.Keyboard);tracker.RefreshCamera();Assert.IsFalse(tracker.UsesCamera);
                }
            }
            finally{PoseEstimatorRegistry.Factory=before;}
        }
        private static PoseFrame Frame(double time,float x=.5f,float y=.35f)
        {
            var f=new PoseFrame{HasPose=true,Time=time,Aspect=4f/3};
            f.Points[11]=new PoseLandmark{X=.4f,Y=.4f,Visibility=1};
            f.Points[12]=new PoseLandmark{X=.6f,Y=.4f,Visibility=1};
            f.Points[14]=new PoseLandmark{X=.6f,Y=.5f,Visibility=1};
            f.Points[16]=new PoseLandmark{X=x,Y=y,Visibility=1};return f;
        }
        [TestCase(.22f,.066f,0,0)]
        [TestCase(.78f,.626f,1,1)]
        [TestCase(.5f,.3467f,.5f,.5f)]
        public void HandCanReachTheWholeMenuIncludingLowerButtons(float x,float y,float expectedX,float expectedY)
        {
            var pointer=new HandMenuPointer();Assert.IsTrue(pointer.Feed(Frame(1,x,y),true,1));
            Assert.AreEqual(expectedX,pointer.X,.01);Assert.AreEqual(expectedY,pointer.Y,.01);
        }
        [Test] public void LostStaleFutureOrLowConfidencePoseCannotClick()
        {
            var p=new HandMenuPointer();Assert.IsTrue(p.Feed(Frame(1),true,1));
            Assert.IsFalse(p.Feed(Frame(1),true,1.26));Assert.IsFalse(p.Feed(Frame(2),true,1));
            var f=Frame(2);f.Points[11].Visibility=.1f;Assert.IsFalse(p.Feed(f,true,2));
            Assert.IsFalse(p.Feed(Frame(3),false,3));Assert.IsFalse(p.Visible);
        }
        [Test] public void ActiveHandStaysSelectedUntilItLowers()
        {
            var f=Frame(1);var p=new HandMenuPointer();p.Feed(f,true,1);Assert.AreEqual(16,p.Hand);
            f.Points[13]=new PoseLandmark{X=.4f,Y=.5f,Visibility=1};
            f.Points[15]=new PoseLandmark{X=.3f,Y=.3f,Visibility=1};f.Time=1.1;
            p.Feed(f,true,1.1);Assert.AreEqual(16,p.Hand);
            f.Points[16].Y=.9f;f.Time=1.2;p.Feed(f,true,1.2);Assert.AreEqual(15,p.Hand);
        }
        [Test] public void PointerSmoothingUsesElapsedTimeRatherThanDisplayRefreshRate()
        {
            var a=new HandMenuPointer();var b=new HandMenuPointer();
            a.Feed(Frame(0,.3f),true,0);b.Feed(Frame(0,.3f),true,0);
            for(int i=1;i<=30;i++)a.Feed(Frame(i/30.0,.7f),true,i/30.0);
            for(int i=1;i<=60;i++)b.Feed(Frame(i/60.0,.7f),true,i/60.0);
            Assert.AreEqual(a.X,b.X,.00001);
        }
        [Test] public void DwellSwitchingAwayAndReturningRearmsButNeverRapidRepeats()
        {
            var d=new GestureDwell();
            for(int i=0;i<10;i++)d.Tick("a",i*.1,true,.75);
            Assert.IsFalse(d.Tick("a",1,true,.75));d.Tick("b",1.1,true,.75);
            Assert.IsFalse(d.Tick("a",1.2,true,.75));bool fired=false;
            for(int i=1;i<=9;i++)fired|=d.Tick("a",1.2+i*.1,true,.75);
            Assert.IsTrue(fired);Assert.IsFalse(d.Tick("a",2.2,true,.75));
            d.Tick(null,2.21,false);Assert.AreEqual(0,d.Progress);
        }
        private static void Feed(AdaptivePerformance p,double seconds,double dt,int cap=60,bool live=true)
        {for(int i=0;i<(int)(seconds/dt);i++)p.Tick(dt,live,cap);}
        [Test] public void SustainedSlowFramesReduceGraphicsWithoutChangingSavedPreferences()
        {
            var p=new AdaptivePerformance();Feed(p,13,1.0/20);Assert.AreEqual(3,p.Level);
            var requested=new UserSettings();var effective=requested.Clone();p.Apply(requested,effective);
            Assert.AreEqual(1,requested.RenderScale);Assert.Less(effective.RenderScale,requested.RenderScale);
            Assert.AreEqual(ShadowSetting.Off,effective.Shadows);Assert.IsFalse(effective.AmbientOcclusion);
            Assert.AreEqual(requested.FrameCap,effective.FrameCap);Assert.IsTrue(requested.AutoPerformance);
        }
        [Test] public void PauseStartupSingleStallsAndNormalThirtyFpsDoNotReduceQuality()
        {
            var p=new AdaptivePerformance();Feed(p,4,1.0/20);Assert.AreEqual(0,p.Level);
            Feed(p,30,1.0/20,60,false);Assert.AreEqual(0,p.Level);
            Feed(p,30,1.0/30,30);Assert.AreEqual(0,p.Level);
            p.Tick(.2,true,30);Feed(p,4,1.0/30,30);Assert.AreEqual(0,p.Level);
        }
        [Test] public void RecoveryIsSlowAndNeverExceedsUserCeiling()
        {
            var p=new AdaptivePerformance();Feed(p,10,1.0/20);int reduced=p.Level;Assert.Greater(reduced,0);
            Feed(p,10,1.0/60);Assert.AreEqual(reduced,p.Level);
            Feed(p,20,1.0/60);Assert.Less(p.Level,reduced);
            var requested=new UserSettings{RenderScale=.55f,DetailDistance=.55f,Shadows=ShadowSetting.Off,AmbientOcclusion=false};
            var effective=requested.Clone();p.Apply(requested,effective);
            Assert.That(effective.RenderScale,Is.InRange(.5f,.55f));Assert.AreEqual(ShadowSetting.Off,effective.Shadows);
            p.Reset();Assert.AreEqual(0,p.Level);
        }
    }
}
