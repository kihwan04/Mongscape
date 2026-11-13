#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Muhanok.Bootstrap;

namespace Muhanok.Tests
{
    public sealed class BlenderHumanoidTests
    {
        [UnityTest] public IEnumerator MonkeyRunMovesSkinnedJoints() => Check("CHR_Monkey_Prisoner","AN_Monkey_Run");
        [UnityTest] public IEnumerator PoliceChaseMovesSkinnedJoints() => Check("CHR_Police_Officer","AN_Guard_ChaseRun");
        [UnityTest] public IEnumerator AvailableHumanoidActionsRetargetAndKeepDomainRoot()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/CHR_Monkey_Prisoner.prefab");
            var actor=Object.Instantiate(source); actor.transform.position=new Vector3(0,0,-200);
            try
            {
                yield return null;
                var animator=actor.GetComponent<Animator>();
                foreach(string action in new[]{"Jump","CrouchSlide","Stumble"})
                {
                    string state="AN_Monkey_"+action;
                    animator.Play(state,0,0); animator.Update(0);
                    var clip=animator.GetCurrentAnimatorClipInfo(0)[0].clip;
                    string motion=action=="CrouchSlide"?"Crouch":action;
                    bool mixamoAvailable=System.IO.File.Exists("Assets/External/Staging/Mixamo/Animations/"+motion+".fbx");
                    string expected=mixamoAvailable?"Assets/External/Staging/Mixamo/Animations/":"Assets/External/Staging/Blender/Characters/";
                    StringAssert.StartsWith(expected,AssetDatabase.GetAssetPath(clip));
                    if(!mixamoAvailable) Assert.AreEqual(state,clip.name);
                    Assert.IsTrue(clip.isHumanMotion); animator.Update(clip.length*.45f);
                    Assert.AreEqual(-200,actor.transform.position.z,.001f,"Imported motion must not bypass Domain movement");
                    Assert.AreEqual(0,actor.transform.position.y,.001f);
                    var head=animator.GetBoneTransform(HumanBodyBones.Head).position.y;
                    Assert.Greater(head,.25f); Assert.Less(head,3f,"Retargeting must retain meter-scale anatomy");
                    if(action=="CrouchSlide") Assert.Less(head,1.45f,"Crouch must lower the character under a barrier");
                }
            }
            finally {Object.Destroy(actor);}
        }
        [UnityTest] public IEnumerator IntroPickaxeUsesMeterScaleOnHumanHand()
        {
            yield return SceneManager.LoadSceneAsync("GameScene"); yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();
            var grip=game.Hero.ToolAnchor; Assert.AreEqual("ToolGrip_Meters",grip.name);
            Assert.AreEqual(1,grip.lossyScale.x,.02f); Assert.AreEqual(1,grip.lossyScale.y,.02f); Assert.AreEqual(1,grip.lossyScale.z,.02f);
            var tool=grip.GetChild(0); var renderers=tool.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            Assert.Greater(bounds.size.magnitude,.8f); Assert.Less(bounds.size.magnitude,1.8f,"FBX unit scaling must not make the tool 100m tall");
            var animator=game.Hero.GetComponentInChildren<Animator>();
            animator.Play("AN_Monkey_MinePickaxe_Loop",0,0); animator.Update(0); var before=tool.position;
            animator.Update(.25f); Assert.Greater(Vector3.Distance(before,tool.position),.03f,"The mine loop must move the held tool");
        }
        private static IEnumerator Check(string id,string state)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/"+id+".prefab");
            var actor=Object.Instantiate(source); actor.transform.position=new Vector3(0,0,-200);
            var mesh=new Mesh();
            try
            {
                yield return null;
                var animator=actor.GetComponent<Animator>();
                Assert.IsTrue(animator.isHuman&&animator.avatar.isValid);
                Assert.IsFalse(animator.applyRootMotion,"Domain owns forward movement");
                Assert.IsTrue(animator.HasState(0,Animator.StringToHash(state)));
                Assert.IsNotNull(animator.GetBoneTransform(HumanBodyBones.RightHand),"Pickaxe attachment must have a real hand bone");
                animator.Play(state,0,0); animator.Update(0);
                var arm=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                var leg=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                var armBefore=arm.localRotation; var legBefore=leg.localRotation;
                animator.Update(.16f);
                Assert.Greater(Quaternion.Angle(armBefore,arm.localRotation)+Quaternion.Angle(legBefore,leg.localRotation),8,"Imported clip must move bones, not just exist in the controller");
                var renderer=actor.GetComponentInChildren<SkinnedMeshRenderer>(); renderer.BakeMesh(mesh);
                // FBX retains Blender's Z-up mesh coordinates; Y of a baked local mesh is depth.
                float extent=Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.y,mesh.bounds.size.z);
                Assert.Greater(mesh.vertexCount,3000); Assert.Greater(extent,1.5f); Assert.Less(extent,2.8f);
                float headHeight=animator.GetBoneTransform(HumanBodyBones.Head).position.y-actor.transform.position.y;
                Assert.Greater(headHeight,1.25f); Assert.Less(headHeight,2.4f);
                Assert.Less(actor.transform.position.z,-199,"Root motion must not move the runner");
            }
            finally { Object.Destroy(actor); Object.Destroy(mesh); }
        }
    }
}
#endif
