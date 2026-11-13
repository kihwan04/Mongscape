using UnityEditor;
using UnityEngine;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        private static void AnimateCharacters()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{Root+"/Animations"}))
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                string name=clip.name; bool guard=name.StartsWith("AN_Guard_"),run=name.EndsWith("Run")||name.EndsWith("SprintStart")||name.EndsWith("HighKnee");
                bool mine=name.EndsWith("MinePickaxe_Loop"),crouch=name.EndsWith("CrouchSlide"),idea=name.EndsWith("IdeaReact"),catchPose=name.EndsWith("Catch")||name.EndsWith("Caught");
                float length=run?.65f:1.2f; clip.ClearCurves();
                foreach(string part in new[]{"Arm_L","Arm_R","Leg_L","Leg_R"})
                {
                    bool arm=part.StartsWith("Arm"),left=part.EndsWith("L"); float sign=left?1:-1;
                    float amplitude=run?(arm?30:36):mine?(arm?31:0):catchPose?(arm?5:0):2;
                    float center=mine&&arm?-55:catchPose&&arm?-63:crouch&&!arm?36:0;
                    if(!arm) sign=-sign;
                    clip.SetCurve(part,typeof(Transform),"localEulerAnglesRaw.x",new AnimationCurve(new Keyframe(0,center-amplitude*sign),new Keyframe(length/2,center+amplitude*sign),new Keyframe(length,center-amplitude*sign)));
                    clip.SetCurve(part,typeof(Transform),"localEulerAnglesRaw.z",AnimationCurve.Constant(0,length,(arm?9:3)*sign));
                }
                clip.SetCurve("Head",typeof(Transform),"localPosition.y",AnimationCurve.Constant(0,length,crouch?1.12f:1.48f));
                clip.SetCurve("Head",typeof(Transform),"localEulerAnglesRaw.y",AnimationCurve.EaseInOut(0,0,length*.6f,name.EndsWith("LookCart")?85:0));
                clip.SetCurve("Head",typeof(Transform),"localEulerAnglesRaw.x",AnimationCurve.Constant(0,length,mine?12:idea?-8:0));
                clip.SetCurve("Body",typeof(Transform),"localEulerAnglesRaw.x",AnimationCurve.Constant(0,length,mine?12:run?8:crouch?28:0));
                clip.SetCurve("Body",typeof(Transform),"localPosition.y",new AnimationCurve(new Keyframe(0,crouch?.62f:.92f),new Keyframe(length/2,crouch?.62f:run?.95f:.92f),new Keyframe(length,crouch?.62f:.92f)));
                // Unity binds all scale axes together; explicitly preserve the muzzle width/depth.
                clip.SetCurve("Head/Mouth",typeof(Transform),"localScale.x",AnimationCurve.Constant(0,length,.37f));
                clip.SetCurve("Head/Mouth",typeof(Transform),"localScale.y",AnimationCurve.Constant(0,length,idea?.20f:mine?.065f:.10f));
                clip.SetCurve("Head/Mouth",typeof(Transform),"localScale.z",AnimationCurve.Constant(0,length,.05f));
                var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=run||mine||guard&&name.EndsWith("Notice"); AnimationUtility.SetAnimationClipSettings(clip,settings); EditorUtility.SetDirty(clip);
            }
        }
    }
}
