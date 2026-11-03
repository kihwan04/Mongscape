using Muhanok.Application;
using System.Collections;
using Muhanok.Content;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using Muhanok.Presentation;
using Muhanok.Presentation.Map;
using UnityEngine;
using UnityEngine.Timeline;

namespace Muhanok.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour, IHudData, ISettingsHost, IIdentityData
    {
        public GameSettings Settings;
        public MuhanokMapChunk[] ChunkPrefabs;
        public GameObject MonkeyPrefab, PolicePrefab, CartPrefab, PickaxePrefab, IdeaPrefab, DustPrefab, HitPrefab;
        public FirstPersonCameraRig CameraRig;
        public Camera OutputCamera;
        public TimelineAsset IntroTimeline;
        public GameObject HandsPrefab;
        public Texture2D TitleLogo;
        public Texture2D LoadingImage;
        public GameObject CartRidePrefab;
        public bool StartSetupOpen=>hud!=null&&hud.SetupOpen;
        public bool IsLoading=>hud!=null&&hud.Loading;
        public bool IsRevealing=>hud!=null&&hud.Revealing;
        public RunSession Session { get; private set; }
        public ChunkSpawner Map { get; private set; }
        public IntroSequenceDirector Intro { get; private set; }
        public PoseTracker Pose { get; private set; }
        public PlayerProgressStore Store { get; private set; }
        public RunnerView Hero { get; private set; }
        private RunnerView guard;
        private KeyboardGameInput keyboard;
        private PoseGameInput poseInput;
        private InputRouter router;
        private HUDPresenter hud;
        private GameAudio audioPlayer;
        private float caughtTime, runFade, runStartedAt = -10, caughtAt = -10;
        private ParticleSystem dust, hit;
        private bool previousStumble;
        // The account (member or guest) is the only source of the current player.
        public AccountSession Account { get; } = new AccountSession();
        // Run settings of the current or next run, taken from the account; developer mode names each run instead.
        // Runs started from the setup screen are recorded on the leaderboard.
        private PlayerProfile player = new PlayerProfile();
        private IAccountGateway gateway;
        private AccountRunUploader runs;
        private Texture2D portrait;
        private string shownAccount = "";
        // Account server state for the login gate, and the face capture (the webcam, or a fake in tests).
        private string serverProblem = "";
        private int serverTicket;
        private bool serverChecking;
        private System.Action<System.Action<string[]>> captureFaces;
        // A member call answered 401: that token expired on the server. The member is signed out here (no /logout) and the
        // login gate opens with the notice; during a run (and its result) this waits until the menu is back.
        public const string ExpiredNotice = "로그인이 만료되었어요. 다시 로그인해 주세요.";
        private string expiredToken, loginNotice = "";
        private bool playerRun, bannerSounded, goSounded, lastNewBest;
        private RankingEntry lastEntry;
        private int lastBestScore = -1;
        // The member's /stats (best score per play style) and the totals read after the last run was saved.
        private StatsReply memberStats, lastStats;
        private string memberStatsId = "";
        private bool lastStatsFailed;
        private int lastTodayRank;
        private UserSettings userSettings, settingsDefaults;
        private GraphicsSettingsApplier graphics;
        private SettingsWindow settingsWindow;
        private PerformanceOverlay overlay;
        private int lastRank;
        private LocalProfileClient identity;
        private FaceAvatarView faceAvatar;
        public PoliceCaptureSequence Capture { get; private set; }
        public PolicePursuitCue PursuitCue { get; private set; }
        private float guardDistance=15;
        private bool automated;
        private readonly AdaptivePerformance adaptive = new AdaptivePerformance();
        private UserSettings effectiveGraphics;
        private bool menuCamera;
        private PlayStyle cameraBeforeMenu;
        // Tutorial lessons on the ordinary run; offered once per session on "게임 시작".
        public TutorialDirector Tutorial { get; private set; }
        private bool tutorialOffered;
        private void Awake()
        {
            var flags=System.Environment.GetCommandLineArgs();
            automated=System.Array.IndexOf(flags,"--muhanok-smoke")>=0||System.Array.IndexOf(flags,"--muhanok-uicapture")>=0
                ||System.Array.IndexOf(flags,"--mongscape-review")>=0||System.Array.IndexOf(flags,"--mongscape-tests")>=0
                ||System.Array.IndexOf(flags,"--muhanok-capture")>=0||System.Array.IndexOf(flags,"--mongscape-recording")>=0;
            Session = new RunSession(Settings != null ? JsonUtility.FromJson<RunConfig>(JsonUtility.ToJson(Settings.Run)) : new RunConfig());
            if (ChunkPrefabs == null || ChunkPrefabs.Length < 5 || CameraRig == null || OutputCamera == null)
            { Debug.LogError("GameScene requires content references. Run Muhanok/Build GameScene."); enabled = false; return; }
            keyboard = new KeyboardGameInput(); poseInput = new PoseGameInput(); router = new InputRouter(Session, keyboard, poseInput);
            Pose = new PoseTracker(poseInput, Settings != null ? Settings.Camera : null, Settings != null ? Settings.Motion : null, () => Session.Runner.Lane, () => Session.Runner.Riding&&!(RouteSurface.ActivePlan?.ForkWindow(Session.Runner.Z)??false));
            Store = new PlayerProgressStore(DataRoot());
            player = Account.Profile.Clone();
            Hero = CreateActor("CHR_Monkey_Prisoner", MonkeyPrefab);
            guard = CreateActor("CHR_Police_Officer", PolicePrefab);
            Capture=gameObject.AddComponent<PoliceCaptureSequence>();Capture.Configure(Hero,guard);
            PursuitCue=gameObject.AddComponent<PolicePursuitCue>();PursuitCue.Configure(Session);
            identity=gameObject.AddComponent<LocalProfileClient>(); faceAvatar=Hero.gameObject.AddComponent<FaceAvatarView>();
            gateway=new ExpiryWatchingGateway(identity,SessionExpired); runs=new AccountRunUploader(Account,gateway);
            captureFaces=done=>identity.CaptureFaces(Pose.CameraTexture,done);
            Account.Changed+=AccountChanged;
            gameObject.AddComponent<CartRideView>().Configure(Session,Hero,CartRidePrefab);
            gameObject.AddComponent<PowerUpAuraView>().Configure(Session,Hero);
            OutputCamera.GetComponent<CinematicFocus>()?.Configure(Session,Hero);
            var mapRoot = new GameObject("ChunkPool"); mapRoot.transform.SetParent(transform, false);
            Map = new ChunkSpawner(ChunkPrefabs, mapRoot.transform, Session, Settings != null ? Settings.Render : new RenderBudget());
            Map.Decorator = new TutorialLayout(new RuntimeContent(mapRoot.transform, RunItems, Settings != null ? Settings.ItemChancePerChunk : .35f), Session);
            CameraRig.Configure(OutputCamera);
            if(OutputCamera.GetComponent<GameViewport>()==null) OutputCamera.gameObject.AddComponent<GameViewport>();
            if (HandsPrefab != null)
            {
                var hands = Instantiate(HandsPrefab, OutputCamera.transform);
                hands.GetComponent<FirstPersonHands>().Configure(Session);
            }
            var cart = CreateProp("PROP_MineCart", CartPrefab, transform);
            var tool = CreateProp("PROP_Pickaxe", PickaxePrefab, Hero.ToolAnchor);
            tool.transform.localPosition = Hero.Humanoid ? Vector3.zero : new Vector3(.05f, -.35f, .35f);
            if(Hero.Humanoid) { tool.transform.localRotation=Quaternion.Euler(0,0,-90); tool.transform.localScale=Vector3.one*.75f; }
            var bulb = CreateProp("VFX_LightBulb_Idea", IdeaPrefab, Hero.transform); bulb.transform.localPosition = new Vector3(0, 2.4f, 0);
            if(Hero.Humanoid) bulb.transform.localScale=Vector3.one*.65f;
            dust = CreateProp("VFX_Dust_Run", DustPrefab, Hero.transform).GetComponentInChildren<ParticleSystem>();
            hit = CreateProp("VFX_Hit_Stumble", HitPrefab, Hero.transform).GetComponentInChildren<ParticleSystem>();
            var cartAudio = cart.GetComponent<AudioSource>();
            Intro = gameObject.AddComponent<IntroSequenceDirector>();
            Intro.Configure(Session, Hero, guard, CameraRig, cart.transform, tool.transform, bulb, cartAudio, IntroTimeline);
            hud = gameObject.AddComponent<HUDPresenter>();
            hud.Map=Map; hud.Data=this; hud.PoliceAlert=()=>PursuitCue.Visible;
            hud.Configure(Session, RetrySamePlayer);
            hud.TitleLogo = TitleLogo;
            hud.LoadingImage=LoadingImage; hud.BeginRun=StartPlayerRun; hud.PauseRun=TogglePause; hud.NextPlayer=NextPlayer; hud.ToMenu=ExitToMenu;
            hud.TextInput=KeyboardGameInput.SetTextInput; hud.Quit=QuitGame;
            hud.StartTutorial=StartTutorial; hud.OfferTutorial=OfferTutorial;
            Tutorial=gameObject.AddComponent<TutorialDirector>();
            Tutorial.Configure(Session,Map,()=>Pose.Status==PoseStatus.Tracking||Pose.Status==PoseStatus.Off||Pose.Status==PoseStatus.NoCamera||Pose.Status==PoseStatus.NoModel,
                RestartTutorial,FinishTutorial,()=>!hud.Hidden&&!hud.Loading&&!hud.Revealing);
            audioPlayer = gameObject.AddComponent<GameAudio>();
            audioPlayer.Configure(Session, Settings != null ? Settings.MusicVolume : .35f, Settings != null ? Settings.EffectsVolume : .7f);
            SetupSettings();
            if (userSettings.DeveloperMode) player = new PlayerProfile { Style = Account.Settings.Style };
            if(automated)player.Style=PlayStyle.Keyboard;
            Session.Flow.Changed += StateChanged;
            Session.Flow.Transition(GameFlowState.TitleIdle);
        }
        private void SetupSettings()
        {
            settingsDefaults = new UserSettings
            {
                MusicVolume = Settings != null ? Settings.MusicVolume : .35f, EffectsVolume = Settings != null ? Settings.EffectsVolume : .7f,
                LiteLandmarkModel = Settings != null && !Settings.Camera.FullLandmarkModel, CameraDevice = Settings != null ? Settings.Camera.DeviceName : "",
            };
            settingsDefaults.ApplyPreset(QualityPreset.Balanced);
            userSettings = UserSettingsStore.Load(settingsDefaults);
            graphics = new GraphicsSettingsApplier();
            overlay = gameObject.AddComponent<PerformanceOverlay>();
            overlay.PoseLine = () => Pose.UsesCamera ? Pose.Diagnostics : "";
            overlay.QualityLine = () => "화질 " + userSettings.Preset + " · 렌더 " + Mathf.RoundToInt((effectiveGraphics?.RenderScale ?? userSettings.RenderScale) * 100) + "% · 그림자 " + (effectiveGraphics?.Shadows ?? userSettings.Shadows) + (userSettings.AutoPerformance ? " · 자동 " + adaptive.Level : "") + (userSettings.FrameCap > 0 ? " · 상한 " + userSettings.FrameCap : "");
            var window = new GameObject("SettingsWindow"); window.transform.SetParent(transform, false);
            settingsWindow = window.AddComponent<SettingsWindow>();
            settingsWindow.Configure(this);
            settingsWindow.Closed += () => hud.Hidden = false;
            hud.OpenSettings = OpenSettings;
            ApplySettings(false);
        }
        public void OpenSettings()
        {
            if (settingsWindow == null || settingsWindow.IsOpen || hud.Loading) return;
            if (Session.Flow.State == GameFlowState.Running && !Session.Paused) TogglePause();
            hud.Hidden = true; settingsWindow.Open();
        }
        public void ApplySettings(bool save)
        {
            adaptive.Reset();
            effectiveGraphics=userSettings.Clone();
            graphics?.Apply(effectiveGraphics);
            if (Map != null) Map.DetailScale = userSettings.DetailDistance;
            audioPlayer?.SetVolumes(userSettings.MusicVolume, userSettings.EffectsVolume);
            Pose.Configure(userSettings.CameraDevice, userSettings.LiteLandmarkModel, userSettings.LaneSensitivity);
            Pose.SetInferenceRate(30);
            if (overlay != null) overlay.Visible = userSettings.PerformanceOverlay;
            if (hud != null && hud.RunModel != null) hud.RunModel.CameraEnabled = userSettings.InGameCamera;
            if (save) UserSettingsStore.Save(userSettings);
        }
        private RunnerView CreateActor(string id, GameObject prefab)
        {
            var root = new GameObject(id); root.transform.SetParent(transform, false);
            var visual = CreateProp("VisualRoot", prefab, root.transform);
            var view = root.AddComponent<RunnerView>(); view.Configure(visual); return view;
        }
        private static GameObject CreateProp(string id, GameObject prefab, Transform parent)
        {
            GameObject result = prefab != null ? Instantiate(prefab, parent) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            result.name = id; result.transform.SetParent(parent, false);
            if (prefab == null) { var collider = result.GetComponent<Collider>(); if (collider != null) collider.enabled = false; }
            return result;
        }
        public void StartIntro()
        {
            if (Session.Flow.Transition(GameFlowState.IntroTimeline)) Intro.Begin();
        }
        public bool MenuOpen=>hud!=null&&hud.MenuOpen;
        public void OpenMainMenu()
        {
            if(Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading) return;
            hud.OpenMainMenu();
        }
        // After the title: face login, sign-up or guest play.
        public void OpenLogin()
        {
            if(Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading) return;
            loginNotice=""; hud.OpenLogin();
        }
        // Straight to the run setup (tests, "다음 플레이어").
        public void OpenStartSetup()
        {
            if(Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading) return;
            hud.OpenSetup();
        }
        // Direct start (tests, tools): keeps the current play style and does not enter the leaderboard.
        public void BeginRun(string weight)
        {
            if(Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading||!Session.Workout.SetWeight(weight)) return;
            menuCamera=false;
            playerRun=false; Session.Workout.Met=PlayStyleInfo.Met(player.Style);
            StartCoroutine(PrepareRun(723));
        }
        // Start from the setup screen: the run is recorded on the leaderboard. Outside developer mode
        // the chosen play style becomes the account's default.
        public void StartPlayerRun(PlayerProfile profile)
        {
            if(profile==null||Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading) return;
            // A weight outside 10-350 kg (an old profile) never blocks the run: it counts as the 65 kg default.
            if(!Session.Workout.SetWeight(profile.Weight)) Session.Workout.SetWeight("");
            if(!userSettings.DeveloperMode&&profile.Style!=Account.Settings.Style){var saved=Account.Settings;saved.Style=profile.Style;SaveSettings(saved);}
            player=profile; playerRun=true; KeyboardGameInput.SetTextInput(false);
            // The best to beat is read before the run (and before its save).
            RefreshMemberStats();
            Session.Workout.Met=PlayStyleInfo.Met(profile.Style);
            Session.Workout.SetGoal(profile.CalorieGoal);
            menuCamera=false; Pose.SetStyle(profile.Style);
            StartCoroutine(PrepareRun(Settings != null && Settings.Run.RandomizeLayout ? Random.Range(1, int.MaxValue) : 723));
        }
        // Tutorial (main menu, after sign-up or the "게임 시작" offer): the same scene and map on a fixed lesson course.
        // It never reaches the result screen, so nothing goes to the ranking, coins or calories.
        public void StartTutorial()
        {
            if(Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading) return;
            tutorialOffered=true; playerRun=false; KeyboardGameInput.SetTextInput(false);
            Session.Workout.SetWeight(""); Session.Workout.Met=PlayStyleInfo.Met(player.Style);
            menuCamera=false; Pose.SetStyle(player.Style);
            Session.IsTutorial=true; Tutorial.Begin(player.Style,Session.Config.ExtendedRoutes);
            StartCoroutine(PrepareRun(TutorialCourse.Seed));
        }
        // Members who have not finished it and every guest are offered it once per session.
        private bool OfferTutorial()
        {
            if(!TutorialScript.ShouldOffer(Account.IsMember,Account.TutorialDone,tutorialOffered)) return false;
            tutorialOffered=true; return true;
        }
        // After a mistake: a fresh course, started at the given route z in front of the step.
        private void RestartTutorial(float z)
        {
            RouteSurface.UsePlan(TutorialCourse.Plan(Session.Config));
            Session.Reset(); Session.Runner.SetStartZ(z); keyboard.Clear(); poseInput.Clear(); Pose.ResetCommands();
            Map.Reset(); Map.Tick(z); Map.AnimateGates(z);
            guardDistance=15; previousStumble=false; Hero.Apply(Session.Runner); CameraRig.Takeover(Hero.Head);
        }
        // A member's profile remembers the finished tutorial; then back to the main menu.
        private void FinishTutorial()
        {
            if(Account.IsMember)
            {
                string id=Account.Id; Account.SetTutorialDone(true);
                gateway.UpdateProfile(Account.Token,new AccountProfileUpdate{TutorialDone=true},reply=>{ if(reply!=null&&reply.HasProfile&&reply.profile.id==id&&reply.profile.tutorial_done) Account.Apply(reply.profile); });
            }
            ExitToMenu();
        }
        private IEnumerator PrepareRun(int seed)
        {
            Session.Flow.Transition(GameFlowState.LoadingIntro);
            hud.BeginLoading(); hud.LoadingProgress=.12f;
            // The player stands still during loading; that is the calibration pose. A fresh result-screen calibration is kept.
            if (!Pose.CalibrationFresh(Time.realtimeSinceStartupAsDouble)) Pose.Recalibrate();
            yield return new WaitForSecondsRealtime(.25f);
            RouteSurface.UsePlan(Session.IsTutorial?TutorialCourse.Plan(Session.Config):new RoutePlan(Session.Config, seed));
            Session.Reset(); keyboard.Clear(); poseInput.Clear(); hud.LoadingProgress=.42f;
            yield return new WaitForSecondsRealtime(.25f);
            Map.Reset(seed); Map.AnimateGates(Session.Runner.Z); hud.LoadingProgress=.78f;
            yield return new WaitForSecondsRealtime(.25f);
            Intro.PrepareGameplay(); hud.LoadingProgress=1;
            yield return new WaitForSecondsRealtime(.25f);
            Session.Flow.Transition(GameFlowState.Running); hud.EndLoading();
        }
        public void TogglePause()
        {
            if(Session.Flow.State!=GameFlowState.Running) return;
            Session.Paused=!Session.Paused; Time.timeScale=Session.Paused?0:1;
            keyboard.Clear(); poseInput.Clear();
        }
        private void UpdateAdaptive(bool live)
        {
            if(automated || !userSettings.AutoPerformance)return;
            if(!adaptive.Tick(Time.unscaledDeltaTime,live && !hud.Revealing && UnityEngine.Application.isFocused,userSettings.FrameCap))return;
            adaptive.Apply(userSettings,effectiveGraphics);graphics.Apply(effectiveGraphics);
            Map.DetailScale=effectiveGraphics.DetailDistance;
            // Rate changes reuse the loaded model and do not restart the camera or invalidate calibration.
            Pose.SetInferenceRate(adaptive.Level>=2?20:30);
        }
        private void UpdateMenuCamera(bool menu)
        {
            bool accountCamera=Session.Flow.State==GameFlowState.TitleIdle&&hud.AccountPageOpen;
            // Keyboard players keep the webcam off in menus; the login gate, sign-up and face capture always use it.
            bool handCamera=userSettings.HandMenus&&PlayStyleInfo.UsesCamera(player.Style);
            GesturePointer.Enabled=userSettings.HandMenus&&(handCamera||accountCamera);
            bool needsCamera=!automated && menu && (handCamera||accountCamera);
            if(needsCamera && (!Pose.UsesCamera||accountCamera&&Pose.Style!=PlayStyle.UpperBody))
            {if(!menuCamera)cameraBeforeMenu=Pose.Style;menuCamera=true;Pose.SetStyle(PlayStyle.UpperBody);}
            else if(menuCamera && !needsCamera){menuCamera=false;Pose.SetStyle(cameraBeforeMenu);}
        }
        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            bool live = Session.Flow.State == GameFlowState.Running && !Session.Paused && !hud.Loading;
            bool menu=(Session.Flow.State==GameFlowState.TitleIdle || Session.Flow.State==GameFlowState.Result || Session.Paused || settingsWindow.IsOpen) && !hud.Loading && !hud.Revealing;
            UpdateMenuCamera(menu);
            UpdateAdaptive(live);
            Pose.Tick(now, live);
            // The calibration panel takes no hand pointing: the player stands still and it closes by itself.
            GesturePointer.Feed(Pose.LastFrame,menu&&GesturePointer.Enabled&&Pose.Tracking&&!hud.CalibrationOpen,now);
            OperatorKeys();
            if (ExpiryPending && Session.Flow.State == GameFlowState.TitleIdle && !hud.Loading) ApplyExpiry();
            TickInput();
            if (Session.Flow.State == GameFlowState.Caught && Time.timeScale < 1 && Time.realtimeSinceStartup - caughtAt > .7f) Time.timeScale = 1;
            if(Session.Paused||hud.Loading||hud.Revealing) return;
            float dt = Mathf.Min(Time.deltaTime, .1f);
            Intro.Tick(dt);
            if (Session.Flow.State == GameFlowState.IntroTimeline) hud.Fade = Mathf.Clamp01(1 - Intro.Elapsed * 3f);
            if (Session.Flow.State == GameFlowState.Running)
            {
                StartBannerSounds();
                Tutorial.Tick(Time.realtimeSinceStartup);
                // The finished tutorial has already gone back to the menu (Retry); nothing of the run may follow this frame.
                if (Session.Flow.State != GameFlowState.Running) return;
                float previousZ = Session.Runner.Z;
                Session.Tick(dt); Map.Tick(Session.Runner.Z); Hero.Apply(Session.Runner); Map.Evaluate(previousZ);
                Map.AnimateGates(Session.Runner.Z);
                bool exercising = Pose.UsesCamera ? Pose.Tracking && Pose.Reading.Active : true;
                int activity=Time.realtimeSinceStartupAsDouble-Pose.LastJumpTime<.65?1:Pose.Reading.Crouch?2:Pose.Reading.HighKnee?3:Pose.Reading.Lane!=0?4:0;
                Session.Workout.TickActivity(dt,Session.Flow.State==GameFlowState.Running,Pose.UsesCamera&&Pose.Tracking,exercising,activity);
                runFade += dt; hud.Fade = Mathf.Clamp01(runFade * 2f);
                if (dust != null && !dust.isPlaying) dust.Play();
                if (Session.Runner.Stumbling && !previousStumble && hit != null) hit.Play();
                previousStumble = Session.Runner.Stumbling;
                if (Session.Flow.State == GameFlowState.Running)
                {
                    float distance=Session.Runner.Slowing&&Session.BananaHits>0||Session.BananaPursuit?1.8f:Mathf.Lerp(15,1.8f,Session.Chase.Pressure);
                    guardDistance=Mathf.MoveTowards(guardDistance,distance,dt*18);
                    guard.Pose(new Vector3(Session.Runner.X, 0, Session.Runner.Z-guardDistance), !Session.Runner.Riding);
                    guard.Play("AN_Guard_ChaseRun");
                }
            }
            if (Session.Flow.State == GameFlowState.Caught)
            {
                caughtTime += Time.unscaledDeltaTime;
                if(Session.Reason==CaptureReason.BrokenRail)
                {
                    guard.SetVisible(false);
                    if(caughtTime>=1.6f)Session.Flow.Transition(GameFlowState.Result);
                }
                else
                {
                    Capture.Tick(Time.unscaledDeltaTime);
                    if(Capture.Complete)Session.Flow.Transition(GameFlowState.Result);
                }
            }
        }
        private void StartBannerSounds()
        {
            float banner = StartBanner;
            if (banner <= 0) return;
            if (!bannerSounded) { bannerSounded = true; audioPlayer.Countdown(false); }
            if (!goSounded && banner <= 1f && Pose.Status != PoseStatus.Calibrating) { goSounded = true; audioPlayer.Countdown(true); }
        }
        private void OperatorKeys()
        {
            if (KeyboardGameInput.SettingsPressed) { if (settingsWindow != null && settingsWindow.IsOpen) settingsWindow.Close(true); else OpenSettings(); }
            if (KeyboardGameInput.OverlayPressed) { userSettings.PerformanceOverlay = !userSettings.PerformanceOverlay; ApplySettings(true); }
            if (settingsWindow != null && settingsWindow.IsOpen) return;
            if (KeyboardGameInput.RecalibratePressed && !hud.MenuOpen) Pose.Recalibrate();
            if (KeyboardGameInput.DeveloperTogglePressed && Session.Flow.State == GameFlowState.TitleIdle) SetDeveloperMode(!userSettings.DeveloperMode);
            if (KeyboardGameInput.ClearRankingPressed && userSettings.DeveloperMode) { Store.ClearToday(); Debug.Log("Today's ranking cleared."); }
        }
        internal HUDPresenter Hud => hud;
        // Development builds can keep player data in another folder (--muhanok-data <dir>) for captures and tests.
        private static string DataRoot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args = System.Environment.GetCommandLineArgs(); int at = System.Array.IndexOf(args, "--muhanok-data");
            if (at >= 0 && at + 1 < args.Length) return args[at + 1];
#endif
            return null;
        }
        // Developer (exhibition) mode: per-run names and every item unlocked. Switching it starts from a clean player.
        public void SetDeveloperMode(bool on, bool save = true)
        {
            userSettings.DeveloperMode = on;
            if (save) UserSettingsStore.Save(userSettings);
            player = on ? new PlayerProfile { Style = Account.Settings.Style } : Account.Profile.Clone();
            if(on&&Account.IsMember)player.Name=Account.Nickname;
            if (hud.MenuOpen && !hud.AccountPageOpen) hud.OpenMainMenu();
            Debug.Log("Developer mode " + (on ? "on" : "off"));
        }
        public void TickInput()
        {
            keyboard.Poll();
            if(hud.Loading||hud.Revealing||(settingsWindow!=null&&settingsWindow.IsOpen)){keyboard.Clear();poseInput.Clear();return;}
            router.Tick();
            if (Session.Flow.State == GameFlowState.TitleIdle && !hud.BlocksStart && KeyboardGameInput.StartPressed) OpenLogin();
        }
        private void LateUpdate()
        {
            if (Session != null && (Session.Flow.State == GameFlowState.Running || Session.Flow.State == GameFlowState.Caught))
            {
                if(Session.Flow.State==GameFlowState.Caught&&Session.Reason!=CaptureReason.BrokenRail)CameraRig.FollowCapture(Hero.transform.position,Time.unscaledDeltaTime,Capture.ProneBlend);
                else if(Session.Runner.Riding)CameraRig.FollowCart(Hero.Head,Time.deltaTime,-Session.Runner.CartLean*12);
                else CameraRig.Follow(Hero.Head,Session.Runner.Crouching,Time.deltaTime,Session.Runner.Slowing?Mathf.Sin(Time.time*12)*2:0);
            }
        }
        private void StateChanged(GameFlowState state)
        {
            if (state == GameFlowState.Running) { runFade = 0; poseInput.Clear(); Pose.ResetCommands(); runStartedAt = Time.realtimeSinceStartup; bannerSounded = goSounded = false; }
            if (state == GameFlowState.Caught)
            {
                caughtTime = 0; guard.Play("AN_Guard_Catch"); Hero.Play("AN_Monkey_Caught");
                if(Session.Reason!=CaptureReason.BrokenRail)
                {
                    Capture.Begin();
                }
                if (dust != null) dust.Stop();
                // A short slow motion and red flash make the capture readable.
                caughtAt = Time.realtimeSinceStartup; Time.timeScale = .35f;
            }
            if (state == GameFlowState.Result)
            {
                Time.timeScale = 1;
                var args=System.Environment.GetCommandLineArgs();
                bool tooling=System.Array.IndexOf(args,"--muhanok-capture")>=0||System.Array.IndexOf(args,"--muhanok-smoke")>=0
                    ||System.Array.IndexOf(args,"--mongscape-review")>=0||System.Array.IndexOf(args,"--mongscape-tests")>=0
                    ||System.Array.IndexOf(args,"--muhanok-uicapture")>=0;
                lastEntry=null; lastRank=lastTodayRank=0; lastBestScore=-1; lastNewBest=false; lastStats=null; lastStatsFailed=false;
                if(!tooling&&playerRun) RecordFinishedRun();
            }
        }
        // The run that just ended goes on this PC's ranking; a member's run is also saved on the account server,
        // which credits the coins (none in developer mode). Guests collect no coins.
        public void RecordFinishedRun()
        {
            if(Session.IsTutorial) return;
            bool developer=userSettings.DeveloperMode;
            var now=System.DateTime.Now;
            string name=Account.IsMember||!developer?Account.Nickname
                :player.HasName?player.Name.Trim():"플레이어 "+(Store.Board.Day(Leaderboard.DayKey(now)).Count+1);
            var mode=player.Style;
            lastEntry=new RankingEntry{Name=name,Score=Session.Score.Score,Distance=Session.Score.Distance,Coins=Session.Score.Coins,
                Style=PlayStyleInfo.Label(mode),Mode=(int)mode,Kcal=PlayStyleInfo.UsesCamera(mode)?Session.Workout.EstimatedKcal:0,
                Ticks=now.Ticks,AccountId=Account.IsMember?Account.Id:"",ScoreVersion=RunScore.Version};
            lastStats=null; lastStatsFailed=false;
            // Personal best per play style: a member's from /stats read before this save; an exhibition run's from this PC by its name; none for guests.
            int previousBest=Account.IsMember?MemberBest(mode):developer?Store.Board.BestScore(lastEntry.Style,name):-1;
            lastNewBest=previousBest>=0&&lastEntry.Score>previousBest; lastBestScore=previousBest>0?previousBest:-1;
            lastRank=Store.Record(lastEntry,Session.Workout.EstimatedKcal);
            lastTodayRank=Store.Board.RankOf(lastEntry,true,mode);
            if(!Account.IsMember) return;
            // The result screen shows the member's totals once the server has this run.
            string id=Account.Id;
            runs.Save(AccountRun.Create(Session,mode,developer?0:Session.Score.Coins),saved=>
            {
                if(!saved){if(Account.Id==id)lastStatsFailed=true;return;}
                RefreshMemberStats(reply=>{if(Account.Id!=id)return;lastStats=reply!=null&&reply.ok?reply:null;lastStatsFailed=lastStats==null;});
            });
        }
        // The member's best score for one play style from the last /stats; -1 when it is not known.
        private int MemberBest(PlayStyle mode)
        {
            var stats=memberStats;
            return Account.IsMember&&stats!=null&&memberStatsId==Account.Id&&stats.best_score!=null&&stats.best_score.Length>(int)mode?stats.best_score[(int)mode]:-1;
        }
        // Reads /stats for the signed-in member and keeps it for the HUD best and the next result.
        private void RefreshMemberStats(System.Action<StatsReply> done=null)
        {
            if(!Account.IsMember||string.IsNullOrEmpty(Account.Token)){done?.Invoke(null);return;}
            string id=Account.Id;
            gateway.Stats(Account.Token,reply=>
            {
                if(reply!=null&&reply.ok&&Account.IsMember&&Account.Id==id){memberStats=reply;memberStatsId=id;}
                done?.Invoke(reply);
            });
        }
        public void Retry()
        {
            if (!Session.Flow.Transition(GameFlowState.Retry)) return;
            Capture.ResetSequence();
            guardDistance=15; Session.IsTutorial=false; Tutorial.End();
            Session.Reset(); keyboard.Clear(); poseInput.Clear(); Map.Reset(); Intro.ResetSequence();
            Time.timeScale=1; hud.CloseSetup();
            if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (hit != null) hit.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            previousStumble = false; runFade = caughtTime = 0; hud.Fade = 1;
            Session.Flow.Transition(GameFlowState.TitleIdle);
        }
        // "다시 하기": the same player runs again with the same settings.
        public void RetrySamePlayer()
        {
            bool again = playerRun; var profile = player;
            Retry();
            // An expired member goes to the login gate (next frame) instead of another run.
            if (again && !ExpiryPending) StartPlayerRun(profile);
        }
        // "다음 플레이어" (developer mode): the member is signed out and the next person starts at the login gate.
        public void NextPlayer()
        {
            var style = player.Style;
            Retry();
            SignOutMember(); tutorialOffered=false;
            player = new PlayerProfile { Style = style == PlayStyle.Keyboard ? PlayStyle.FullBody : style };
            playerRun = false; OpenLogin();
        }
        // "메인으로" from the result or pause screen; a paused run ends without a record.
        public void ExitToMenu()
        {
            if (Session.Paused) { Session.Paused = false; Time.timeScale = 1; }
            playerRun = false;
            Retry();
            if (!userSettings.DeveloperMode) player = Account.Profile.Clone();
            OpenMainMenu();
        }
        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
        // Members are signed out when the game closes; the request is not awaited.
        private void OnApplicationQuit() { if(Account.IsMember&&!string.IsNullOrEmpty(Account.Token)) gateway?.Logout(Account.Token,null); }
        private void OnDestroy() { GesturePointer.Reset(); Time.timeScale=1; graphics?.Dispose(); Pose?.Dispose(); RouteSurface.UsePlan(null); KeyboardGameInput.SetTextInput(false); ClearPortrait(); }

        // Tests and tools: another account server (or a fake), another PC store and a face capture without a webcam. Call on the title screen.
        public void UseAccountServices(IAccountGateway accountGateway, PlayerProgressStore store = null, System.Action<System.Action<string[]>> faces = null)
        {
            if (accountGateway != null) { gateway = new ExpiryWatchingGateway(accountGateway, SessionExpired); runs.Gateway = gateway; serverTicket++; serverChecking = false; serverProblem = ""; }
            if (store != null) Store = store;
            if (faces != null) captureFaces = faces;
        }
        // Items placed on the map: every item in developer mode, otherwise what the member unlocked (none for guests).
        public System.Collections.Generic.List<ItemKind> RunItems()
        {
            if (Session != null && Session.IsTutorial) return new System.Collections.Generic.List<ItemKind>();
            if (userSettings != null && userSettings.DeveloperMode)
            {
                var all = new System.Collections.Generic.List<ItemKind>();
                foreach (var item in ItemCatalog.All) all.Add(item.Kind);
                return all;
            }
            return Account.RunItems();
        }
        // Guests keep settings in session memory; a member's settings also go to the server.
        private void SaveSettings(AccountSettings settings)
        {
            Account.UpdateSettings(settings);
            if (!Account.IsMember) return;
            string id = Account.Id;
            gateway.UpdateProfile(Account.Token, AccountProfileUpdate.From(Account.Settings), reply => { if (reply.HasProfile && reply.profile.id == id) Account.Apply(reply.profile); });
        }

        // IHudData
        PlayerProfile IHudData.Player => player;
        PlayerProfile IHudData.SavedProfile => Account.Profile;
        bool IHudData.DeveloperMode => userSettings != null && userSettings.DeveloperMode;
        Texture IHudData.CameraTexture => Pose.CameraTexture;
        PoseFrame IHudData.PoseFrame => Pose.LastFrame;
        PoseStatus IHudData.PoseStatus => Pose.Status;
        string IHudData.PoseMessage => Pose.Message;
        string IHudData.PoseDiagnostics => userSettings != null && userSettings.PoseDiagnostics ? Pose.Diagnostics : "";
        MotionReading IHudData.Motion => Pose.Reading;
        float IHudData.SecondsSinceJump => (float)(Time.realtimeSinceStartupAsDouble - Pose.LastJumpTime);
        float IHudData.CalibrationProgress => Pose.CalibrationProgress;
        float IHudData.StepsPerMinute => Pose.StepsPerMinute;
        bool IHudData.BodyActive => Pose.Tracking && Pose.Reading.Active;
        // Developer mode keeps this PC's exhibition wallet with every item open; players see their account.
        PlayerWallet IHudData.Wallet { get { if (userSettings == null || !userSettings.DeveloperMode) return Account.Wallet; Store.Wallet.UnlockAll = true; return Store.Wallet; } }
        Leaderboard IHudData.Board => Store.Board;
        PlayerStats IHudData.Stats => Store.Stats;
        RankingEntry IHudData.LastEntry => lastEntry;
        // A guest's run (not in developer mode) was recorded as "게스트" and can still be named on the result screen.
        bool IHudData.CanNameLastRun => lastEntry != null && !Account.IsMember && string.IsNullOrEmpty(lastEntry.AccountId) && userSettings != null && !userSettings.DeveloperMode;
        bool IHudData.NameLastRun(string name) => ((IHudData)this).CanNameLastRun && AccountSession.NicknameProblem(name) == null && Store.Rename(lastEntry, name);
        int IHudData.LastRank => lastRank;
        int IHudData.LastTodayRank => lastTodayRank;
        bool IHudData.LastNewBest => lastNewBest;
        int IHudData.LastBestScore => lastBestScore;
        StatsReply IHudData.LastStats => lastStats;
        bool IHudData.LastStatsFailed => lastStatsFailed;
        int IHudData.BestScore => MemberBest(player.Style);
        bool IHudData.RunSaveFailed => runs != null && runs.LastFailed;
        public float StartBanner => Session.Flow.State == GameFlowState.Running ? Mathf.Max(0, 2.2f - (Time.realtimeSinceStartup - runStartedAt)) : 0;
        float IHudData.CaughtFlash => Session.Flow.State == GameFlowState.Caught ? Mathf.Clamp01(1 - (Time.realtimeSinceStartup - caughtAt) / .6f) : 0;
        void IHudData.PreviewStyle(PlayStyle style) { menuCamera=false; player.Style = style; Pose.SetStyle(style); }
        // Members buy on the server and take its balance and unlocked items; guests have no coins and developer mode has everything.
        void IHudData.Unlock(ItemKind kind, System.Action<bool, string> done)
        {
            if (!Account.IsMember || userSettings.DeveloperMode) { done?.Invoke(false, "회원가입하면 코인을 모아 아이템을 해금할 수 있어요"); return; }
            string id = Account.Id;
            gateway.Buy(Account.Token, kind, reply =>
            {
                if (reply != null && reply.HasProfile && reply.profile.id == id && Account.Apply(reply.profile)) { done?.Invoke(true, ""); return; }
                NoteServer(reply);
                string reason = reply == null || !string.IsNullOrEmpty(reply.message) || reply.status == 0 ? ServerReason(reply)
                    : reply.status == 409 ? "이미 해금한 아이템입니다." : "코인이 부족합니다.";
                done?.Invoke(false, reason);
            });
        }
        // The member's nickname stays the server's; a guest's name lives in session memory only.
        void IHudData.SaveProfile(PlayerProfile profile)
        {
            if (profile == null) return;
            if (!Account.IsMember) Account.SetNickname(profile.Name);
            SaveSettings(AccountSession.SettingsOf(profile));
            if(!userSettings.DeveloperMode)player=Account.Profile.Clone();
        }
        void IHudData.Recalibrate() => Pose.Recalibrate();

        // A new member (or back to a guest) replaces the player and the face badge; coin or setting updates do not.
        private void AccountChanged()
        {
            string id = Account.IsMember ? Account.Id : "";
            if (id == shownAccount) return;
            shownAccount = id; tutorialOffered = false;
            if (!Account.IsMember) ClearPortrait();
            faceAvatar.Apply(portrait);
            // The HUD best and the next result compare with this member's /stats.
            memberStats = null; memberStatsId = "";
            if (Account.IsMember && gateway != null) RefreshMemberStats();
            if (userSettings == null) return;
            if (!userSettings.DeveloperMode) player = Account.Profile.Clone();
            else if (Account.IsMember) player.Name = Account.Nickname;
        }
        private void SignedInReply(AccountReply reply)
        {
            if (reply == null || !reply.HasProfile) return;
            ClearPortrait();
            // The face crop only exists in this reply; it becomes the badge and is never stored.
            if (!string.IsNullOrEmpty(reply.avatar))
            {
                portrait = new Texture2D(2, 2, TextureFormat.RGB24, false);
                try { if (!portrait.LoadImage(System.Convert.FromBase64String(reply.avatar))) ClearPortrait(); } catch { ClearPortrait(); }
            }
            shownAccount = ""; loginNotice = ""; expiredToken = null;
            Account.SignInMember(reply.profile, reply.token);
        }
        private void ClearPortrait() { if (portrait != null) Destroy(portrait); portrait = null; }
        // Back to a guest at once; /logout is sent without waiting.
        private void SignOutMember()
        {
            if (!Account.IsMember) return;
            string token = Account.Token;
            Account.SignOut();
            if (!string.IsNullOrEmpty(token)) gateway.Logout(token, null);
        }
        // Only the signed-in member's own token counts; a late 401 for an earlier member is ignored.
        private void SessionExpired(string token)
        {
            if (!Account.IsMember || token != Account.Token) return;
            expiredToken = token;
            if (Session.Flow.State == GameFlowState.TitleIdle && !hud.Loading) ApplyExpiry();
        }
        private bool ExpiryPending => expiredToken != null && Account.IsMember && expiredToken == Account.Token;
        private void ApplyExpiry()
        {
            bool expired = ExpiryPending; expiredToken = null;
            if (!expired) return;
            Account.SignOut(); loginNotice = ExpiredNotice;
            if (settingsWindow != null && settingsWindow.IsOpen) settingsWindow.Close(true);
            hud.OpenLogin();
        }

        // Local or central voluntary identity UI
        public bool IdentityBusy=>identity.Busy;
        public bool SignedIn=>Account.IsMember;
        public string IdentityMessage=>identity.Message;
        public Texture Portrait=>portrait;
        public bool ServerChecking=>serverChecking;
        public string ServerProblem=>serverProblem;
        public string LoginNotice=>loginNotice;
        // Asked each time the login gate opens; until the server answers, face login and sign-up wait. Only the latest answer counts.
        public void CheckServer()
        {
            int mine=++serverTicket; serverChecking=true;
            gateway.Health(reply=>{ if(mine!=serverTicket)return; serverChecking=false; serverProblem=reply!=null&&reply.ok?"":ServerReason(reply); });
        }
        public void CheckNickname(string name,System.Action<bool,string> done)
        {
            string problem=AccountSession.NicknameProblem(name);
            if(problem!=null){done?.Invoke(false,problem);return;}
            gateway.NameCheck(name.Trim(),reply=>
            {
                if(reply==null||!reply.ok){NoteServer(reply);done?.Invoke(false,ServerReason(reply));return;}
                done?.Invoke(reply.available,reply.available?"":string.IsNullOrEmpty(reply.message)?"사용할 수 없는 닉네임입니다.":reply.message);
            });
        }
        public void FaceLogin(System.Action<bool> done)
        {
            if (IdentityBusy || Account.IsMember) return;
            captureFaces(images => gateway.Login(images, reply => FaceReply(reply, done)));
        }
        // Sign-up: the nickname and settings chosen in the sign-up steps, not the guest's.
        public void FaceEnroll(string nickname, AccountSettings settings, System.Action<bool> done)
        {
            if (IdentityBusy || Account.IsMember || AccountSession.NicknameProblem(nickname) != null) return;
            settings ??= new AccountSettings();
            captureFaces(images => gateway.Enroll(new AccountEnrollment { name = nickname.Trim(), consent = true, images = images, weight = settings.Weight, style = (int)settings.Style, goal = settings.Goal }, reply => FaceReply(reply, done)));
        }
        private void FaceReply(AccountReply reply, System.Action<bool> done)
        {
            NoteServer(reply); SignedInReply(reply);
            done?.Invoke(reply != null && reply.HasProfile && Account.IsMember);
        }
        // A request that could not reach the server disables face login and sign-up on the gate until the next check.
        // An expired member token (401) is handled for every call by ExpiryWatchingGateway (SessionExpired).
        private void NoteServer(AccountResult reply) { if (reply != null && !reply.ok && reply.status == 0) serverProblem = ServerReason(reply); }
        private static string ServerReason(AccountResult reply) => reply != null && !string.IsNullOrEmpty(reply.message) ? reply.message : "얼굴 서버에 연결할 수 없습니다.";
        public void FaceLogout(bool delete)
        {
            if (IdentityBusy || !Account.IsMember) return;
            if (!delete) { SignOutMember(); return; }
            DeleteAccount(null);
        }
        // 내 정보: nickname and settings in one /profile/update; the session changes only after the server accepted them.
        public void SaveMemberProfile(string nickname, AccountSettings settings, System.Action<bool,string> done)
        {
            if (!Account.IsMember) { done?.Invoke(false, "회원만 저장할 수 있습니다."); return; }
            string problem = AccountSession.NicknameProblem(nickname);
            if (problem != null) { done?.Invoke(false, problem); return; }
            var update = AccountProfileUpdate.From(settings ?? Account.Settings);
            string name = nickname.Trim(), id = Account.Id;
            if (name != Account.Nickname) update.Name = name;
            gateway.UpdateProfile(Account.Token, update, reply =>
            {
                if (reply != null && reply.HasProfile && reply.profile.id == id && Account.Apply(reply.profile))
                {
                    if (!userSettings.DeveloperMode) player = Account.Profile.Clone(); else player.Name = Account.Nickname;
                    done?.Invoke(true, ""); return;
                }
                NoteServer(reply);
                done?.Invoke(false, reply != null && reply.status == 409 && string.IsNullOrEmpty(reply.message) ? "이미 사용 중인 닉네임입니다." : ServerReason(reply));
            });
        }
        // Server totals for 내 정보; null when there is no member or no token.
        public void MemberStats(System.Action<StatsReply> done)
        {
            if (!Account.IsMember || string.IsNullOrEmpty(Account.Token)) { done?.Invoke(null); return; }
            gateway.Stats(Account.Token, done);
        }
        // 회원 랭킹 for anyone; a member's token adds their own row ("me").
        public void MemberLeaderboard(PlayStyle mode, bool today, System.Action<LeaderboardReply> done)
        {
            if (gateway == null) { done?.Invoke(new LeaderboardReply { message = "서버에 연결할 수 없어요" }); return; }
            gateway.Leaderboard(Account.IsMember ? Account.Token : "", mode, today, done);
        }
        // Account deletion (face, profile and runs on the server, and the member's rows on this PC's ranking); the player becomes a guest.
        public void DeleteAccount(System.Action<bool,string> done)
        {
            if (IdentityBusy || !Account.IsMember) { done?.Invoke(false, "회원만 탈퇴할 수 있습니다."); return; }
            string id = Account.Id;
            gateway.Delete(Account.Token, reply =>
            {
                bool ok = reply != null && reply.ok;
                if (ok) { Store.RemoveAccount(id); if (Account.IsMember && Account.Id == id) Account.SignOut(); }
                else NoteServer(reply);
                done?.Invoke(ok, ok ? "" : ServerReason(reply));
            });
        }

        // ISettingsHost
        UserSettings ISettingsHost.Settings => userSettings;
        UserSettings ISettingsHost.Defaults => settingsDefaults;
        System.Collections.Generic.IReadOnlyList<string> ISettingsHost.CameraDevices => PoseTracker.CameraDevices();
        IHudData ISettingsHost.Hud => this;
        void ISettingsHost.RefreshCamera() => Pose.RefreshCamera();
    }
}
