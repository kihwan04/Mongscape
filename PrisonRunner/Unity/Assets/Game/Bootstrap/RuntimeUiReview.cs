#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Muhanok.Application;
using Muhanok.Domain;
using Muhanok.Presentation;
using UnityEngine;

namespace Muhanok.Bootstrap
{
    // Only runs with --muhanok-uicapture: walks through every menu screen, a run and its result, and saves
    // screenshots to --muhanok-out <dir>. Use with --muhanok-data <dir> so real player data stays untouched.
    public sealed class RuntimeUiReview : MonoBehaviour
    {
        private GameBootstrap game;
        private string destination;
        private float reviewStarted;
        private string failure;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--muhanok-uicapture") < 0) return;
            new GameObject("UI Review").AddComponent<RuntimeUiReview>();
        }
        private void Start()
        {
            reviewStarted=Time.realtimeSinceStartup;
            UnityEngine.Application.logMessageReceived+=LogFailure;
            UnityEngine.Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--muhanok-out");
            destination = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(UnityEngine.Application.persistentDataPath, "UiReview");
            Directory.CreateDirectory(destination);
            StartCoroutine(Run());
        }
        private IEnumerator Run()
        {
            float started = Time.realtimeSinceStartup;
            while ((game = FindFirstObjectByType<GameBootstrap>()) == null || game.Session == null || game.Session.Flow.State != GameFlowState.TitleIdle) yield return null;
            game.SetDeveloperMode(false, false);
            yield return new WaitForSecondsRealtime(3f);
            var hud = game.Hud; var data = (IHudData)game;
            // One look at the login gate with the configured face server (connected or not), read-only: nothing is sent but /health.
            game.OpenLogin();
            float checkStarted = Time.realtimeSinceStartup;
            while (((IIdentityData)game).ServerChecking && Time.realtimeSinceStartup - checkStarted < 10) yield return null;
            yield return Shot("00_Login_ConfiguredServer");
            Debug.Log("UI_CAPTURE_SERVER: " + (((IIdentityData)game).ServerProblem is { Length: > 0 } problem ? "unavailable: " + problem : "connected"));
            hud.ShowPage(MenuPage.None);
            // The rest of the review uses only a synthetic in-memory account, never the configured face server.
            game.UseAccountServices(new ReviewAccountGateway());

            yield return Shot("01_Title");
            game.OpenLogin(); yield return Shot("02_Login");
            hud.ShowPage(MenuPage.SignUp); yield return Shot("03_SignUp_Consent");
            hud.SignUp.Agree(); yield return Shot("03b_SignUp_Nickname");
            game.OpenMainMenu(); yield return Shot("03c_MainMenu_Guest");
            hud.ShowPage(MenuPage.Shop); yield return Shot("03d_Shop_Guest");

            Seed(data);
            hud.ShowPage(MenuPage.Profile); yield return Shot("04_Profile");
            hud.ShowPage(MenuPage.Shop); yield return Shot("05_Shop");
            // The review data is on this PC (keyboard runs); the member tab would ask the server.
            hud.ShowPage(MenuPage.Ranking); hud.Ranking.Select(false, PlayStyle.Keyboard, false); yield return Shot("06_Ranking");
            game.OpenMainMenu(); yield return Shot("07_MainMenu");
            // Settings: each tab, then cancel so the review leaves the stored settings untouched.
            var settings = FindFirstObjectByType<SettingsWindow>();
            game.OpenSettings();
            foreach (var page in new[] { "graphics", "sound", "body", "display" }) { settings.ShowPage(page); yield return Shot("07s_Settings_" + page); }
            settings.ShowPage("graphics"); settings.ScrollToEnd(); yield return Shot("07s_Settings_graphics_end");
            settings.Close(false);
            hud.ShowPage(MenuPage.Setup); yield return Shot("08_Setup");
            hud.ShowIdentity();yield return Shot("08_Face_Login");hud.ShowPage(MenuPage.Setup);

            game.StartPlayerRun(new PlayerProfile { Name = "탈옥왕", Weight = "65", Style = PlayStyle.Keyboard,CalorieGoal=180 });
            while (game.IsLoading || game.IsRevealing || game.Session.Flow.State != GameFlowState.Running) yield return null;
            // Nobody steers: a shield kept active lets the run go on long enough for the HUD and pause shots.
            shielded = true; StartCoroutine(KeepShield());
            yield return new WaitForSecondsRealtime(3f);
            game.enabled=false;
            yield return Shot("09_RunHud");
            // Synthetic display checks: keep Pose in keyboard mode so this never opens a camera.
            // Freeze the simulation so extra screenshots cannot run into a rail gap (shields only stop obstacles).
            game.enabled=false;
            double originalMet=game.Session.Workout.Met;
            data.Player.Style=PlayStyle.FullBody; game.Session.Workout.Met=PlayStyleInfo.Met(PlayStyle.FullBody);game.Session.Workout.Tick(60,true,true); workoutPreview=true;
            yield return Shot("09b_Workout_Synthetic");
            workoutPreview=false; yield return Shot("09c_Tracking_Recovery_Synthetic");
            if(game.Session.Flow.State!=GameFlowState.Running){Fail("HUD previews left running state");yield break;}
            data.Player.Style=PlayStyle.Keyboard; game.Session.Workout.Reset();game.Session.Workout.Met=originalMet;
            game.enabled=true;game.TogglePause();
            if(!game.Session.Paused){Fail("Pause did not open");yield break;}
            yield return Shot("10_Pause"); game.TogglePause();
            shielded = false;

            // The next obstacle ends the run; a hit is forced if the route is clear for too long.
            float waited = 0;
            while (game.Session.Flow.State != GameFlowState.Result)
            {
                waited += Time.unscaledDeltaTime;
                if (waited > 8f && game.Session.Flow.State == GameFlowState.Running) game.Session.Hit(true);
                if (Time.realtimeSinceStartup - started > 180) { Fail("result did not appear"); yield break; }
                yield return null;
            }
            yield return Shot("11_Result");

            game.SetDeveloperMode(true, false); game.ExitToMenu(); yield return Shot("12_MainMenu_Developer");
            game.OpenStartSetup(); yield return Shot("13_Setup_Developer");
            // Close the text-entry page while the player window is still alive, then let queued IME events settle.
            // Quitting directly from the setup field can trip the existing native Windows callback shutdown issue.
            game.OpenMainMenu(); yield return new WaitForSecondsRealtime(.8f);
            if(failure!=null){Fail(failure);yield break;}
            Debug.Log("MUHANOK_UI_CAPTURE_PASS: " + destination);
            UnityEngine.Application.Quit(0);
        }
        private bool shielded;
        private bool workoutPreview;
        private void LateUpdate()
        {
            if(workoutPreview&&game!=null)game.Hud.RunModel.Tick(game.Session,game.Map,PlayStyle.FullBody,PoseStatus.Tracking,false,Time.unscaledTime);
        }
        private IEnumerator KeepShield()
        {
            while (shielded) { if (game.Session.Flow.State == GameFlowState.Running) game.Session.Items.Activate(ItemKind.Shield); yield return null; }
        }
        // A profile, runs from today and yesterday, and a member with coins and one unlocked item.
        private void Seed(IHudData data)
        {
            data.SaveProfile(new PlayerProfile { Name = "탈옥왕", Weight = "65", Style = PlayStyle.Keyboard,CalorieGoal=180 });
            var today = DateTime.Now; var yesterday = today.AddDays(-1);
            float[] todayRuns = { 2310, 1530, 980, 640, 455 }, earlier = { 1820, 1240 };
            for (int i = 0; i < todayRuns.Length; i++) game.Store.Record(new RankingEntry { Name = "탈옥왕", Distance = todayRuns[i], Score = (int)todayRuns[i], ScoreVersion = RunScore.Version, Style = "키보드", Ticks = today.AddMinutes(-i * 7).Ticks }, todayRuns[i] / 25);
            for (int i = 0; i < earlier.Length; i++) game.Store.Record(new RankingEntry { Name = "탈옥왕", Distance = earlier[i], Score = (int)earlier[i] + 300, ScoreVersion = RunScore.Version, Style = "전신", Ticks = yesterday.AddMinutes(-i * 9).Ticks }, earlier[i] / 18);
            // This synthetic token only reaches ReviewAccountGateway; it also exercises member stats panels.
            game.Account.SignInMember(new AccountProfile { id = "ui-review", name = "탈옥왕", weight = 65, style = (int)PlayStyle.Keyboard, goal = 180, coins = 80, unlocked = new[] { (int)ItemKind.Magnet } }, "synthetic-ui-review");
        }
        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(.9f);
            string path = Path.Combine(destination, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.5f);
            if (!File.Exists(path)) { Fail("missing " + path); yield break; }
            Debug.Log("UI_CAPTURE: " + name);
        }
        private static void Fail(string reason)
        {
            Debug.LogError("MUHANOK_UI_CAPTURE_FAIL: " + reason);
            UnityEngine.Application.Quit(1);
        }
        private void LogFailure(string message,string stack,LogType type){if(type==LogType.Exception&&failure==null)failure=message;}
        private void Update(){if(reviewStarted>0&&Time.realtimeSinceStartup-reviewStarted>180)Fail("UI review timeout");}
        private void OnDestroy(){UnityEngine.Application.logMessageReceived-=LogFailure;}
        private sealed class ReviewAccountGateway : IAccountGateway
        {
            private readonly AccountProfile profile=new AccountProfile{id="ui-review",name="탈옥왕",weight=65,style=(int)PlayStyle.Keyboard,goal=180,coins=240,unlocked=new[]{(int)ItemKind.Magnet}};
            private AccountReply Reply()=>new AccountReply{ok=true,profile=profile};
            public void Health(Action<AccountResult> done)=>done?.Invoke(new AccountResult{ok=true});
            public void NameCheck(string name,Action<AccountReply> done)=>done?.Invoke(new AccountReply{ok=true,available=true});
            public void Enroll(AccountEnrollment request,Action<AccountReply> done)=>done?.Invoke(Reply());
            public void Login(string[] images,Action<AccountReply> done)=>done?.Invoke(Reply());
            public void FetchProfile(string token,Action<AccountReply> done)=>done?.Invoke(Reply());
            public void UpdateProfile(string token,AccountProfileUpdate update,Action<AccountReply> done)=>done?.Invoke(Reply());
            public void SaveRun(string token,AccountRun run,Action<AccountReply> done)=>done?.Invoke(new AccountReply{ok=true,coins=profile.coins});
            public void Buy(string token,ItemKind item,Action<AccountReply> done)=>done?.Invoke(Reply());
            public void Leaderboard(string token,PlayStyle mode,bool today,Action<LeaderboardReply> done)=>done?.Invoke(new LeaderboardReply{ok=true});
            public void Stats(string token,Action<StatsReply> done)=>done?.Invoke(new StatsReply{ok=true,today_kcal=52.1,total_kcal=310,best_score=new[]{900,800,640}});
            public void Logout(string token,Action<AccountReply> done)=>done?.Invoke(new AccountReply{ok=true});
            public void Delete(string token,Action<AccountReply> done)=>done?.Invoke(new AccountReply{ok=true});
        }
    }
}
#endif
