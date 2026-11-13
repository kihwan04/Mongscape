using System.Collections;
using Muhanok.Bootstrap;
using Muhanok.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Muhanok.Tests
{
    public sealed class VisualBudgetTests
    {
        [UnityTest] public IEnumerator PooledMapsRetainPaintedSurfacesAndBoundedPointShadows()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();
            Assert.IsNotNull(game.OutputCamera.GetComponent<PointShadowBudget>());
            bool found=false;
            foreach(float distance in new[]{5f,103f,140f,199f,260f})
            {
                game.Map.Tick(distance);game.OutputCamera.transform.position=new Vector3(0,2,distance);
                yield return null;
                int count=0;
                foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if(light.type!=LightType.Point||light.shadows==LightShadows.None)continue;
                    count++;Assert.AreEqual(1,light.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier);
                }
                Assert.LessOrEqual(count,2,"Point lights consume six shadow atlas faces each");
                if(distance==5f)Assert.Greater(count,0,"Mining lanterns must actually cast shadows, not just satisfy an empty upper bound");
                foreach(var chunk in game.Map.Active)
                    foreach(var renderer in chunk.Chunk.GetComponentsInChildren<MeshRenderer>())
                        foreach(var material in renderer.sharedMaterials)
                            if(material!=null&&material.shader.name=="Muhanok/HandPaintedTriplanar")
                            {found=true;Assert.IsNotNull(material.GetTexture("_BaseMap"));}
            }
            Assert.IsTrue(found,"Authored material must be used by actual pooled renderers");
        }
    }
}
