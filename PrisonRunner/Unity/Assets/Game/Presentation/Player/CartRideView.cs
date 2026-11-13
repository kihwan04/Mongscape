using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    public sealed class CartRideView : MonoBehaviour
    {
        private RunSession session;
        private RunnerView hero;
        private Transform body;
        private AudioSource sound;
        private float fall;
        public bool Visible=>body!=null&&body.gameObject.activeSelf;
        public void Configure(RunSession run,RunnerView actor,GameObject prefab)
        {
            session=run;hero=actor;
            if(prefab==null)return;
            body=Instantiate(prefab,transform).transform;body.name="Actual track cart";
            sound=body.GetComponent<AudioSource>();body.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if(session==null||body==null)return;
            bool visible=session.Runner.Riding&&(session.Flow.State==GameFlowState.Running||session.Flow.State==GameFlowState.Caught);
            body.gameObject.SetActive(visible);
            if(session.Flow.State==GameFlowState.Running)hero.SetVisible(visible);
            else if(session.Flow.State==GameFlowState.Caught)hero.SetVisible(visible||session.Reason!=CaptureReason.BrokenRail);
            if(!visible){fall=0;return;}
            RouteSurface.Sample(session.Runner.Z,out float x,out float y);
            RouteSurface.Sample(session.Runner.Z+.3f,out float nx,out float ny);
            bool fallen=session.Flow.State==GameFlowState.Caught&&session.Reason==CaptureReason.BrokenRail;
            if(fallen)fall+=Time.deltaTime;
            float choiceOffset=RouteSurface.ActivePlan?.ChoiceLaneOffset(session.Runner.Z,session.Runner.X)??0;
            body.position=new Vector3(x+choiceOffset,y-fall*fall*7,session.Runner.Z);
            body.rotation=Quaternion.LookRotation(new Vector3(nx-x,ny-y,.3f))*Quaternion.Euler(0,0,-session.Runner.CartLean*12+(fallen?fall*100:0));
            if(fallen)hero.transform.position=new Vector3(x+session.Runner.WorldLaneOffset,y+.25f-fall*fall*7,session.Runner.Z);
            if(sound!=null)
            {
                bool rolling=session.Flow.State==GameFlowState.Running&&!session.Paused;
                if(rolling&&!sound.isPlaying)sound.Play();if(!rolling&&sound.isPlaying)sound.Stop();
            }
        }
    }
}
