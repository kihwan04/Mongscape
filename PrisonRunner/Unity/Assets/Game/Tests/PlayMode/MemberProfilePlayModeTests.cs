using System;
using System.Collections;
using System.IO;
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
    // 내 정보, the member shop and the guest nickname on the result, with a fake account server and a temp PC ranking.
    public sealed class MemberProfilePlayModeTests
    {
        private string root;
        private GameBootstrap game;
        private HUDPresenter hud;
        private FakeAccountGateway fake;
        private IHudData Data=>game;
        [SetUp] public void TempFolder() { root=Path.Combine(Path.GetTempPath(),"mongscape-profile-"+Guid.NewGuid().ToString("N")); }
        [TearDown] public void RemoveFolder() { if(Directory.Exists(root))Directory.Delete(root,true); }

        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            game=Object.FindFirstObjectByType<GameBootstrap>();hud=game.GetComponent<HUDPresenter>();fake=new FakeAccountGateway();
            game.UseAccountServices(fake,new PlayerProgressStore(root));
            LogAssert.Expect(LogType.Log,"Developer mode off");game.SetDeveloperMode(false,false);
        }
        private void SignIn(int coins=0) { fake.Profile.coins=coins;game.Account.SignInMember(fake.Profile,"fake-token");Assert.IsTrue(game.SignedIn); }

        [UnityTest]
        public IEnumerator MemberNicknameAndSettingsAreSavedOnTheServer()
        {
            yield return Open();
            fake.StatsResult=new StatsReply{ok=true,today_kcal=12,total_kcal=340,best_score=new[]{900,0,450}};
            SignIn();
            hud.ShowPage(MenuPage.Profile);Assert.AreEqual(MenuPage.Profile,hud.Page);
            var form=hud.Profile;CollectionAssert.Contains(fake.Calls,"stats");Assert.IsNotNull(form.Stats);Assert.AreEqual(340,form.Stats.total_kcal);
            Assert.AreEqual("회원",form.Nickname);
            form.Nickname=" 새닉네임 ";form.Weight="72";form.Goal="120";form.Style=PlayStyle.UpperBody;form.Save();
            Assert.IsFalse(form.Failed,form.Message);
            Assert.AreEqual("새닉네임",game.Account.Nickname);Assert.AreEqual("새닉네임",Data.SavedProfile.Name);
            var saved=game.Account.Settings;Assert.AreEqual(72,saved.Weight);Assert.AreEqual(120,saved.Goal);Assert.AreEqual(PlayStyle.UpperBody,saved.Style);
            Assert.AreEqual("새닉네임",fake.Updates[fake.Updates.Count-1].Name);
            form.Weight="80";form.Save();
            Assert.IsNull(fake.Updates[fake.Updates.Count-1].Name,"An unchanged nickname is not sent again");Assert.AreEqual(80,game.Account.Settings.Weight);
            fake.TakenName="민수";form.Nickname="민수";form.Save();
            Assert.IsTrue(form.Failed);StringAssert.Contains("사용 중",form.Message);Assert.AreEqual("새닉네임",game.Account.Nickname,"A taken nickname leaves the session unchanged");
            int sent=fake.Updates.Count;form.Nickname="열한글자가넘는닉네임이다";form.Save();
            Assert.IsTrue(form.Failed);Assert.AreEqual(sent,fake.Updates.Count,"The nickname rule is checked before the server");
            yield return null;yield return null;LogAssert.NoUnexpectedReceived();
            game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator DeleteNeedsTwoConfirmationsAndLogoutReturnsToLoginGate()
        {
            yield return Open();
            SignIn();
            game.Store.Record(new RankingEntry{Name="회원",Distance=500,AccountId=fake.Profile.id,Ticks=DateTime.Now.Ticks},0);
            game.Store.Record(new RankingEntry{Name="게스트",Distance=300,Ticks=DateTime.Now.Ticks},0);
            hud.ShowPage(MenuPage.Profile);var form=hud.Profile;
            hud.DeleteAccount();Assert.AreEqual(ProfileDeleteStep.Ask,form.Deleting);
            yield return null;
            hud.DeleteAccount();Assert.AreEqual(ProfileDeleteStep.Confirm,form.Deleting);Assert.AreEqual(0,fake.Deletes,"Two questions come before the delete");
            yield return null;
            form.CancelDelete();Assert.AreEqual(ProfileDeleteStep.None,form.Deleting);
            hud.DeleteAccount();hud.DeleteAccount();Assert.AreEqual(0,fake.Deletes);
            hud.DeleteAccount();
            Assert.AreEqual(1,fake.Deletes);Assert.IsFalse(game.SignedIn);Assert.AreEqual(MenuPage.Login,hud.Page);
            Assert.AreEqual(1,game.Store.Board.All.Count,"The deleted member's rows leave this PC's ranking");Assert.AreEqual("",game.Store.Board.All[0].AccountId);
            SignIn();hud.ShowPage(MenuPage.Profile);
            hud.Logout();
            Assert.IsFalse(game.SignedIn);Assert.AreEqual(MenuPage.Login,hud.Page);Assert.AreEqual(1,fake.Logouts);
        }

        [UnityTest]
        public IEnumerator GuestHasNoProfilePageAndCannotBuy()
        {
            yield return Open();
            Assert.IsFalse(game.SignedIn);
            hud.ShowPage(MenuPage.Profile);Assert.AreEqual(MenuPage.Main,hud.Page,"내 정보 is for members only");
            hud.SelectMenu(MenuAction.Profile);Assert.AreEqual(MenuPage.Main,hud.Page);
            bool? bought=null;string reason="";
            Data.Unlock(ItemKind.Magnet,(ok,why)=>{bought=ok;reason=why;});
            Assert.AreEqual(false,bought);StringAssert.Contains("회원가입",reason);
            CollectionAssert.DoesNotContain(fake.Calls,"shop/buy");CollectionAssert.IsEmpty(game.RunItems());
            hud.ShowPage(MenuPage.Shop);
            for(int i=0;i<3;i++)yield return null;
            Assert.AreEqual(MenuPage.Shop,hud.Page);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MemberPurchaseUpdatesCoinsAndUnlockedItems()
        {
            yield return Open();
            SignIn(100);
            hud.ShowPage(MenuPage.Shop);yield return null;
            bool? bought=null;string reason="";
            Data.Unlock(ItemKind.Magnet,(ok,why)=>{bought=ok;reason=why;});
            Assert.AreEqual(true,bought,reason);CollectionAssert.Contains(fake.Calls,"shop/buy");
            Assert.AreEqual(70,game.Account.Coins);Assert.AreEqual(70,Data.Wallet.Coins);
            CollectionAssert.Contains(game.Account.Unlocked,ItemKind.Magnet);CollectionAssert.AreEqual(new[]{ItemKind.Magnet},game.RunItems());
            Data.Unlock(ItemKind.Magnet,(ok,why)=>{bought=ok;reason=why;});
            Assert.AreEqual(false,bought);StringAssert.Contains("이미",reason);
            Data.Unlock(ItemKind.Shield,(ok,why)=>{bought=ok;reason=why;});
            Assert.AreEqual(false,bought);StringAssert.Contains("부족",reason);Assert.AreEqual(70,game.Account.Coins);
            yield return null;LogAssert.NoUnexpectedReceived();
            game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator GuestNicknameRenamesTheRecordedGuestRun()
        {
            yield return Open();
            game.RecordFinishedRun();
            var entry=game.Store.Board.All[0];Assert.AreEqual(AccountSession.GuestName,entry.Name,"The run is recorded as 게스트 first");
            Assert.IsTrue(Data.CanNameLastRun);
            Assert.IsFalse(Data.NameLastRun("열한글자가넘는닉네임이다"),"At most 10 characters");Assert.IsFalse(Data.NameLastRun("  "));
            Assert.AreEqual(AccountSession.GuestName,entry.Name);
            Assert.IsTrue(Data.NameLastRun(" 민지 "));
            Assert.AreEqual("민지",game.Store.Board.All[0].Name);Assert.AreEqual(1,game.Store.Board.All.Count,"Renamed, not recorded twice");
            Assert.AreEqual("민지",new PlayerProgressStore(root).Board.All[0].Name,"The new name is saved in records.json");
            Assert.AreEqual(AccountSession.GuestName,game.Account.Nickname,"The next guest run starts as 게스트 again");
            SignIn();game.RecordFinishedRun();Assert.IsFalse(Data.CanNameLastRun,"Members keep their nickname");
            game.FaceLogout(false);
            game.SetDeveloperMode(true,false);
            try
            {
                game.RecordFinishedRun();
                Assert.IsFalse(Data.CanNameLastRun,"Developer mode keeps the 플레이어 N name");StringAssert.StartsWith("플레이어 ",Data.LastEntry.Name);
            }
            finally { game.SetDeveloperMode(false,false); }
        }
    }
}
