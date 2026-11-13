using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    public sealed class RunnerView : MonoBehaviour
    {
        [SerializeField] private GameObject visualRoot;
        private Animator animator;
        private FootGrounding grounding;
        private string state;
        private Transform toolSocket;
        public Vector3 Head => transform.position + Vector3.up * 1.65f;
        public bool Humanoid => animator != null && animator.isHuman;
        public Transform Visual=>visualRoot.transform;
        public Animator RigAnimator=>animator;
        public Transform ToolAnchor
        {
            get
            {
                if (Humanoid)
                {
                    if(toolSocket!=null) return toolSocket;
                    var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if(hand==null) return visualRoot.transform;
                    toolSocket=new GameObject("ToolGrip_Meters").transform; toolSocket.SetParent(hand,false);
                    var scale=hand.lossyScale;
                    toolSocket.localScale=new Vector3(1/Mathf.Max(.0001f,Mathf.Abs(scale.x)),1/Mathf.Max(.0001f,Mathf.Abs(scale.y)),1/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
                    return toolSocket;
                }
                return visualRoot.transform.Find("Arm_R") ?? visualRoot.transform;
            }
        }
        public void Configure(GameObject visual) { visualRoot = visual; animator = visual.GetComponentInChildren<Animator>(); grounding=visual.GetComponentInChildren<FootGrounding>();if(grounding!=null)grounding.FollowRoute=true;toolSocket=null; }
        public void Pose(Vector3 position, bool visible) { RouteSurface.Sample(position.z,out float x,out float y); transform.position=position+new Vector3(x,y,0); visualRoot.SetActive(visible); }
        public void PoseWorld(Vector3 position,bool visible) { transform.position=position; visualRoot.SetActive(visible); }
        public void SetVisible(bool visible)=>visualRoot.SetActive(visible);
        public void Apply(RunnerController runner)
        {
            RouteSurface.Sample(runner.Z,out float x,out float y);
            transform.position = new Vector3(runner.WorldLaneOffset+x, runner.Y+y+(runner.Riding?.25f:0), runner.Z);
            if(grounding!=null)grounding.Suspended=runner.Riding||runner.Y>.05f;
            if(animator!=null)animator.speed=runner.Riding||runner.Crouching||runner.Y>.05f||runner.Stumbling?1:Mathf.Clamp((1.65f+runner.Z/1200f)*runner.SpeedMultiplier,.8f,2.2f);
            RouteSurface.Sample(runner.Z+.25f,out float nextX,out float nextY);
            transform.rotation=Quaternion.LookRotation(new Vector3(nextX-x,nextY-y,.25f));
            if(runner.Riding)transform.rotation*=Quaternion.AngleAxis(-runner.CartLean*12,Vector3.forward);
            Play(runner.Stumbling ? "AN_Monkey_Stumble" : runner.Riding ? "AN_Monkey_CartRide" : runner.Crouching ? "AN_Monkey_CrouchSlide"
                : runner.KneeStepping ? "AN_Monkey_HighKnee" : runner.Y > .05f ? "AN_Monkey_Jump" : "AN_Monkey_Run");
        }
        public void Play(string clip)
        {
            if (clip == state) return;
            state = clip;
            if (animator != null && animator.HasState(0, Animator.StringToHash(clip))) animator.CrossFade(clip, .12f);
        }
    }
}
