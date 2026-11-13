using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Alert state only. The HUD draws one compact siren; no camera-space geometry is created.
    public sealed class PolicePursuitCue : MonoBehaviour
    {
        private RunSession run;
        private int lastHits;
        private float remaining;
        public bool Visible=>run!=null&&run.Flow.State==GameFlowState.Running&&!run.Paused&&(remaining>0||run.SlipPursuit);
        public void Configure(RunSession session){run=session;}
        private void Update()
        {
            if(run==null)return;
            bool live=run.Flow.State==GameFlowState.Running;
            if(run.SlipStrikes==0){lastHits=0;remaining=0;}
            if(live&&run.SlipStrikes>lastHits){lastHits=run.SlipStrikes;remaining=3.6f;}
            if(live&&!run.Paused)remaining=Mathf.Max(0,remaining-Time.unscaledDeltaTime);
        }
    }
}
