using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Runs on the Animator object. Domain owns translation; IK only corrects contact.
    [RequireComponent(typeof(Animator))]
    public sealed class FootGrounding : MonoBehaviour
    {
        public bool FollowRoute;
        public bool Suspended;
        public float SoleHeight = .13f;
        public int ContactCount { get; private set; }
        public float MaximumPenetration { get; private set; }
        private Animator animator;
        private readonly Vector3[] planted=new Vector3[2];
        private readonly bool[] locked=new bool[2];
        private Vector3 previousRoot;
        private void OnEnable(){locked[0]=locked[1]=false;previousRoot=transform.position;}
        private void Awake() => animator = GetComponent<Animator>();
        public static Vector3 SurfaceNormal(float z)
        {
            RouteSurface.Sample(z-.05f,out _,out float a);
            RouteSurface.Sample(z+.05f,out _,out float b);
            return new Vector3(0,1,-(b-a)/.1f).normalized;
        }
        private void OnAnimatorIK(int layer)
        {
            ContactCount=0; MaximumPenetration=0;
            if(animator==null||!animator.isHuman||Suspended){locked[0]=locked[1]=false;return;}
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if(state.IsName("AN_Monkey_CartRide")||state.IsName("AN_Monkey_Jump")){locked[0]=locked[1]=false;return;}
            if(Vector3.Distance(transform.position,previousRoot)>1)locked[0]=locked[1]=false;
            previousRoot=transform.position;
            Ground(AvatarIKGoal.LeftFoot,HumanBodyBones.LeftFoot);
            Ground(AvatarIKGoal.RightFoot,HumanBodyBones.RightFoot);
        }
        private void Ground(AvatarIKGoal goal,HumanBodyBones bone)
        {
            var foot=animator.GetBoneTransform(bone); if(foot==null)return;
            Vector3 animated=animator.GetIKPosition(goal);
            float floor=transform.position.y; Vector3 normal=Vector3.up;
            if(FollowRoute){RouteSurface.Sample(animated.z,out _,out floor);normal=SurfaceNormal(animated.z);}
            float clearance=animated.y-floor-SoleHeight;
            // Correct low stance feet without pinning an elevated swing leg to the floor.
            float contact=1-Mathf.SmoothStep(.035f,.19f,clearance);
            int index=goal==AvatarIKGoal.LeftFoot?0:1;
            if(contact<=0){locked[index]=false;return;}
            Vector3 target=animated; target.y=floor+SoleHeight;
            if(contact>.8f)
            {
                if(!locked[index]){planted[index]=target;locked[index]=true;}
                Vector3 drift=target-planted[index];drift.y=0;
                // Bounded stance lock avoids stretching legs during a fast lane change.
                if(drift.sqrMagnitude<.28f*.28f){target.x=planted[index].x;target.z=planted[index].z;}
                else locked[index]=false;
            }
            else locked[index]=false;
            animator.SetIKPositionWeight(goal,contact);
            animator.SetIKRotationWeight(goal,contact*.85f);
            animator.SetIKPosition(goal,target);
            animator.SetIKRotation(goal,Quaternion.FromToRotation(Vector3.up,normal)*animator.GetIKRotation(goal));
            ContactCount++;
            MaximumPenetration=Mathf.Max(MaximumPenetration,Mathf.Max(0,-clearance)*(1-contact));
        }
    }
}
