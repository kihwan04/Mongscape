using System;
using System.Collections;
using System.Collections.Generic;
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
    // Result screen ranking tabs, calorie panel and personal best for members and guests, with a fake account server and a temp PC ranking.
    public sealed class RankingResultPlayModeTests
    {
        private string root;
        private GameBootstrap game;
        private HUDPresenter hud;
        private FakeAccountGateway fake;
        private IHudData Data=>game;
        private IIdentityData Identity=>game;
        private RankingBoard Board=>hud.Ranking;
        [SetUp] public void TempFolder() { root=Path.Combine(Path.GetTempPath(),"mongscape-ranking-"+Guid.NewGuid().ToString("N")); }
        [TearDown] public void RemoveFolder() { if(Directory.Exists(root))Directory.Delete(root,true); }

        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            game=Object.FindFirstObjectByType<GameBootstrap>();hud=game.GetComponent<HUDPresenter>();fake=new FakeAccountGateway();
            game.UseAccountServices(fake,new PlayerProgressStore(root));
            game.SetDeveloperMode(false,false);
        }
        // An upper-body member whose /stats says: today 52.1 kcal, lifetime 310 kcal, upper-body best `best`.
        private void SignIn(int best)
        {
            fake.Profile.style=(int)PlayStyle.UpperBody;
            fake.StatsResult=new StatsReply{ok=true,today_kcal=52.1,total_kcal=310,best_score=new[]{0,best,0}};
            game.Account.SignInMember(fake.Profile,"fake-token");Assert.IsTrue(game.SignedIn);
        }
        private static bool Has(List<KeyValuePair<string,string>> lines,string label){foreach(var line in lines)if(line.Key==label)return true;return false;}

        [UnityTest]
        public IEnumerator MemberResultStartsOnTheMemberRankingForTheModeJustPlayedToday()
        {
            yield return Open();
            fake.BoardResult=new LeaderboardReply{ok=true,entries=new[]{new LeaderboardRow{rank=1,name="민수",score=900},new LeaderboardRow{rank=2,name="회원",score=500}},me=new LeaderboardMe{rank=2,score=500}};
            SignIn(400);
            game.Session.Score.Advance(500);game.RecordFinishedRun();
            Assert.AreEqual((int)PlayStyle.UpperBody,Data.LastEntry.Mode);Assert.AreEqual(fake.Profile.id,Data.LastEntry.AccountId);
            Board.OpenResult(Data,Identity);
            Assert.IsTrue(Board.MemberTab,"회원 랭킹");Assert.AreEqual(PlayStyle.UpperBody,Board.Mode);Assert.IsTrue(Board.TodayOnly);
            var request=fake.BoardRequests[fake.BoardRequests.Count-1];
            Assert.AreEqual(PlayStyle.UpperBody,request.Mode);Assert.IsTrue(request.Today);Assert.AreEqual("fake-token",request.Token);
            Assert.AreEqual(RankingStatus.Ready,Board.Status);Assert.AreEqual(2,Board.Rows.Count);Assert.AreEqual("민수",Board.Rows[0].Name);
            Assert.IsNotNull(Board.Mine);Assert.AreEqual(2,Board.Mine.Rank);
            // This PC's tab marks the member's run; another mode's tab does not show it.
            Board.Select(false,PlayStyle.UpperBody,true);
            Assert.AreEqual(1,Board.Rows.Count);Assert.IsTrue(Board.Rows[0].Member);Assert.IsNotNull(Board.Mine);
            Board.Select(false,PlayStyle.FullBody,true);Assert.AreEqual(0,Board.Rows.Count);Assert.IsNull(Board.Mine);
            // The server board fails visibly.
            fake.FailBoard=true;Board.Select(true,PlayStyle.Keyboard,false);
            Assert.AreEqual(RankingStatus.Failed,Board.Status);Assert.AreEqual(RankingBoard.OfflineText,Board.StatusText);
            Assert.AreEqual(PlayStyle.Keyboard,fake.BoardRequests[fake.BoardRequests.Count-1].Mode);Assert.IsFalse(fake.BoardRequests[fake.BoardRequests.Count-1].Today);
            game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator GuestResultStartsOnThisPcRanking()
        {
            yield return Open();
            game.Session.Score.Advance(300);game.RecordFinishedRun();
            Board.OpenResult(Data,Identity);
            Assert.IsFalse(Board.MemberTab,"이 PC");Assert.AreEqual(Data.Player.Style,Board.Mode);Assert.IsTrue(Board.TodayOnly);
            CollectionAssert.DoesNotContain(fake.Calls,"leaderboard");
            Assert.AreEqual(1,Board.Rows.Count);Assert.IsFalse(Board.Rows[0].Member);Assert.IsNotNull(Board.Mine);Assert.AreEqual(1,Board.Mine.Rank);
            // Naming the run on the result screen shows the new name on the 이 PC board at once.
            game.StartIntro();for(int i=0;i<82;i++)game.Intro.Tick(.1f);
            game.Session.Flow.Transition(GameFlowState.Caught);game.Session.Flow.Transition(GameFlowState.Result);
            game.RecordFinishedRun();hud.OpenResult();
            Assert.IsTrue(hud.AsksGuestName);Assert.AreEqual(AccountSession.GuestName,Board.Mine.Name);
            Assert.IsTrue(hud.SaveGuestName(" 민지 "));Assert.IsFalse(hud.AsksGuestName);
            Assert.IsNotNull(Board.Mine);Assert.AreEqual("민지",Board.Mine.Name);Assert.AreEqual(2,Board.Rows.Count);
            Assert.IsTrue(Board.Rows.Exists(row=>row.Name=="민지"));
            game.Retry();
            yield return null;
        }

        [UnityTest]
        public IEnumerator KcalPanelShowsMemberTotalsAndOnlyThisRunForGuests()
        {
            yield return Open();
            game.Session.Score.Advance(300);game.RecordFinishedRun();
            Board.OpenResult(Data,Identity);
            var guest=Board.KcalLines(game.Session);
            Assert.AreEqual(1,guest.Count);Assert.AreEqual("이번 판",guest[0].Key);Assert.AreEqual(RankingBoard.GuestKcalNote,Board.KcalNote);
            CollectionAssert.DoesNotContain(fake.Calls,"stats");

            SignIn(400);
            game.RecordFinishedRun();
            Board.OpenResult(Data,Identity);
            var member=Board.KcalLines(game.Session);
            Assert.IsTrue(Has(member,"이번 판"));Assert.IsTrue(Has(member,"오늘 합계"));Assert.IsTrue(Has(member,"누적 합계"));
            Assert.AreEqual("52.1 kcal",member[1].Value);Assert.AreEqual("310 kcal",member[2].Value);Assert.AreEqual("",Board.KcalNote);
            Assert.AreEqual(1,fake.SavedRuns.Count,"The totals are read after the run was saved");

            // The totals cannot be read: the panel says so and keeps this run's line.
            fake.FailStats=true;game.RecordFinishedRun();Board.OpenResult(Data,Identity);
            Assert.AreEqual(1,Board.KcalLines(game.Session).Count);Assert.AreEqual(RankingBoard.OfflineText,Board.KcalNote);
            game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator NewBestShowsOnlyWhenTheMembersEarlierBestIsBeaten()
        {
            yield return Open();
            SignIn(400);
            Assert.AreEqual(400,Data.BestScore,"The HUD best is the member's best for the current mode");
            game.Session.Score.Advance(500);game.RecordFinishedRun();
            Board.OpenResult(Data,Identity);
            Assert.IsTrue(Board.NewBest);Assert.AreEqual("내 최고 400점",Board.BestLine,"The best before this run");

            // A higher earlier best: no 신기록.
            game.Account.SignOut();fake.StatsResult=new StatsReply{ok=true,best_score=new[]{0,900,0}};
            game.Account.SignInMember(fake.Profile,"fake-token");
            game.RecordFinishedRun();Board.OpenResult(Data,Identity);
            Assert.IsFalse(Board.NewBest);Assert.AreEqual("내 최고 900점",Board.BestLine);

            // Guests see no best, only the sign-up note.
            game.Account.SignOut();Assert.AreEqual(-1,Data.BestScore);
            game.RecordFinishedRun();Board.OpenResult(Data,Identity);
            Assert.IsFalse(Board.NewBest);Assert.AreEqual(RankingBoard.GuestBestNote,Board.BestLine);
            yield return null;
        }

        // 웹캠 보정 is offered on the result screen of camera runs only. Without a webcam the calibration never completes,
        // so the panel stays open (no hand pointing meanwhile) until it is closed or the result screen is left.
        [UnityTest]
        public IEnumerator WebcamCalibrationIsOfferedOnlyAfterCameraRuns()
        {
            yield return Open();
            game.StartIntro();for(int i=0;i<82;i++)game.Intro.Tick(.1f);
            Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);
            game.Session.Flow.Transition(GameFlowState.Caught);game.Session.Flow.Transition(GameFlowState.Result);
            Data.Player.Style=PlayStyle.Keyboard;yield return null;
            Assert.IsFalse(hud.CalibrationAvailable,"키보드 모드에는 버튼이 없다");
            hud.OpenCalibration();Assert.IsFalse(hud.CalibrationOpen);

            Data.Player.Style=PlayStyle.UpperBody;yield return null;
            Assert.IsTrue(hud.CalibrationAvailable,"카메라 모드");
            hud.OpenCalibration();Assert.IsTrue(hud.CalibrationOpen);
            yield return null;yield return null;
            Assert.IsTrue(hud.CalibrationOpen);Assert.IsFalse(GesturePointer.Visible,"No hand pointing while the panel is open");
            hud.CloseCalibration();Assert.IsFalse(hud.CalibrationOpen);

            hud.OpenCalibration();Assert.IsTrue(hud.CalibrationOpen);
            game.Retry();yield return null;
            Assert.IsFalse(hud.CalibrationAvailable);Assert.IsFalse(hud.CalibrationOpen,"Leaving the result screen closes it");
        }
    }
}
