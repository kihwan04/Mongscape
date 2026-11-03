using System;
using Muhanok.Application;
using UnityEngine;
namespace Muhanok.Presentation
{
    // The face account as the login gate, sign-up and capture screens see it; implemented by the bootstrap.
    public interface IIdentityData
    {
        bool IdentityBusy {get;}
        bool SignedIn {get;}
        string IdentityMessage {get;}
        Texture Portrait {get;}
        // Account server check for the login gate: ServerProblem is empty when it answered, otherwise the reason.
        bool ServerChecking {get;}
        string ServerProblem {get;}
        // Shown on the login gate after an expired member session; empty otherwise.
        string LoginNotice {get;}
        void CheckServer();
        // done(available, reason) once the server answered.
        void CheckNickname(string name,Action<bool,string> done);
        // done(true) once the face signed in as a member; false keeps the screen for another try.
        void FaceLogin(Action<bool> done);
        void FaceEnroll(string nickname,AccountSettings settings,Action<bool> done);
        void FaceLogout(bool delete);
        // 내 정보 (members). done(ok, reason) once the server answered; a taken nickname gives its reason.
        void SaveMemberProfile(string nickname,AccountSettings settings,Action<bool,string> done);
        // Totals for 내 정보; null without a member.
        void MemberStats(Action<StatsReply> done);
        // 회원 랭킹: the server board for one play style (the member's own row in "me" when signed in).
        void MemberLeaderboard(Muhanok.Domain.PlayStyle mode,bool today,Action<LeaderboardReply> done);
        // Deletes the account on the server and returns to a guest.
        void DeleteAccount(Action<bool,string> done);
    }
    // Face capture step shared by face login and sign-up: camera with the face guide, one capture button and back.
    internal sealed class IdentityScreen
    {
        private readonly HudSkin skin;
        private GUIStyle paragraph,notice;
        public IdentityScreen(HudSkin skin){this.skin=skin;}
        public void Draw(IHudData data,IIdentityData identity,string heading,string guide,string action,Action capture,Action back)
        {
            paragraph??=new GUIStyle(skin.Style(20,FontStyle.Normal)){wordWrap=true};
            notice??=new GUIStyle(skin.Style(17,FontStyle.Normal)){wordWrap=true};
            skin.Card(new Rect(0,0,1280,720),new Color(.025f,.035f,.06f,.99f));
            GUI.Label(new Rect(80,45,950,56),heading,skin.Style(38,FontStyle.Bold));
            // Video only: no body skeleton or pose status here, just a fixed face guide. Hand pointing still runs.
            var view=PosePreview.Draw(skin,new Rect(70,130,560,400),data,showStatus:false,drawSkeleton:false);
            DrawFaceGuide(view,identity.IdentityBusy);
            GUI.Label(new Rect(680,145,510,160),guide,paragraph);
            GUI.Label(new Rect(680,305,510,90),"전시용 얼굴 로그인입니다. 사진·영상 위조를 차단하는 보안 인증 기능은 포함하지 않습니다.",notice);
            GUI.enabled=!identity.IdentityBusy;
            if(skin.PrimaryButton(new Rect(680,420,490,64),action)||Enter())capture?.Invoke();
            GUI.enabled=true;
            GUI.Label(new Rect(70,552,560,60),GesturePointer.Hint("Enter를 누르세요."),paragraph);
            GUI.Label(new Rect(70,615,950,60),identity.IdentityMessage,skin.Style(18,FontStyle.Normal));
            GUI.enabled=!identity.IdentityBusy;
            if(skin.FlatButton(new Rect(1020,615,190,52),"뒤로",false))back?.Invoke();
            GUI.enabled=true;
        }
        // Enter (or keypad Enter) presses the screen's main button.
        internal static bool Enter()
        {
            var e=Event.current;
            if(!GUI.enabled||e.type!=EventType.KeyDown||(e.keyCode!=KeyCode.Return&&e.keyCode!=KeyCode.KeypadEnter))return false;
            e.Use();return true;
        }
        private static readonly Color GuideIdle=new Color(1f,1f,1f,.85f),Shade=new Color(0,0,0,.55f);
        // Fixed oval face guide over the video: shade outside, dotted outline, hint below. Repaint only.
        private void DrawFaceGuide(Rect view,bool busy)
        {
            if(Event.current.type!=EventType.Repaint)return;
            float h=view.height*.6f,w=h*.75f,cx=view.center.x,cy=view.center.y,top=cy-h*.5f,bottom=cy+h*.5f;
            var white=Texture2D.whiteTexture;
            GUI.color=Shade;
            GUI.DrawTexture(new Rect(view.x,view.y,view.width,top-view.y),white);
            GUI.DrawTexture(new Rect(view.x,bottom,view.width,view.yMax-bottom),white);
            // 3px rows left and right of the oval; rows tile exactly so the shade never doubles.
            for(float y=top;y<bottom;y+=3)
            {
                float rh=Mathf.Min(3,bottom-y),dy=(y+rh*.5f-cy)/(h*.5f),half=w*.5f*Mathf.Sqrt(Mathf.Max(0,1-dy*dy));
                GUI.DrawTexture(new Rect(view.x,y,cx-half-view.x,rh),white);
                GUI.DrawTexture(new Rect(cx+half,y,view.xMax-cx-half,rh),white);
            }
            GUI.color=busy?Palette.Gold:GuideIdle;
            for(int i=0;i<160;i++)
            {
                float a=i*Mathf.PI*2f/160f;
                GUI.DrawTexture(new Rect(cx+Mathf.Cos(a)*w*.5f-2.5f,cy+Mathf.Sin(a)*h*.5f-2.5f,5,5),white);
            }
            GUI.color=busy?Palette.Gold:Color.white;
            GUI.Label(new Rect(view.x,bottom+8,view.width,30),busy?"가만히 계세요":"틀 안에 얼굴을 맞춰 주세요",skin.Centered(22,FontStyle.Bold));
            GUI.color=Color.white;
        }
    }
}
