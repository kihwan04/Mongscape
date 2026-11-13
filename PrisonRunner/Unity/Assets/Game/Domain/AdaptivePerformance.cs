using System;
namespace Muhanok.Domain
{
    // Multi-second windows ignore isolated stalls, pause/loading and startup shader compilation.
    public sealed class AdaptivePerformance
    {
        public int Level {get;private set;}
        private double warmup,window,healthy;
        private int frames,slow;
        public bool Tick(double dt,bool playing,int frameCap)
        {
            if(!playing){warmup=window=healthy=0;frames=slow=0;return false;}
            if(double.IsNaN(dt)||double.IsInfinity(dt)||dt<=0||dt>.25)return false;
            if(warmup<5){warmup+=dt;return false;}
            double budget=1.0/(frameCap==30?30:60);
            window+=dt;frames++;if(dt>budget*1.2)slow++;
            if(window<2)return false;
            double average=window/frames; bool overloaded=average>budget*1.15 && slow>frames*.25;
            bool comfortable=average<=budget*1.05;double elapsed=window;
            window=0;frames=slow=0;
            if(overloaded){healthy=0;if(Level<3){Level++;return true;}}
            else if(comfortable){healthy+=elapsed;if(healthy>=20&&Level>0){Level--;healthy=0;return true;}}
            else healthy=0;
            return false;
        }
        public void Reset(){Level=0;warmup=window=healthy=0;frames=slow=0;}
        public void Apply(UserSettings requested,UserSettings result)
        {
            result.RenderScale=Math.Max(.5f,requested.RenderScale-Level*.1f);
            result.DetailDistance=Math.Max(.5f,requested.DetailDistance-Level*.15f);
            result.Shadows=Level>=2?ShadowSetting.Off:Level==1&&requested.Shadows==ShadowSetting.High?ShadowSetting.Low:requested.Shadows;
            result.AmbientOcclusion=requested.AmbientOcclusion&&Level==0;
        }
    }
}
