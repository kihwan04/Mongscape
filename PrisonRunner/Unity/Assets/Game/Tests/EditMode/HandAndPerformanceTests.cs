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
            // The follow rate adapts to the remaining offset, so 30 and 60 Hz agree closely rather than bit for bit.
            Assert.AreEqual(a.X,b.X,.001);
        }
        [Test] public void LandmarkJitterBarelyMovesTheCursorButAReachStillArrivesQuickly()
        {
            var p=new HandMenuPointer();p.Feed(Frame(0),true,0);float home=p.X,worst=0;
            // ±1% wrist and shoulder jitter at 23 fps for two seconds.
            for(int i=1;i<=46;i++)
            {
                float n=i%2==0?.01f:-.01f;var f=Frame(i/23.0,.5f+n,.35f-n);f.Points[11].X+=n;f.Points[12].X-=n;
                p.Feed(f,true,i/23.0);worst=Math.Max(worst,Math.Abs(p.X-home));
            }
            Assert.Less(worst,.02f,"Raw jitter maps to about 3.6% of the screen; the cursor stays within 2%");
            for(int i=47;i<=53;i++)p.Feed(Frame(i/23.0,.7f),true,i/23.0);
            Assert.Greater(p.X,home+.25f,"A real reach covers most of the way within 0.3 s");
        }
        private static PoseFrame Face(double time,float x=.5f,float y=.45f,float eyeSpan=.08f)
        {
            var f=new PoseFrame{HasPose=true,Time=time,Aspect=4f/3};
            float half=eyeSpan*.5f/f.Aspect;
            f.Points[PoseJoint.Nose]=new PoseLandmark{X=x,Y=y+.03f,Visibility=1};
            f.Points[2]=new PoseLandmark{X=x+half,Y=y,Visibility=1};f.Points[5]=new PoseLandmark{X=x-half,Y=y,Visibility=1};return f;
        }
        [Test] public void FaceInsideTheOvalNearEnoughAndFreshCounts()
        {
            Assert.IsTrue(FaceAutoCapture.FaceInOval(Face(1),1));
            Assert.IsFalse(FaceAutoCapture.FaceInOval(Face(1,.9f),1),"Outside the oval");
            Assert.IsFalse(FaceAutoCapture.FaceInOval(Face(1,.5f,.45f,.02f),1),"Too far away");
            Assert.IsFalse(FaceAutoCapture.FaceInOval(Face(1),1.5),"Stale frame");
            var hidden=Face(1);hidden.Points[2].Visibility=.1f;Assert.IsFalse(FaceAutoCapture.FaceInOval(hidden,1),"Eyes must be seen");
        }
        [Test] public void FaceHeldForTheHoldTimeCapturesOnceUntilItLeavesOrRetryTimePasses()
        {
            var a=new FaceAutoCapture();bool fired=false;int count=0;
            for(int i=0;i<=11;i++)fired|=a.Tick(true,true,i*.1);
            Assert.IsFalse(fired,"Not before 1.2 s");Assert.Greater(a.Progress,.8);
            Assert.IsTrue(a.Tick(true,true,1.21));count++;
            // The request runs for 2 s, then fails; the face stays: no new attempt for the retry time.
            for(double t=1.3;t<3.3;t+=.1)Assert.IsFalse(a.Tick(true,false,t));
            for(double t=3.3;t<6.5;t+=.1)if(a.Tick(true,true,t))count++;
            Assert.AreEqual(1,count,"No hammering while the face simply stays");
            for(double t=7.5;t<9;t+=.1)if(a.Tick(true,true,t))count++;
            Assert.AreEqual(2,count,"Retries once the retry time has passed");
            // Leaving the oval briefly rearms at once.
            for(double t=9;t<9.6;t+=.1)a.Tick(false,true,t);
            fired=false;for(double t=9.6;t<11;t+=.1)fired|=a.Tick(true,true,t);Assert.IsTrue(fired);
            // Leaving cancels progress.
            var b=new FaceAutoCapture();b.Tick(true,true,0);b.Tick(true,true,1);b.Tick(false,true,1.1);Assert.AreEqual(0,b.Progress);Assert.IsFalse(b.Tick(true,true,1.2));
        }
        [Test] public void PlayerLockFollowsThePlayerRejectsFarBodiesAndExpires()
        {
            var g=new PlayerLockGate();Assert.IsFalse(g.Active(0));Assert.IsTrue(g.Accepts(10,0),"No reference yet: anyone");
            g.Track(320,200,0);Assert.IsTrue(g.Active(1));
            g.Band(640,out float min,out float max);Assert.AreEqual((320-240)/640f,min,1e-4);Assert.AreEqual((320+240)/640f,max,1e-4);
            Assert.IsFalse(g.Accepts(90,1),"Someone far behind (less than half the size)");Assert.AreEqual(1,g.Rejected);
            Assert.IsTrue(g.Accepts(150,1),"The player a step farther back");
            // Walking forward while tracked moves the reference with the player.
            for(int i=1;i<=20;i++)g.Track(320,260,i*.05);Assert.Greater(g.Size,250);
            Assert.IsTrue(g.Accepts(160,1.5),"Above 55% of the grown size");
            Assert.IsFalse(g.Active(1+PlayerLockGate.LockSeconds+.1),"Released a few seconds after the player was last tracked");
            Assert.IsTrue(g.Accepts(10,5));
            g.Track(100,120,10);Assert.AreEqual(100,g.X,"After expiry the next tracked body becomes the reference at once");
        }
        [Test] public void AFlickeringWristHoldsTheCursorBrieflyInsteadOfDroppingIt()
        {
            var p=new HandMenuPointer();p.Feed(Frame(1),true,1);float x=p.X;
            var f=Frame(1.1);f.Points[16].Visibility=.2f;
            Assert.IsTrue(p.Feed(f,true,1.1));Assert.IsTrue(p.Visible);Assert.AreEqual(16,p.Hand);Assert.AreEqual(x,p.X);
            f.Time=1.5;Assert.IsFalse(p.Feed(f,true,1.5),"Gone after the grace time");Assert.IsFalse(p.Visible);
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
