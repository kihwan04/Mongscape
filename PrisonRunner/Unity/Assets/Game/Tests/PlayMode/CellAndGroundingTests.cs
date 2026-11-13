#if UNITY_EDITOR
using System.Collections;
using Muhanok.Bootstrap;
using Muhanok.Presentation;
using Muhanok.Presentation.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Muhanok.Tests
{
    public sealed class CellAndGroundingTests
    {
        [UnityTest] public IEnumerator PooledPrisonersStayBehindBarsAndAnimate()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();game.Map.Tick(126);yield return null;
            int checkedActors=0;
            foreach(var item in game.Map.Active)
            {
                if(item.Type!=4&&item.Type!=5)continue;
                var inmates=item.Chunk.GetComponentsInChildren<CellPrisoner>();Assert.AreEqual(2,inmates.Length);
                foreach(var inmate in inmates)
                {
                    var local=inmate.transform.localPosition;
                    Assert.Greater(Mathf.Abs(local.x),4.4f,"Inmates belong behind the x=3.97 cell front");
                    Assert.Less(Mathf.Abs(local.x),7.6f,"Inmates must remain inside the back wall");
                    Assert.Less(local.z,23.8f);Assert.Greater(local.z,0);
                    var animator=inmate.GetComponent<Animator>();Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("AN_Monkey_CellIdle"));
                    var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);var before=hand.position;
                    animator.Update(.3f);Assert.Greater(Vector3.Distance(before,hand.position),.0005f);
                    Assert.IsFalse(inmate.GetComponent<FootGrounding>().FollowRoute);checkedActors++;
                }
            }
            Assert.GreaterOrEqual(checkedActors,2);
        }
        [UnityTest] public IEnumerator HumanoidFootIKCorrectsLowFeetButReleasesCart()
        {
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/CHR_Monkey_Prisoner.prefab"));
            try
            {
                var animator=actor.GetComponent<Animator>();var grounding=actor.GetComponent<FootGrounding>();Assert.IsNotNull(grounding);
                yield return null;animator.Play("AN_Monkey_CellIdle",0,.3f);animator.Update(.02f);
                Assert.Greater(grounding.ContactCount,0);Assert.Less(grounding.MaximumPenetration,.01f);
                foreach(var bone in new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                    Assert.GreaterOrEqual(animator.GetBoneTransform(bone).position.y,.10f,"Feet should not sink through flat floor");
                grounding.Suspended=true;animator.Play("AN_Monkey_CartRide");animator.Update(.1f);Assert.AreEqual(0,grounding.ContactCount);
                var flat=FootGrounding.SurfaceNormal(130);Assert.AreEqual(Vector3.up,flat);
                Assert.Greater(Vector3.Angle(Vector3.up,FootGrounding.SurfaceNormal(108)),1);
            }
            finally{Object.Destroy(actor);}
        }
    }
}
#endif
