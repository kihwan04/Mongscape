using System;
using System.Collections;
using Muhanok.Application;
using Muhanok.Bootstrap;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using Muhanok.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Muhanok.Tests
{
    public sealed class MergedMenuPlayModeTests
    {
        [UnityTest]
        public IEnumerator HudPausesWithoutSearchingAndResumesCleanly()
        {
            yield return SceneManager.LoadSceneAsync("GameScene"); yield return null;
            var game = Object.FindFirstObjectByType<GameBootstrap>();
            var hud = game.GetComponent<HUDPresenter>();
            game.StartPlayerRun(new PlayerProfile { Name="HUD QA", Weight="70", Style=PlayStyle.Keyboard, CalorieGoal=180 });
            float deadline=Time.realtimeSinceStartup+10;
            while((game.IsLoading||game.IsRevealing)&&Time.realtimeSinceStartup<deadline) yield return null;
            yield return null;
            Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State); Assert.IsFalse(hud.RunModel.ShowWorkout);
            game.TogglePause(); yield return null;
            int searches=hud.RunModel.CueScans;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(searches,hud.RunModel.CueScans);
            game.TogglePause(); yield return null; yield return null;
            Assert.Greater(hud.RunModel.CueScans,searches);
            game.TogglePause(); game.ExitToMenu(); Assert.AreEqual(1,Time.timeScale);
        }
        [UnityTest]
        public IEnumerator MenuFacePageAndCalorieGoalRemainConnected()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();var hud=game.GetComponent<HUDPresenter>();
            var data=(IHudData)game;var host=(ISettingsHost)game;
            host.Settings.DeveloperMode=false;game.ApplySettings(false);
            data.SaveProfile(new PlayerProfile{Name="Menu QA",Weight="70",Style=PlayStyle.Keyboard,CalorieGoal=180});
            game.OpenMainMenu();Assert.AreEqual(MenuPage.Main,hud.Page);
            hud.ShowIdentity();Assert.IsTrue(hud.IdentityOpen);
            hud.ShowPage(MenuPage.Setup);Assert.AreEqual(180,data.Player.CalorieGoal);
            game.StartPlayerRun(data.Player.Clone());
            float deadline=Time.realtimeSinceStartup+10;
            while((game.IsLoading||game.IsRevealing)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);
            Assert.AreEqual(180,game.Session.Workout.GoalKcal);
            int before=game.Store.Board.All.Count;
            game.TogglePause();game.ExitToMenu();
            Assert.AreEqual(GameFlowState.TitleIdle,game.Session.Flow.State);Assert.AreEqual(MenuPage.Main,hud.Page);
            Assert.IsFalse(game.Session.Paused);Assert.AreEqual(1,Time.timeScale);
            Assert.AreEqual(before,game.Store.Board.All.Count,"Abandoning from pause must not create a run record");
        }

        [UnityTest]
        public IEnumerator IdentityScreenDrawsFaceGuideWithoutErrors()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();var hud=game.GetComponent<HUDPresenter>();
            ((ISettingsHost)game).Settings.DeveloperMode=false;game.ApplySettings(false);
            game.OpenMainMenu();hud.ShowIdentity();Assert.IsTrue(hud.IdentityOpen);
            // Let OnGUI lay out and repaint the camera view and face guide a few times.
            for(int i=0;i<3;i++)yield return null;
            Assert.IsTrue(hud.IdentityOpen);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MemberNicknameStaysTheServersAndSignOutReturnsToGuest()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();var data=(IHudData)game;var fake=new FakeAccountGateway();
            game.UseAccountServices(fake);
            ((ISettingsHost)game).Settings.DeveloperMode=false;game.ApplySettings(false);
            data.SaveProfile(new PlayerProfile{Name="Local QA",Weight="70",Style=PlayStyle.Keyboard});
            Assert.IsFalse(game.SignedIn);Assert.AreEqual("Local QA",data.SavedProfile.Name);CollectionAssert.IsEmpty(fake.Calls,"Guest settings stay in memory");
            game.Account.SignInMember(new AccountProfile{id="synthetic-profile",name="Server QA",style=(int)PlayStyle.Keyboard},"synthetic-test-token");
            Assert.IsTrue(game.SignedIn);Assert.AreEqual("Server QA",data.SavedProfile.DisplayName);Assert.AreEqual("Server QA",data.Player.Name);
            data.SaveProfile(new PlayerProfile{Name="Different",Weight="65",Style=PlayStyle.Keyboard,CalorieGoal=140});
            Assert.AreEqual("Server QA",data.Player.Name);Assert.AreEqual(140,data.SavedProfile.CalorieGoal);
            CollectionAssert.Contains(fake.Calls,"profile/update");
            game.FaceLogout(false);
            Assert.IsFalse(game.SignedIn);Assert.AreEqual(1,fake.Logouts);Assert.AreEqual(AccountSession.GuestName,data.SavedProfile.Name);
        }
    }
}
