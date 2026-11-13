using System.Collections;
using Muhanok.Bootstrap;
using Muhanok.Domain;
using Muhanok.Presentation.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Muhanok.Tests
{
    public sealed class ZooEncounterTests
    {
        private static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();game.Pose.SetStyle(PlayStyle.Keyboard);game.BeginRun("70");
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);game.enabled=false;
            RoutePlan plan=null;
            for(int lane=-1;lane<=1;lane++)
            {
                var candidate=new RoutePlan(game.Session.Config,723);candidate.UpdateChoice(120,lane);
                if(candidate.DestinationAt(6)<0){plan=candidate;break;}
            }
            Assert.IsNotNull(plan);RouteSurface.UsePlan(plan);game.Map.Reset(723);
        }
        [UnityTest] public IEnumerator NeckAndTrunkStayConnectedToTheAnimalThroughTheWholeMotion()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();
            var animals=game.GetComponentsInChildren<ZooHazard>(true);Assert.AreEqual(8,animals.Length);
            foreach(var animal in animals)
            {
                foreach(float distance in new[]{40f,30f,23f,18f,14f,0f})
                {
                    animal.Preview(animal.transform.position.z-distance);
                    Assert.Less(Vector3.Distance(animal.Reach.Base,animal.Attachment.position),.001f);
                    Assert.Less(Vector3.Distance(animal.Reach.Point(.499f),animal.Reach.Point(.501f)),.12f,"No gap between curve sections");
                    if(animal.Giraffe)Assert.Less(Vector3.Distance(animal.Head.position,animal.Reach.Tip),.001f);
                    else Assert.Less(Vector3.Distance(animal.Moving.position,animal.Reach.Tip),.001f);
                }
                Assert.AreEqual(1,animal.Progress);float x=animal.Reach.Point(.5f).x;
                Assert.That(x,Is.InRange(2.5f,2.8f));Assert.Less(animal.Reach.Point(.75f).y,.8f);
                int poses=animal.Reach.PoseUpdates;for(int i=0;i<100;i++)animal.Preview(animal.transform.position.z);
                Assert.AreEqual(poses,animal.Reach.PoseUpdates,"An unchanged pose must reuse its existing mesh buffers");
            }
        }
        [UnityTest] public IEnumerator AllThreeEncountersAppearAndClearTheirRowWithoutOverlappingAuthoredHazards()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();int seen=0;
            for(float z=180;z<1050;z+=48)
            {
                game.Session.Runner.SetStartZ(z);game.Map.Tick(z);
                foreach(var item in game.Map.Active)
                {
                    foreach(var animated in item.Chunk.Animated)
                    {
                        var animal=animated as ZooHazard;var tree=animated as FallenTreeHazard;
                        if(animal==null&&tree==null)continue;
                        seen|=tree!=null?4:animal.Giraffe?1:2;
                        foreach(var h in item.Chunk.Obstacles)Assert.IsFalse(h.gameObject.activeSelf);
                        foreach(var h in item.Chunk.ExerciseObstacles)Assert.IsFalse(h.gameObject.activeSelf);
                        var hazards=tree!=null?tree.Hazards:animal.Hazards;Assert.AreEqual(3,hazards.Length);
                        foreach(var h in hazards){Assert.IsTrue(h.FullExerciseRow);Assert.AreEqual(ObstacleKind.Crate,h.Kind);}
                    }
                }
            }
            Assert.AreEqual(7,seen,"Giraffe, elephant and tree should all be available in one zoo route");
        }
        [UnityTest] public IEnumerator FallenTreeGivesWarningThenLiesLowAndCanBeJumped()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();FallenTreeHazard tree=null;
            for(float z=180;z<700&&tree==null;z+=24)
            {
                game.Session.Runner.SetStartZ(z);game.Map.Tick(z);
                foreach(var item in game.Map.Active)foreach(var animated in item.Chunk.Animated)if(animated is FallenTreeHazard found){tree=found;break;}
            }
            Assert.IsNotNull(tree);float at=tree.transform.position.z;
            tree.Preview(at-40);Assert.AreEqual(0,tree.Progress);
            tree.Preview(at-23);Assert.That(tree.Progress,Is.InRange(.4f,.6f));
            tree.Preview(at-14);Assert.AreEqual(1,tree.Progress);Assert.Less(Mathf.Abs(tree.Pivot.up.y),.001f);
            game.Session.Reset();game.Session.Runner.SetStartZ(at);game.Session.Runner.Command(MotionCommand.Jump);game.Session.Runner.Tick(.2f,0);
            foreach(var h in tree.Hazards){h.ResetPass();h.Evaluate(game.Session,at-1);}Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);
            game.Session.Reset();game.Session.Runner.SetStartZ(at);
            foreach(var h in tree.Hazards){h.ResetPass();h.Evaluate(game.Session,at-1);}Assert.AreEqual(GameFlowState.Caught,game.Session.Flow.State);
            game.Map.Reset();foreach(var pooled in game.GetComponentsInChildren<FallenTreeHazard>(true))Assert.AreEqual(0,pooled.Progress);
        }
    }
}
