using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation.Map
{
    // The bridge mesh is baked with no rails in this window; per-side rail pieces and broken-rail dressing are switched per run.
    public sealed class CartRailGap : MonoBehaviour
    {
        public int RequiredLean;
        // Sibling root of the pieces below, so they stay visible while this judge is off (intact window or fork).
        public Transform Pieces;
        public GameObject LeftRail, RightRail, LeftBreak, RightBreak;
        public bool Passed {get;private set;}
        public void ResetGap()=>Passed=false;
        // -1 missing left rail, 1 missing right rail, 0 intact: both rails shown and nothing to judge.
        public void Configure(int missingSide)
        {
            Passed=false; RequiredLean=-missingSide;
            if(LeftRail!=null)LeftRail.SetActive(missingSide>=0);
            if(RightRail!=null)RightRail.SetActive(missingSide<=0);
            if(LeftBreak!=null)LeftBreak.SetActive(missingSide<0);
            if(RightBreak!=null)RightBreak.SetActive(missingSide>0);
            gameObject.SetActive(missingSide!=0);
        }
        public void Evaluate(RunSession run,float previousZ)
        {
            if(Passed||!gameObject.activeInHierarchy||run.Paused||run.Flow.State!=GameFlowState.Running)return;
            if(previousZ>transform.position.z||run.Runner.Z<transform.position.z)return;
            Passed=true;
            if(!run.Runner.Riding||run.Runner.CartLean*RequiredLean<.55f)run.Fail(CaptureReason.BrokenRail);
            else run.Score.CleanPass();
        }
    }
}
