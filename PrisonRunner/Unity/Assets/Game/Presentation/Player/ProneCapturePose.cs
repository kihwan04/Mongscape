using UnityEngine;

namespace Muhanok.Presentation
{
    // A reversible pose on the existing humanoid. Hands and toes support the horizontal body.
    public sealed class ProneCapturePose : MonoBehaviour
    {
        private RunnerView actor;
        private Animator animator;
        private Transform visual;
        private Transform[] bones;
        private Quaternion[] rotations;
        private Vector3[] positions;
        private Vector3 visualPosition;
        private Quaternion visualRotation;
        private bool posing, animatorEnabled;
        private static readonly HumanBodyBones[] supports={HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftToes,HumanBodyBones.RightToes};
        public float Blend { get; private set; }
        public void Configure(RunnerView view)
        {
            actor=view;animator=view.RigAnimator;visual=view.Visual;
            bones=visual.GetComponentsInChildren<Transform>(true);
            rotations=new Quaternion[bones.Length];positions=new Vector3[bones.Length];
        }
        public void Begin()
        {
            if(posing)ResetPose();
            animatorEnabled=animator!=null&&animator.enabled;
            if(animator!=null){animator.Play("AN_Monkey_Caught",0,0);animator.Update(0);animator.enabled=false;}
            for(int i=0;i<bones.Length;i++){rotations[i]=bones[i].localRotation;positions[i]=bones[i].localPosition;}
            visualPosition=visual.localPosition;visualRotation=visual.localRotation;posing=true;Blend=0;
        }
        private Transform Bone(HumanBodyBones id)=>animator!=null&&animator.isHuman?animator.GetBoneTransform(id):null;
        private void Aim(HumanBodyBones parent,HumanBodyBones child,Vector3 direction,float blend)
        {
            var a=Bone(parent);var b=Bone(child);if(a==null||b==null)return;
            var desired=Quaternion.FromToRotation(b.position-a.position,direction)*a.rotation;
            a.rotation=Quaternion.Slerp(a.rotation,desired,blend);
        }
        public void Apply(float blend,float floor,float reaction)
        {
            if(!posing)return;Blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(blend));
            for(int i=0;i<bones.Length;i++){bones[i].localRotation=rotations[i];bones[i].localPosition=positions[i];}
            visual.localRotation=visualRotation*Quaternion.Euler(90*Blend,0,0);
            visual.localPosition=visualPosition+new Vector3(0,(.62f-reaction*.035f)*Blend,-1.1f*Blend);
            for(int side=0;side<2;side++)
            {
                bool left=side==0;float sign=left?-1:1;
                Aim(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm,left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,new Vector3(sign*.2f,-1,.2f),Blend);
                Aim(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand,new Vector3(sign*.08f,-1,.18f),Blend);
                Aim(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg,left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,new Vector3(sign*.06f,-.18f,-1),Blend);
                Aim(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot,new Vector3(0,-.18f,-1),Blend);
                Aim(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot,left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes,new Vector3(0,-1,.1f),Blend);
            }
            var head=Bone(HumanBodyBones.Head);if(head!=null)head.rotation=Quaternion.AngleAxis(-95*Blend,actor.transform.right)*head.rotation;
            if(Blend>.001f)
            {
                float low=float.PositiveInfinity;
                foreach(var id in supports)
                {var bone=Bone(id);if(bone!=null)low=Mathf.Min(low,bone.position.y);}
                if(!float.IsInfinity(low))visual.position+=Vector3.up*(floor+.07f-low)*Blend;
            }
        }
        public void ResetPose()
        {
            if(!posing)return;
            for(int i=0;i<bones.Length;i++)if(bones[i]!=null){bones[i].localRotation=rotations[i];bones[i].localPosition=positions[i];}
            visual.localPosition=visualPosition;visual.localRotation=visualRotation;
            if(animator!=null)animator.enabled=animatorEnabled;posing=false;Blend=0;
        }
    }
}
