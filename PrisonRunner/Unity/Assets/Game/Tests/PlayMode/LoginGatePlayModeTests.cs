using System.Collections;
using Muhanok.Application;
using Muhanok.Bootstrap;
using Muhanok.Domain;
using Muhanok.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Muhanok.Tests
{
    // Title -> login gate (face login / sign-up / guest) with a fake account server and a fake face capture.
    public sealed class LoginGatePlayModeTests
    {
        private GameBootstrap game;
        private HUDPresenter hud;
        private FakeAccountGateway fake;
        private IIdentityData Identity=>game;

        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            game=Object.FindFirstObjectByType<GameBootstrap>();hud=game.GetComponent<HUDPresenter>();fake=new FakeAccountGateway();
            game.UseAccountServices(fake,null,done=>done(new[]{"face-1","face-2","face-3"}));
            game.SetDeveloperMode(false,false);
        }

        [UnityTest]
        public IEnumerator TitleInputOpensLoginGateAndGuestStartsWithoutNicknamePrompt()
        {
            yield return Open();
            var background=InputSystem.settings.backgroundBehavior;var editorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();InputSystem.EnableDevice(mouse);
            try
            {
                InputSystem.Update();
                InputState.Change(mouse,new MouseState().WithButton(MouseButton.Left));game.TickInput();
                Assert.AreEqual(MenuPage.Login,hud.Page,"Title input opens the login gate, not the main menu");
                InputState.Change(mouse,new MouseState());
            }
            finally { InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorBehavior; }
            CollectionAssert.Contains(fake.Calls,"health");Assert.IsFalse(Identity.ServerChecking);Assert.AreEqual("",Identity.ServerProblem);
            // 게임 종료 on the gate closes the game without signing in (the real quit is swapped out here).
            var quit=hud.Quit;bool quitAsked=false;hud.Quit=()=>quitAsked=true;
            try{hud.ChooseLogin(LoginAction.Quit);}finally{hud.Quit=quit;}
            Assert.IsTrue(quitAsked);Assert.AreEqual(MenuPage.Login,hud.Page);
            hud.ChooseLogin(LoginAction.Guest);
            Assert.AreEqual(MenuPage.Main,hud.Page);Assert.IsFalse(game.SignedIn);
            Assert.AreEqual(AccountSession.GuestName,((IHudData)game).SavedProfile.Name);
            hud.SelectMenu(MenuAction.Play);
            Assert.AreEqual(MenuPage.TutorialOffer,hud.Page,"A guest is offered the tutorial once");
            hud.ChooseTutorial(TutorialChoice.Skip);
            Assert.AreEqual(MenuPage.Setup,hud.Page,"게임 시작 goes to the run setup without asking a nickname");
            hud.OpenMainMenu();hud.SelectMenu(MenuAction.Play);
            Assert.AreEqual(MenuPage.Setup,hud.Page,"The offer is shown once per session");
        }

        [UnityTest]
        public IEnumerator SignUpStepsEnrollTheChosenNicknameAndRetryKeepsInputs()
        {
            yield return Open();
            fake.TakenName="민수";
            game.OpenLogin();hud.ChooseLogin(LoginAction.SignUp);
            Assert.AreEqual(MenuPage.SignUp,hud.Page);var flow=hud.SignUp;Assert.AreEqual(SignUpStep.Consent,flow.Step);
            Assert.IsFalse(flow.Agree(),"Nothing checked: stays on consent");Assert.AreEqual(SignUpStep.Consent,flow.Step);
            flow.Toggle(ConsentItem.Age);flow.Toggle(ConsentItem.Personal);
            Assert.IsFalse(flow.Agree(),"The separate biometric consent is required");Assert.AreEqual(SignUpStep.Consent,flow.Step);
            flow.Toggle(ConsentItem.Biometric);Assert.IsTrue(flow.AllAgreed);
            flow.ToggleAll();Assert.IsFalse(flow.Agreed(ConsentItem.Age)||flow.Agreed(ConsentItem.Personal)||flow.Agreed(ConsentItem.Biometric),"All checked: 전체 동의 clears them");
            flow.ToggleAll();Assert.IsTrue(flow.Agree());Assert.AreEqual(SignUpStep.Nickname,flow.Step);
            flow.SubmitNickname("   ");Assert.AreEqual(SignUpStep.Nickname,flow.Step);StringAssert.Contains("입력",flow.NameMessage);
            flow.SubmitNickname("민수");Assert.AreEqual(SignUpStep.Nickname,flow.Step);StringAssert.Contains("사용 중",flow.NameMessage);
            flow.SubmitNickname(" 새회원 ");Assert.AreEqual(SignUpStep.Settings,flow.Step);Assert.AreEqual("새회원",flow.Nickname);
            Assert.IsFalse(flow.SubmitSettings("500",PlayStyle.UpperBody),"A member must always be able to start a run (10-350 kg)");
            Assert.AreEqual(SignUpStep.Settings,flow.Step);Assert.IsNotEmpty(flow.Validation);
            Assert.IsTrue(flow.SubmitSettings("72",PlayStyle.UpperBody));Assert.AreEqual(SignUpStep.Capture,flow.Step);
            fake.FailEnroll=true;hud.EnrollFace();
            Assert.IsFalse(game.SignedIn);Assert.AreEqual(MenuPage.SignUp,hud.Page);Assert.AreEqual(SignUpStep.Capture,flow.Step);
            Assert.AreEqual("새회원",flow.Nickname);Assert.AreEqual(72,flow.Settings.Weight);
            hud.EnrollFace();
            Assert.IsTrue(game.SignedIn);Assert.AreEqual(MenuPage.TutorialOffer,hud.Page,"A new member is offered the tutorial");Assert.AreEqual(2,fake.Enrollments.Count);
            hud.ChooseTutorial(TutorialChoice.Skip);Assert.AreEqual(MenuPage.Main,hud.Page);
            var sent=fake.Enrollments[1];
            Assert.AreEqual("새회원",sent.name);Assert.AreEqual(72,sent.weight);Assert.AreEqual((int)PlayStyle.UpperBody,sent.style);Assert.AreEqual(3,sent.images.Length);
            Assert.AreEqual("새회원",game.Account.Nickname);
            game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator CreditsReturnToWhereTheyWereOpened()
        {
            yield return Open();
            game.OpenLogin();hud.ChooseLogin(LoginAction.Credits);Assert.AreEqual(MenuPage.Credits,hud.Page);yield return null;
            hud.CloseCredits();Assert.AreEqual(MenuPage.Login,hud.Page,"Opened from the login gate");
            hud.OpenMainMenu();hud.SelectMenu(MenuAction.Credits);Assert.AreEqual(MenuPage.Credits,hud.Page);yield return null;
            hud.CloseCredits();Assert.AreEqual(MenuPage.Main,hud.Page,"Opened from the main menu");
            StringAssert.StartsWith("v"+UnityEngine.Application.version,BuildInfo.Label);
        }

        [UnityTest]
        public IEnumerator SkippedSettingsEnrollWithDefaults()
        {
            yield return Open();
            game.OpenLogin();hud.ChooseLogin(LoginAction.SignUp);var flow=hud.SignUp;
            flow.ToggleAll();flow.Agree();flow.SubmitNickname("기본값");flow.SkipSettings();Assert.AreEqual(SignUpStep.Capture,flow.Step);
            hud.EnrollFace();
            Assert.IsTrue(game.SignedIn);var sent=fake.Enrollments[0];
            Assert.AreEqual(AccountSettings.DefaultWeight,sent.weight);Assert.AreEqual((int)PlayStyle.FullBody,sent.style);
            game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator FaceLoginThenLogoutReturnsToLoginGate()
        {
            yield return Open();
            fake.Profile.name="얼굴회원";
            game.OpenLogin();hud.ChooseLogin(LoginAction.Face);Assert.AreEqual(MenuPage.Identity,hud.Page);
            hud.LoginWithFace();
            Assert.IsTrue(game.SignedIn);Assert.AreEqual(MenuPage.Main,hud.Page);Assert.AreEqual("얼굴회원",((IHudData)game).SavedProfile.Name);
            hud.SelectMenu(MenuAction.Identity);
            Assert.IsFalse(game.SignedIn);Assert.AreEqual(MenuPage.Login,hud.Page);Assert.AreEqual(1,fake.Logouts);
        }

        [UnityTest]
        public IEnumerator UnreachableServerLeavesOnlyGuestPlay()
        {
            yield return Open();
            fake.Offline=true;
            game.OpenLogin();
            Assert.AreEqual(MenuPage.Login,hud.Page);Assert.IsFalse(Identity.ServerChecking);Assert.IsNotEmpty(Identity.ServerProblem);
            // Let the gate draw its disabled buttons and reason.
            yield return null;yield return null;
            hud.ChooseLogin(LoginAction.Guest);Assert.AreEqual(MenuPage.Main,hud.Page);
            fake.Offline=false;hud.OpenLogin();Assert.AreEqual("",Identity.ServerProblem,"The next check clears the problem");
        }

        [UnityTest]
        public IEnumerator DeveloperNextPlayerSignsOutToLoginGate()
        {
            yield return Open();
            game.SetDeveloperMode(true,false);
            try
            {
                game.Account.SignInMember(new AccountProfile{id="dev-member",name="전시회원"},"dev-token");Assert.IsTrue(game.SignedIn);
                game.NextPlayer();
                Assert.IsFalse(game.SignedIn);Assert.AreEqual(MenuPage.Login,hud.Page);Assert.AreEqual(1,fake.Logouts);
            }
            finally { game.SetDeveloperMode(false,false); }
        }
    }
}
