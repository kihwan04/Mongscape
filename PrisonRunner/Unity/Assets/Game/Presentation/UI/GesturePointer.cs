using Muhanok.Domain;
using UnityEngine;
namespace Muhanok.Presentation
{
    // One interaction pass per visible screen; leaving a target cancels dwell in the same repaint.
    public static class GesturePointer
    {
        private static readonly GestureDwell dwell=new GestureDwell();
        private static readonly HandMenuPointer hand=new HandMenuPointer();
        public static Vector2 Position {get;private set;}
        public static bool Visible=>hand.Visible;
        // Hand pointing is on for this screen (the setting is on and the menu camera runs); hints follow it.
        public static bool Enabled {get;set;}=true;
        // Screen hints: the hand part only while hand pointing is on, then the keys.
        public static string Hint(string keys)=>Enabled?"손으로 버튼을 잠시 가리키거나 "+keys:keys;
        private static bool hovered;
        private static string target;
        private static Rect targetRect;
        public static void Feed(PoseFrame frame,bool valid,double now)
        {
            int previousHand=hand.Hand;
            hand.Feed(frame,valid,now);
            Position=new Vector2(hand.X*1280,hand.Y*720);
            if(!Visible || previousHand!=hand.Hand){dwell.Reset();target=null;}
        }
        public static void BeginPass(){if(Event.current.type==EventType.Repaint)hovered=false;}
        public static bool Activate(Rect rect,string id)
        {
            if(Event.current.type!=EventType.Repaint || !GUI.enabled || !Visible || hovered || !rect.Contains(Position))return false;
            hovered=true;
            if(target!=id || targetRect!=rect){dwell.Reset();target=id;targetRect=rect;}
            return dwell.Tick(id,Time.realtimeSinceStartupAsDouble,true,.75);
        }
        public static void Draw()
        {
            if(Event.current.type!=EventType.Repaint)return;
            if(!hovered){dwell.Reset();target=null;}
            if(!Visible)return;
            var old=GUI.color;
            GUI.color=new Color(.22f,1,.65f,.95f);
            if(hovered)
            {
                var r=targetRect;
                GUI.DrawTexture(new Rect(r.x,r.y,r.width,3),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x,r.yMax-3,r.width,3),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x,r.y,3,r.height),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.xMax-3,r.y,3,r.height),Texture2D.whiteTexture);
            }
            GUI.DrawTexture(new Rect(Position.x-6,Position.y-6,12,12),Texture2D.whiteTexture);
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8-Mathf.PI*.5f;
                GUI.color=new Color(.22f,1,.65f,i<dwell.Progress*16?1:.2f);
                GUI.DrawTexture(new Rect(Position.x+Mathf.Cos(angle)*20-2,Position.y+Mathf.Sin(angle)*20-2,4,4),Texture2D.whiteTexture);
            }
            GUI.color=old;
        }
        public static void Reset(){hand.Reset();dwell.Reset();target=null;hovered=false;}
    }
}
