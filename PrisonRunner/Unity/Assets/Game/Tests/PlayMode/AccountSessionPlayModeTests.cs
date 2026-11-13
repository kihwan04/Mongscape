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
    // GameBootstrap reads the player only through AccountSession; the account server is a fake and this PC's ranking a temp folder.
    public sealed class AccountSessionPlayModeTests
    {
        private string root;
        private GameBootstrap game;
        private FakeAccountGateway fake;
        [SetUp] public void TempFolder() { root=Path.Combine(Path.GetTempPath(),"mongscape-account-"+Guid.NewGuid().ToString("N")); }
        [TearDown] public void RemoveFolder() { if(Directory.Exists(root))Directory.Delete(root,true); }

        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            game=Object.FindFirstObjectByType<GameBootstrap>();fake=new FakeAccountGateway();
            game.UseAccountServices(fake,new PlayerProgressStore(root));
            game.SetDeveloperMode(false,false);
        }
        private IEnumerator RunKeyboard()
        {
            game.StartPlayerRun(new PlayerProfile{Name="Setup QA",Weight="65",Style=PlayStyle.Keyboard});
            float deadline=Time.realtimeSinceStartup+10;
            while((game.IsLoading||game.IsRevealing||game.Session.Flow.State!=GameFlowState.Running)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);
            yield return new WaitForSecondsRealtime(.3f);
        }
        private static ItemPickup[] ActivePickups()=>Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None);

        [UnityTest]
        public IEnumerator GuestRunGoesOnlyToThisPcRankingWithoutItems()
        {
            yield return Open();
            Assert.IsFalse(game.SignedIn);CollectionAssert.IsEmpty(game.RunItems(),"Guests get no items");
            yield return RunKeyboard();
            Assert.AreEqual(0,ActivePickups().Length,"No item is placed for a guest");
            game.RecordFinishedRun();
            Assert.AreEqual(0,fake.SavedRuns.Count,"A guest run never reaches the account server");
            Assert.AreEqual(1,game.Store.Board.All.Count);
            Assert.AreEqual(AccountSession.GuestName,game.Store.Board.All[0].Name);Assert.AreEqual("",game.Store.Board.All[0].AccountId);
            Assert.AreEqual(0,game.Account.Coins);Assert.IsFalse(((IHudData)game).RunSaveFailed);
            game.TogglePause();game.ExitToMenu();
        }

        [UnityTest]
        public IEnumerator WeightOutsideTheRangeStillStartsTheRunAt65Kg()
        {
            yield return Open();
            game.StartPlayerRun(new PlayerProfile{Name="Weight QA",Weight="500",Style=PlayStyle.Keyboard});
            Assert.AreNotEqual(GameFlowState.TitleIdle,game.Session.Flow.State,"The run starts instead of returning silently");
            Assert.AreEqual(WorkoutEstimate.DefaultWeightKg,game.Session.Workout.WeightKg);
            float deadline=Time.realtimeSinceStartup+10;
            while((game.IsLoading||game.Session.Flow.State!=GameFlowState.Running)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);
            game.TogglePause();game.ExitToMenu();
        }

        [UnityTest]
        public IEnumerator MemberRunIsSavedWithModeAndCoinsAndOnlyUnlockedItems()
        {
            yield return Open();
            fake.Balance=100;
            game.Account.SignInMember(new AccountProfile{id="member-1",name="회원QA",coins=100,unlocked=new[]{(int)ItemKind.Shield}},"token-1");
            Assert.IsTrue(game.SignedIn);Assert.AreEqual("회원QA",((IHudData)game).SavedProfile.DisplayName);
            CollectionAssert.AreEqual(new[]{ItemKind.Shield},game.RunItems());
            yield return RunKeyboard();
            foreach(var pickup in ActivePickups())Assert.AreEqual(ItemKind.Shield,pickup.Kind,"Only unlocked items are placed");
            game.RecordFinishedRun();
            Assert.AreEqual(1,fake.SavedRuns.Count);
            var run=fake.SavedRuns[0];
            Assert.AreEqual((int)PlayStyle.Keyboard,run.mode);Assert.AreEqual(game.Session.Score.Coins,run.coins);
            Assert.AreEqual(RunScore.Version,run.score_version);Assert.AreEqual(game.Session.Elapsed,run.play_seconds,.001);Assert.Greater(run.play_seconds,0);
            Assert.AreEqual(game.Session.Score.Score,run.score,"The score uses the distance and exercise formula");Assert.AreEqual(5,run.activities.Length);
            Assert.AreEqual("token-1",fake.RunTokens[0]);
            Assert.AreEqual(100+run.coins,game.Account.Coins,"The balance from the reply reaches the session");
            Assert.AreEqual("member-1",game.Store.Board.All[0].AccountId);Assert.AreEqual("회원QA",game.Store.Board.All[0].Name);
            game.TogglePause();game.ExitToMenu();
        }

        [UnityTest]
        public IEnumerator FailedMemberSaveIsShownAndResentWithTheNextRun()
        {
            yield return Open();
            game.Account.SignInMember(new AccountProfile{id="member-2",name="재시도"},"token-2");
            yield return RunKeyboard();
            fake.FailRuns=true;game.RecordFinishedRun();
            Assert.IsTrue(((IHudData)game).RunSaveFailed);
            fake.FailRuns=false;game.RecordFinishedRun();
            Assert.AreEqual(3,fake.SavedRuns.Count,"The failed run is resent once before the new one");
            Assert.AreEqual(fake.SavedRuns[0].id,fake.SavedRuns[1].id);Assert.AreNotEqual(fake.SavedRuns[1].id,fake.SavedRuns[2].id);
            Assert.IsFalse(((IHudData)game).RunSaveFailed);
            game.TogglePause();game.ExitToMenu();
        }

        // A 401 on the menu (here the shop) signs the member out at once and opens the login gate with the notice; no /logout is sent.
        [UnityTest]
        public IEnumerator ExpiredSessionOnTheMenuReturnsToTheLoginGate()
        {
            yield return Open();
            var hud=game.GetComponent<HUDPresenter>();
            game.Account.SignInMember(new AccountProfile{id="member-4",name="만료",coins=500},"token-4");
            hud.OpenMainMenu();Assert.AreEqual(MenuPage.Main,hud.Page);
            fake.Expired=true;bool? bought=null;
            ((IHudData)game).Unlock(ItemKind.Magnet,(ok,why)=>bought=ok);
            Assert.AreEqual(false,bought);Assert.IsFalse(game.SignedIn);Assert.AreEqual(0,fake.Logouts,"The token is already gone on the server");
            Assert.AreEqual(MenuPage.Login,hud.Page);Assert.AreEqual(GameBootstrap.ExpiredNotice,((IIdentityData)game).LoginNotice);
            yield return null;Assert.AreEqual(MenuPage.Login,hud.Page);
            // Signing in again clears the notice.
            fake.Expired=false;bool? back=null;game.UseAccountServices(null,null,done=>done(new[]{"face"}));game.FaceLogin(ok=>back=ok);
            Assert.AreEqual(true,back);Assert.IsTrue(game.SignedIn);Assert.AreEqual("",((IIdentityData)game).LoginNotice);
            game.FaceLogout(false);
        }

        // During a run the member stays until the run is left; then the login gate opens. The refused upload is not resent in a loop.
        [UnityTest]
        public IEnumerator ExpiredSessionDuringARunWaitsForTheMenu()
        {
            yield return Open();
            var hud=game.GetComponent<HUDPresenter>();
            game.Account.SignInMember(new AccountProfile{id="member-5",name="달리기"},"token-5");
            yield return RunKeyboard();
            fake.Expired=true;game.RecordFinishedRun();
            Assert.IsTrue(game.SignedIn,"Not signed out in the middle of a run");Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);
            Assert.IsTrue(((IHudData)game).RunSaveFailed);Assert.AreEqual(1,fake.Calls.FindAll(c=>c=="runs").Count);
            yield return null;yield return null;
            Assert.AreEqual(1,fake.Calls.FindAll(c=>c=="runs").Count,"No automatic resend");
            game.TogglePause();game.ExitToMenu();
            yield return null;
            Assert.IsFalse(game.SignedIn);Assert.AreEqual(MenuPage.Login,hud.Page);Assert.AreEqual(GameBootstrap.ExpiredNotice,((IIdentityData)game).LoginNotice);
            Assert.AreEqual(0,fake.Logouts);Assert.AreEqual("member-5",game.Store.Board.All[0].AccountId,"The run stays on this PC's ranking");
        }

        [UnityTest]
        public IEnumerator DeveloperModeOpensEveryItemAndLogoutReturnsToGuest()
        {
            yield return Open();
            game.Account.SignInMember(new AccountProfile{id="member-3",name="상점"},"token-3");
            CollectionAssert.IsEmpty(game.RunItems(),"A member without unlocks gets no items");
            game.SetDeveloperMode(true,false);
            Assert.AreEqual(ItemCatalog.All.Length,game.RunItems().Count);
            game.SetDeveloperMode(false,false);
            game.FaceLogout(false);
            Assert.AreEqual(1,fake.Logouts);Assert.IsFalse(game.SignedIn);
            Assert.AreEqual(AccountSession.GuestName,((IHudData)game).SavedProfile.Name);
        }

        [Test]
        public void UploaderRetriesAFailedRunOnlyOnce()
        {
            var session=new AccountSession();var gateway=new FakeAccountGateway{FailRuns=true};
            var uploader=new AccountRunUploader(session,gateway);
            session.SignInMember(new AccountProfile{id="m",name="m"},"t");
            uploader.Save(new AccountRun{id="a"});
            Assert.IsTrue(uploader.LastFailed);Assert.IsTrue(uploader.HasPending);
            uploader.Save(new AccountRun{id="b"});
            CollectionAssert.AreEqual(new[]{"a","a","b"},gateway.SavedRuns.ConvertAll(r=>r.id));
            Assert.IsTrue(uploader.LastFailed,"b failed and waits; a was dropped after its one retry");
            gateway.FailRuns=false;gateway.Balance=40;
            uploader.Save(new AccountRun{id="c",coins=5});
            CollectionAssert.AreEqual(new[]{"a","a","b","b","c"},gateway.SavedRuns.ConvertAll(r=>r.id));
            Assert.IsFalse(uploader.LastFailed);Assert.IsFalse(uploader.HasPending);Assert.AreEqual(45,session.Coins);
        }

        [Test]
        public void UploaderIgnoresGuestsAndDropsAnotherMembersPendingRun()
        {
            var session=new AccountSession();var gateway=new FakeAccountGateway{FailRuns=true};
            var uploader=new AccountRunUploader(session,gateway);
            uploader.Save(new AccountRun{id="guest"});
            Assert.AreEqual(0,gateway.SavedRuns.Count);
            session.SignInMember(new AccountProfile{id="first",name="first"},"t1");
            uploader.Save(new AccountRun{id="a"});
            session.SignOut();session.SignInMember(new AccountProfile{id="second",name="second"},"t2");
            gateway.FailRuns=false;uploader.Save(new AccountRun{id="b"});
            CollectionAssert.AreEqual(new[]{"a","b"},gateway.SavedRuns.ConvertAll(r=>r.id));
            Assert.AreEqual("t2",gateway.RunTokens[1]);
        }
    }
}
