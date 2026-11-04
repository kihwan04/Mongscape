using System;
using System.Collections;
using System.IO;
using Muhanok.Application;
using Muhanok.Bootstrap;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using Muhanok.Presentation;
using Muhanok.Presentation.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Muhanok.Tests
{
    // The tutorial runs in GameScene on its fixed course; a member with unlocked items plays it against a fake account server.
    public sealed class TutorialPlayModeTests
    {
        private string root;
        private GameBootstrap game;
        private HUDPresenter hud;
        private FakeAccountGateway fake;
        [SetUp] public void TempFolder() { root=Path.Combine(Path.GetTempPath(),"mongscape-tutorial-"+Guid.NewGuid().ToString("N")); }
        [TearDown] public void RemoveFolder() { Time.timeScale=1; if(Directory.Exists(root))Directory.Delete(root,true); }

        private IEnumerator OpenAsMember()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            game=Object.FindFirstObjectByType<GameBootstrap>();hud=game.GetComponent<HUDPresenter>();fake=new FakeAccountGateway();
            game.UseAccountServices(fake,new PlayerProgressStore(root));
            game.SetDeveloperMode(false,false);
            fake.Profile.coins=40;fake.Profile.unlocked=new[]{(int)ItemKind.Magnet,(int)ItemKind.DoubleScore,(int)ItemKind.Shield};
            game.Account.SignInMember(fake.Profile,"fake-token");Assert.IsTrue(game.SignedIn);Assert.IsFalse(game.Account.TutorialDone);
            ((IHudData)game).PreviewStyle(PlayStyle.Keyboard);
        }
        private IEnumerator WaitRunning()
        {
            float deadline=Time.realtimeSinceStartup+10;
            while((game.IsLoading||game.IsRevealing||game.Session.Flow.State!=GameFlowState.Running)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);Assert.IsTrue(game.Session.IsTutorial);Assert.IsTrue(game.Tutorial.Active);
        }

        [UnityTest]
        public IEnumerator TutorialMistakeRetriesAndLeavesRankingCoinsAndItemsAlone()
        {
            yield return OpenAsMember();
            game.OpenMainMenu();hud.SelectMenu(MenuAction.Tutorial);
            yield return WaitRunning();
            CollectionAssert.IsEmpty(game.RunItems(),"No item spawns in the tutorial");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(0,Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(TutorialStep.Lane,game.Tutorial.Script.Current);
            Assert.IsTrue(game.Session.Hit(true));
            Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State,"A tutorial mistake is never a capture");
            yield return null;yield return null;
            Assert.IsTrue(game.Tutorial.Retrying);Assert.AreEqual("다시 해 볼까요?",game.Tutorial.Message);
            float deadline=Time.realtimeSinceStartup+5;
            while(game.Tutorial.Retrying&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(game.Tutorial.Retrying);Assert.AreEqual(1,game.Tutorial.Script.TotalRetries);Assert.AreEqual(TutorialStep.Lane,game.Tutorial.Script.Current);
            Assert.Less(game.Session.Runner.Z,TutorialCourse.LaneChunk*RouteSurface.Length+TutorialCourse.LaneBlockZ,"Restarted in front of the step");
            game.TogglePause();game.ExitToMenu();
            Assert.AreEqual(GameFlowState.TitleIdle,game.Session.Flow.State);Assert.IsFalse(game.Session.IsTutorial);Assert.AreEqual(MenuPage.Main,hud.Page);
            Assert.AreEqual(0,game.Store.Board.All.Count,"Nothing goes to this PC's ranking");
            Assert.AreEqual(0,fake.SavedRuns.Count,"Nothing goes to the account server");
            Assert.AreEqual(40,game.Account.Coins);Assert.IsFalse(game.Account.TutorialDone);CollectionAssert.DoesNotContain(fake.Calls,"profile/update");
            Assert.IsNotEmpty(game.RunItems(),"Ordinary runs keep the unlocked items");
            game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator KeyboardTutorialCompletesAndMarksTheMember()
        {
            yield return OpenAsMember();
            hud.OpenMainMenu();hud.SelectMenu(MenuAction.Play);
            Assert.AreEqual(MenuPage.TutorialOffer,hud.Page,"An unfinished member is offered the tutorial on 게임 시작");
            hud.ChooseTutorial(TutorialChoice.Start);
            yield return WaitRunning();
            CollectionAssert.AreEqual(new TutorialScript(PlayStyle.Keyboard).Steps,game.Tutorial.Script.Steps);
            var run=game.Session.Runner;
            Time.timeScale=3;
            float deadline=Time.realtimeSinceStartup+150;
            while(game.Tutorial.Active&&Time.realtimeSinceStartup<deadline)
            {
                var t=game.Tutorial;var step=t.Script.Current;
                if(!t.Retrying&&!t.Script.Finished&&game.Session.Flow.State==GameFlowState.Running)
                {
                    bool near=t.LessonDistance<=TutorialDirector.ActDistance;
                    if(step==TutorialStep.Lane&&near&&run.Lane==0)run.Command(MotionCommand.Left);
                    else if(step==TutorialStep.Jump&&near&&!t.Resumed&&run.Y<=.001f)run.Command(MotionCommand.Jump);
                    else if(step==TutorialStep.Crouch&&near)run.Command(MotionCommand.Crouch);
                    else if(step==TutorialStep.HighKnee&&near)run.Command(MotionCommand.HighKnee);
                    else if(step==TutorialStep.Rail&&t.LessonDistance<=TutorialDirector.SlowDistance&&run.Lane==0)run.Command(TutorialCourse.RailLean);
                    else if(step==TutorialStep.Fork&&(RouteSurface.ActivePlan?.ForkWindow(run.Z)??false)&&run.Lane==0)run.Command(MotionCommand.Left);
                }
                yield return null;
            }
            Time.timeScale=1;
            Assert.IsFalse(game.Tutorial.Active,"The keyboard tutorial reaches the end");
            Assert.AreEqual(GameFlowState.TitleIdle,game.Session.Flow.State);Assert.AreEqual(MenuPage.Main,hud.Page);
            // The frame that finished the tutorial stops there: no running dust or run fade over the menu.
            var dust=game.Hero.transform.Find("VFX_Dust_Run")?.GetComponentInChildren<ParticleSystem>();
            if(dust!=null)Assert.IsFalse(dust.isPlaying,"Running dust stays stopped on the menu");
            Assert.AreEqual(1f,hud.Fade);
            Assert.IsTrue(game.Account.TutorialDone);CollectionAssert.Contains(fake.Calls,"profile/update");
            Assert.IsTrue(fake.Updates[fake.Updates.Count-1].TutorialDone==true);
            Assert.AreEqual(0,game.Store.Board.All.Count);Assert.AreEqual(0,fake.SavedRuns.Count);Assert.AreEqual(40,game.Account.Coins);
            hud.SelectMenu(MenuAction.Play);Assert.AreEqual(MenuPage.Setup,hud.Page,"A finished member goes straight to the setup");
            game.FaceLogout(false);
        }
    }
}
