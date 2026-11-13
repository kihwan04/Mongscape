using System;
namespace Muhanok.Domain
{
    // The same target can fire only once until the hand leaves. Missing tracking cancels progress immediately.
    public sealed class GestureDwell
    {
        private string target, fired;
        private double entered, lastSeen;
        public double Progress { get; private set; }
        public bool Tick(string hovered, double now, bool valid, double seconds=.9)
        {
            if (!valid || string.IsNullOrEmpty(hovered)) { target=null; fired=null; Progress=0; return false; }
            if (hovered!=target || now-lastSeen>.25 || now<lastSeen) { target=hovered; fired=null; entered=now; Progress=0; }
            lastSeen=now;
            if (fired==hovered) { Progress=1; return false; }
            Progress=Math.Max(0,Math.Min(1,(now-entered)/Math.Max(.2,seconds)));
            if(Progress<1) return false;
            fired=hovered; return true;
        }
        public void Reset() { target=fired=null; Progress=0; }
    }
}
