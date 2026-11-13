using Muhanok.Application;
using Muhanok.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Muhanok.Presentation
{
    public sealed class CinematicFocus : MonoBehaviour
    {
        public Volume Volume;
        private RunSession session;
        private RunnerView hero;
        private DepthOfField depth;
        public void Configure(RunSession run,RunnerView actor){session=run;hero=actor;}
        private void Start()
        {
            if(Volume!=null&&Volume.profile.TryGet<DepthOfField>(out var value))depth=value;
        }
        private void LateUpdate()
        {
            if(depth==null||session==null||hero==null)return;
            var state=session.Flow.State;
            depth.active=state==GameFlowState.TitleIdle||state==GameFlowState.IntroTimeline;
            depth.focusDistance.value=Mathf.Max(.8f,Vector3.Distance(transform.position,hero.Head));
        }
    }
}
