using System;
namespace Muhanok.Domain
{
    public sealed class HandMenuPointer
    {
        public bool Visible { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public int Hand { get; private set; } = -1;
        private double lastTime = -1, lastRaised = -1;
        // Shoulder centre and width the hand is measured against, eased slowly: the cursor divides by the shoulder width,
        // so raw shoulder jitter used to shake the cursor three to four times harder than the hand itself moved.
        private float refCenter, refShoulder, refSpan;
        // A dropped hand keeps the cursor still this long before it disappears or the other hand takes over:
        // the wrist's visibility flickers for a frame or two while pointing.
        public const double HandGraceSeconds = .3;
        private static bool Raised(PoseFrame f, int wrist, int elbow, bool keep)
        {
            var h=f.Points[wrist]; var e=f.Points[elbow];
            // Requiring the wrist above its elbow makes lower menu buttons unreachable.
            // Instead use a shoulder-relative interaction area while keeping lowered hands inactive.
            float shoulder=(f.Points[11].Y+f.Points[12].Y)*.5f;
            float span=Math.Max(.08f,Math.Abs(f.Points[11].X-f.Points[12].X)*f.Aspect);
            // The active hand keeps going on a lower confidence than a new hand needs to take over.
            return h.Visibility>=(keep?.45f:.6f) && e.Visibility>=(keep?.4f:.5f) && h.Y<shoulder+span*.9f;
        }
        public bool Feed(PoseFrame f, bool valid, double now)
        {
            if(!valid || f==null || !f.HasPose || now<f.Time || now-f.Time>.25
                || f.Points[11].Visibility<.5f || f.Points[12].Visibility<.5f)
            { Reset(); return false; }
            int hand=Hand;
            bool kept=(hand==15||hand==16)&&Raised(f,hand,hand==16?14:13,true);
            if(!kept)
            {
                int other=hand==16?15:hand==15?16:-1;
                if(other>0&&Raised(f,other,other==16?14:13,false)) hand=other;
                else if(other<0&&Raised(f,16,14,false)) hand=16;
                else if(other<0&&Raised(f,15,13,false)) hand=15;
                // No hand qualifies: hold the cursor through a short visibility drop of the active hand.
                else if(Visible&&now>=lastRaised&&now-lastRaised<=HandGraceSeconds) return true;
                else { Reset(); return false; }
            }
            var a=f.Points[11]; var b=f.Points[12]; var point=f.Points[hand];
            float spanX=Math.Max(.08f,Math.Abs(a.X-b.X));
            float center=(a.X+b.X)*.5f, shoulder=(a.Y+b.Y)*.5f;
            bool snap=!Visible || Hand!=hand || now<lastTime || now-lastTime>.25;
            float dt=snap?0:(float)Math.Max(0,now-lastTime);
            if(snap){refCenter=center;refShoulder=shoulder;refSpan=spanX;}
            else
            {
                float ease=(float)(1-Math.Exp(-2.5*dt));
                refCenter+=(center-refCenter)*ease;refShoulder+=(shoulder-refShoulder)*ease;refSpan+=(spanX-refSpan)*ease;
            }
            float spanY=refSpan*Math.Max(.5f,f.Aspect);
            float x=Clamp((point.X-refCenter)/(refSpan*2.8f)+.5f);
            float y=Clamp((point.Y-(refShoulder-spanY*1.25f))/(spanY*2.1f));
            // Error-adaptive smoothing: small offsets (landmark jitter) follow slowly, a real reach follows quickly.
            float error=(float)Math.Sqrt((x-X)*(x-X)+(y-Y)*(y-Y));
            float rate=Math.Min(30f,5f+60f*error);
            float blend=snap?1:(float)(1-Math.Exp(-rate*dt));
            X+=(x-X)*blend;Y+=(y-Y)*blend;Visible=true;Hand=hand;lastTime=now;lastRaised=now;
            return true;
        }
        private static float Clamp(float v)=>Math.Max(0,Math.Min(1,v));
        public void Reset(){Visible=false;Hand=-1;lastTime=-1;lastRaised=-1;}
    }
}
