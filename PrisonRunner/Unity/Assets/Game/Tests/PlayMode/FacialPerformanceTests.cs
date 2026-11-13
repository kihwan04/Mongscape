using System.Collections;
using Muhanok.Bootstrap;
using Muhanok.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Muhanok.Tests
{
    public sealed class FacialPerformanceTests
    {
        [UnityTest] public IEnumerator IdeaExpressionMovesExportedFaceWhileKeepingActorRoot()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var actor=Object.FindFirstObjectByType<GameBootstrap>().Hero;
            var animator=actor.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator.GetComponent<FacialPerformance>());
            var renderer=actor.GetComponentInChildren<SkinnedMeshRenderer>();int jaw=-1;
            for(int i=0;i<renderer.sharedMesh.blendShapeCount;i++)
                if(renderer.sharedMesh.GetBlendShapeName(i).EndsWith("JawOpen"))jaw=i;
            Assert.GreaterOrEqual(jaw,0,"Actual exported Blender face must contain the animation shape");
            var root=actor.transform.position;animator.Play("AN_Monkey_IdeaReact",0,0);animator.Update(0);
            yield return new WaitForSeconds(.25f);
            Assert.Greater(renderer.GetBlendShapeWeight(jaw),60);
            Assert.AreEqual(root,actor.transform.position,"Facial animation must not move the gameplay root");
        }
    }
}
