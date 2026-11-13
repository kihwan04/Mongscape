using System;
namespace Muhanok.Domain
{
    public sealed class HandMenuPointer
    {
        public bool Visible { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public int Hand { get; private set; } = -1;
        private double lastTime = -1;
        private static bool Raised(PoseFrame f, int wrist, int elbow)
        {
            var h=f.Points[wrist]; var e=f.Points[elbow];
            // Requiring the wrist above its elbow makes lower menu buttons unreachable.
            // Instead use a shoulder-relative interaction area while keeping lowered hands inactive.
            float shoulder=(f.Points[11].Y+f.Points[12].Y)*.5f;
            float span=Math.Max(.08f,Math.Abs(f.Points[11].X-f.Points[12].X)*f.Aspect);
            return h.Visibility>=.6f && e.Visibility>=.5f && h.Y<shoulder+span*.9f;
        }
        public bool Feed(PoseFrame f, bool valid, double now)
        {
            if(!valid || f==null || !f.HasPose || now<f.Time || now-f.Time>.25
                || f.Points[11].Visibility<.5f || f.Points[12].Visibility<.5f)
            { Reset(); return false; }
            int hand=Hand;
            if(hand!=15 && hand!=16) hand=Raised(f,16,14)?16:15;
            if(!Raised(f,hand,hand==16?14:13)) hand=hand==16?15:16;
            if(!Raised(f,hand,hand==16?14:13)) { Reset(); return false; }
            var a=f.Points[11]; var b=f.Points[12]; var point=f.Points[hand];
            float spanX=Math.Max(.08f,Math.Abs(a.X-b.X));
            float spanY=spanX*Math.Max(.5f,f.Aspect);
            float center=(a.X+b.X)*.5f, shoulder=(a.Y+b.Y)*.5f;
            float x=Clamp((point.X-center)/(spanX*2.8f)+.5f);
            float y=Clamp((point.Y-(shoulder-spanY*1.25f))/(spanY*2.1f));
            bool snap=!Visible || Hand!=hand || now<lastTime || now-lastTime>.25;
            float blend=snap?1:(float)(1-Math.Exp(-18*Math.Max(0,now-lastTime)));
            X+=(x-X)*blend;Y+=(y-Y)*blend;Visible=true;Hand=hand;lastTime=now;
            return true;
        }
        private static float Clamp(float v)=>Math.Max(0,Math.Min(1,v));
        public void Reset(){Visible=false;Hand=-1;lastTime=-1;}
    }
}
