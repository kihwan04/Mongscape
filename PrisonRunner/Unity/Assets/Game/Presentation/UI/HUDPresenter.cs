using System;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    public enum MenuPage { None, Main, Setup, Profile, Shop, Ranking, Identity, Login, SignUp, TutorialOffer }

    // UI root: owns the menu page state and draws exactly one screen for the current game state.
    public sealed class HUDPresenter : MonoBehaviour
    {
        private RunSession session;
        public Muhanok.Presentation.Map.ChunkSpawner Map;
        private Action retry;
        public float Fade { get; set; } = 1f;
        public Texture2D TitleLogo;
        public Texture2D LoadingImage;
        public IHudData Data;
        public Action<PlayerProfile> BeginRun;
        public Action PauseRun;
        // Result and pause: back to the main menu; developer mode result: a fresh setup for the next player.
        public Action ToMenu;
        public Action NextPlayer;
        public Action OpenSettings;
        public Action Quit;
        public Func<bool> PoliceAlert;
        // Tutorial: starts it, and decides (and remembers) whether "게임 시작" offers it first.
        public Action StartTutorial;
        public Func<bool> OfferTutorial;
        private TutorialOfferScreen tutorialOffer;
        private bool offerAfterSignUp;
        private IdentityScreen identity;
        private LoginGateScreen gate;
        private SignUpScreen signUp;
        // Sign-up step state; tests and tools can drive it without clicking.
        public SignUpFlow SignUp { get; } = new SignUpFlow();
        // 내 정보 form state (members only).
        public ProfileForm Profile { get; } = new ProfileForm();
        // Ranking tabs, the result best and the calorie panel; tests read what the ranking screen shows.
        public RankingBoard Ranking { get; } = new RankingBoard();
        public bool IdentityOpen=>Page==MenuPage.Identity;
        // Login gate, sign-up and face login keep the upper-body camera on for hand pointing and face capture.
        public bool AccountPageOpen=>Page==MenuPage.Login||Page==MenuPage.SignUp||Page==MenuPage.Identity;
        private IIdentityData Account=>Data as IIdentityData;
        // Text fields need the IME; the bootstrap switches it.
        public Action<bool> TextInput;
        // The UI Toolkit settings window draws over the game; the IMGUI HUD hides meanwhile.
        public bool Hidden { get; set; }
        public MenuPage Page { get; private set; }
        public bool MenuOpen => Page != MenuPage.None;
        public bool SetupOpen => Page == MenuPage.Setup;
        public bool Loading { get; private set; }
        public bool Revealing => revealRemaining > 0;
        public float RevealOpacity => Mathf.SmoothStep(0, 1, revealRemaining / .85f);
        public bool BlocksStart => MenuOpen || Loading || Revealing;
        public float LoadingProgress { get; set; }
        private float revealRemaining;
        private bool resultOpen;
        private HudSkin skin;
        private OverlaySkin overlaySkin;
        private TitleScreen title;
        private MainMenuScreen menu;
        private SetupScreen setup;
        private ProfileScreen profile;
        private ShopScreen shop;
        private LeaderboardScreen board;
        private CalibrationPanel calibration;
        private LoadingScreen loading;
        private RunHud runHud;
        private PauseScreen pause;
        public void Configure(RunSession run, Action again)
        {
            session = run; retry = again;
            skin = new HudSkin();
            overlaySkin = new OverlaySkin(Resources.Load<Font>("MuhanokUI/BlackHanSans-Regular"), null);
            title = new TitleScreen(skin); setup = new SetupScreen(skin); loading = new LoadingScreen(skin); identity=new IdentityScreen(skin); signUp=new SignUpScreen(skin,identity);
            menu = new MainMenuScreen(overlaySkin); tutorialOffer = new TutorialOfferScreen(overlaySkin); gate = new LoginGateScreen(overlaySkin); profile = new ProfileScreen(overlaySkin); shop = new ShopScreen(overlaySkin); board = new LeaderboardScreen(overlaySkin, Ranking);
            calibration = new CalibrationPanel(overlaySkin, skin);
            runHud = new RunHud(skin, new ProgressHud(overlaySkin)); pause = new PauseScreen(skin);
        }
        public void ShowPage(MenuPage page)
        {
            // 내 정보 is for members; a guest stays on the main menu.
            if (page == MenuPage.Profile && (Account == null || !Account.SignedIn)) page = MenuPage.Main;
            Page = page;
            if (page == MenuPage.Setup) setup.Open(Data);
            else if (page == MenuPage.Profile) { Profile.Open(Data, Account); profile.Open(); }
            else if (page == MenuPage.Shop) shop.Open();
            else if (page == MenuPage.Ranking) board.OpenBoard(Data, Account);
            else if (page == MenuPage.Login) Account?.CheckServer();
            else if (page == MenuPage.SignUp) { SignUp.Open(Account); signUp.Open(); }
            TextInput?.Invoke(page == MenuPage.Setup || page == MenuPage.Profile || page == MenuPage.SignUp);
        }
        public void OpenMainMenu() => ShowPage(MenuPage.Main);
        public void OpenLogin() => ShowPage(MenuPage.Login);
        // Face login capture screen (from the login gate).
        public void ShowIdentity() => ShowPage(MenuPage.Identity);
        public void OpenSetup() => ShowPage(MenuPage.Setup);
        public void ChooseGuest() => OpenMainMenu();
        // A recognized face goes to the main menu; a failure stays for another try.
        public void LoginWithFace() => Account?.FaceLogin(ok => { if (ok && Page == MenuPage.Identity) OpenMainMenu(); });
        // Sign-up capture: the new member is signed in and is offered the tutorial.
        public void EnrollFace() => SignUp.Capture(ok => { if (ok && Page == MenuPage.SignUp) { OfferTutorial?.Invoke(); ShowTutorialOffer(true); } });
        public void ShowTutorialOffer(bool afterSignUp) { offerAfterSignUp = afterSignUp; ShowPage(MenuPage.TutorialOffer); }
        // "튜토리얼 시작" runs it; skipping goes on to where the player was heading.
        public void ChooseTutorial(TutorialChoice choice)
        {
            if (choice == TutorialChoice.Start) StartTutorial?.Invoke();
            else if (choice == TutorialChoice.Skip) { if (offerAfterSignUp) OpenMainMenu(); else ShowPage(MenuPage.Setup); }
        }
        public void ChooseLogin(LoginAction action)
        {
            if (action == LoginAction.Face) ShowIdentity();
            else if (action == LoginAction.SignUp) ShowPage(MenuPage.SignUp);
            else if (action == LoginAction.Guest) ChooseGuest();
            else if (action == LoginAction.Settings) OpenSettings?.Invoke();
        }
        // Main menu actions. "게임 시작" goes to the run setup, after a one-time tutorial offer; the login entry signs a member out first.
        public void SelectMenu(MenuAction action)
        {
            if (action == MenuAction.Play) { if (OfferTutorial?.Invoke() ?? false) ShowTutorialOffer(false); else ShowPage(MenuPage.Setup); }
            else if (action == MenuAction.Tutorial) StartTutorial?.Invoke();
            else if (action == MenuAction.Ranking) ShowPage(MenuPage.Ranking);
            else if (action == MenuAction.Shop) ShowPage(MenuPage.Shop);
            else if (action == MenuAction.Profile) ShowPage(MenuPage.Profile);
            else if (action == MenuAction.Identity) Logout();
            else if (action == MenuAction.Settings) OpenSettings?.Invoke();
            else if (action == MenuAction.Quit) Quit?.Invoke();
        }
        // Main menu "로그아웃" and 내 정보: a member is signed out; both go to the login gate.
        public void Logout() { if (Account != null && Account.SignedIn) Account.FaceLogout(false); OpenLogin(); }
        // 내 정보 "회원 탈퇴": two questions, then the account is deleted and the login gate opens.
        public void DeleteAccount() => Profile.RequestDelete(() => { if (Page == MenuPage.Profile) OpenLogin(); });
        // Result screen: a guest is asked for a name for the run just recorded as "게스트" on this PC's ranking.
        public bool AsksGuestName => board.AsksGuestName;
        public bool SaveGuestName(string name) => board.SaveGuestName(Data, name);
        // Opens the result screen once per result (drawn by OnGUI; tests call it directly).
        public void OpenResult()
        {
            if (resultOpen || session == null || session.Flow.State != GameFlowState.Result) return;
            resultOpen = true; board.OpenResult(Data, Account, session); if (board.AsksGuestName) TextInput?.Invoke(true);
        }
        // Result screen 웹캠 보정: only after a camera run; the panel blocks the result buttons and hand pointing while open.
        public bool CalibrationAvailable => session != null && session.Flow.State == GameFlowState.Result && Data?.Player != null && PlayStyleInfo.UsesCamera(Data.Player.Style);
        public bool CalibrationOpen => calibration != null && calibration.Open;
        public void OpenCalibration() { if (CalibrationAvailable) calibration.Show(Data); }
        public void CloseCalibration() => calibration?.Close();
        public void CloseSetup() { ShowPage(MenuPage.None); Loading = false; revealRemaining = 0; }
        public void BeginLoading() { ShowPage(MenuPage.None); Loading = true; }
        public void EndLoading() { Loading = false; revealRemaining = .85f; }
        public RunHudModel RunModel => runHud?.Model;
        private void Update()
        {
            revealRemaining = Mathf.Max(0, revealRemaining - Time.unscaledDeltaTime);
            if (!CalibrationAvailable) calibration?.Close(); else calibration.Tick(Data, Time.realtimeSinceStartup);
            if (session == null || runHud == null || Hidden || Loading || Revealing) return;
            if (session.Flow.State == GameFlowState.Running || session.Flow.State == GameFlowState.Caught)
                runHud.Model.Tick(session, Map, Data?.Player?.Style ?? PlayStyle.Keyboard, Data?.PoseStatus ?? PoseStatus.Off, PoliceAlert?.Invoke() ?? false, Time.unscaledTime);
        }
        private void OnGUI()
        {
            if (session == null || Hidden) return;
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = ViewportLayout.GuiMatrix;
            GesturePointer.BeginPass();
            var state = session.Flow.State;
            if (state != GameFlowState.Result) resultOpen = false;
            if (Loading || Revealing) loading.Draw(LoadingImage, LoadingProgress, Loading, RevealOpacity);
            else if (state == GameFlowState.TitleIdle) DrawMenu();
            else if (state == GameFlowState.Running || state == GameFlowState.Caught)
            {
                if (session.Paused) pause.Draw(session, Data, PauseRun, OpenSettings, ToMenu);
                else runHud.Draw(session, Fade, Data, PauseRun);
            }
            else if (state == GameFlowState.Result)
            {
                OpenResult();
                bool perRun = Data != null && Data.DeveloperMode, calibrating = calibration.Open;
                // The open calibration panel takes the keys first and leaves the result screen underneath inert.
                calibration.Keys();
                GUI.enabled = !calibrating;
                board.Draw(Data, session, retry, "다시 하기", perRun ? NextPlayer : ToMenu, perRun ? "다음 플레이어" : "메인으로", CalibrationAvailable ? OpenCalibration : (Action)null, "웹캠 보정");
                GUI.enabled = true;
                if (calibrating) calibration.Draw(Data);
            }
            GesturePointer.Draw();
            GUI.color = Color.white; GUI.matrix = old;
        }
        private void DrawMenu()
        {
            // Esc is "뒤로" on the pages whose back button returns to the main menu (the profile handles its own keys).
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && (Page == MenuPage.Shop || Page == MenuPage.Ranking || Page == MenuPage.Setup))
            { e.Use(); OpenMainMenu(); return; }
            switch (Page)
            {
                case MenuPage.None: title.Draw(TitleLogo, Fade, OpenLogin); break;
                case MenuPage.Setup: setup.Draw(Data, BeginRun, OpenMainMenu, OpenSettings); break;
                case MenuPage.Profile:
                    if (Account == null || !Account.SignedIn) OpenMainMenu();
                    else profile.Draw(Profile, OpenMainMenu, Logout, DeleteAccount);
                    break;
                case MenuPage.Shop: shop.Draw(Data, OpenMainMenu); break;
                case MenuPage.Ranking: board.Draw(Data, session, OpenMainMenu, "뒤로", null, null); break;
                case MenuPage.Login: ChooseLogin(gate.Draw(Account, TitleLogo)); break;
                case MenuPage.SignUp: if (Account != null) signUp.Draw(Data, Account, SignUp, EnrollFace, OpenLogin); break;
                case MenuPage.Identity:
                    if (Account != null) identity.Draw(Data, Account, "얼굴 로그인", "정면을 보고 틀 안에 얼굴을 맞춘 뒤 [얼굴 로그인]을 누르세요.\n가입한 얼굴이면 이름 입력 없이 로그인됩니다.", "얼굴 로그인", LoginWithFace, OpenLogin);
                    break;
                case MenuPage.Main: SelectMenu(menu.Draw(Data, TitleLogo)); break;
                case MenuPage.TutorialOffer: ChooseTutorial(tutorialOffer.Draw(offerAfterSignUp)); break;
            }
        }
        private void OnDestroy() { skin?.Dispose(); overlaySkin?.Dispose(); }
    }
}
