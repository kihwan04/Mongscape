using System.Collections;
using Muhanok.Bootstrap;
using Muhanok.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Muhanok.Tests
{
    public sealed class StartAndGateTests
    {
        [UnityTest] public IEnumerator WeightSetupLoadingAndPauseExcludeInactiveTime()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();Assert.IsNotNull(game.LoadingImage);
            Assert.AreEqual(1672,game.LoadingImage.width,"Loading import must retain source aspect instead of power-of-two resizing");
            Assert.AreEqual(941,game.LoadingImage.height);
            game.OpenStartSetup();Assert.IsTrue(game.StartSetupOpen);
            game.Session.Config.ExtendedRoutes=false;game.BeginRun("70");Assert.IsTrue(game.IsLoading);Assert.AreEqual(70,game.Session.Workout.WeightKg);
            yield return new WaitForSecondsRealtime(1.1f);Assert.IsFalse(game.IsLoading);Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);
            Assert.IsTrue(game.IsRevealing,"Loading image must dissolve into the preposed live world");
            Assert.AreEqual(0,game.Intro.Elapsed,.001f,"The loading screen replaces the separate 8 second intro");
            float beforeReveal=game.Session.Runner.Z;
            yield return new WaitForSecondsRealtime(.9f);Assert.IsFalse(game.IsRevealing);
            Assert.AreEqual(0,game.Intro.Elapsed,"Do not start a second cinematic after loading");
            Assert.Greater(game.Session.Runner.Z,beforeReveal,"Gameplay resumes after the real scene is revealed");
            game.TogglePause();float z=game.Session.Runner.Z;game.Session.Tick(1);
            Assert.IsTrue(game.Session.Paused);Assert.AreEqual(z,game.Session.Runner.Z);Assert.AreEqual(0,game.Session.Workout.EstimatedKcal);
            game.TogglePause();Assert.IsFalse(game.Session.Paused);Assert.AreEqual(1,Time.timeScale);
        }
        [UnityTest] public IEnumerator PrisonDoorsCloseToFloorAndLeaveOtherLanesOpen()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();game.Map.Tick(126);
            bool found=false;
            foreach(var item in game.Map.Active)
            {
                if(item.Type<4||item.Type>6)continue;
                found=true;
                Assert.AreEqual(item.Type==6?3:6,item.Chunk.Gates.Length);
                foreach(var gate in item.Chunk.Gates)
                {
                    gate.Preview(gate.transform.position.z-20);Assert.AreEqual(0,gate.Closure);
                    gate.Preview(gate.transform.position.z-7);Assert.AreEqual(1,gate.Closure);
                    Assert.AreEqual(0,gate.Panel.localPosition.y-1.65f,.001f,"Bottom edge must reach floor");
                    gate.ResetGate();Assert.AreEqual(5,gate.Panel.localPosition.y);
                }
                for(int row=0;row<2;row++)
                {
                    int count=0;for(int lane=0;lane<3;lane++)if(item.Chunk.Obstacles[row*3+lane].gameObject.activeSelf)count++;
                    Assert.LessOrEqual(count,1,"Closed doors must leave other lanes open; exercise rows are separate");
                }
            }
            Assert.IsTrue(found,"At least one prison chunk must be checked");
        }
    }
}
