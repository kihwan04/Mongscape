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
    // Only runs with --mongscape-exercise-review: screenshots of the exercise-plan screens without a webcam (synthetic workout
    // numbers and a fake account server), saved to --review-output <dir>. Use with --muhanok-data <dir>.
    // The log ends with MONGSCAPE_EXERCISE_REVIEW_PASS or MONGSCAPE_EXERCISE_REVIEW_FAIL.
    public sealed class RuntimeExerciseReview : MonoBehaviour
    {
        public const string Flag = "--mongscape-exercise-review";
        private GameBootstrap game;
        private string destination;
        private float started;
        private string failure;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) < 0) return;
            new GameObject("Exercise Review").AddComponent<RuntimeExerciseReview>();
        }
        private void Start()
        {
            started = Time.realtimeSinceStartup;
            UnityEngine.Application.logMessageReceived += LogFailure;
            UnityEngine.Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--review-output");
            destination = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(UnityEngine.Application.persistentDataPath, "ExerciseReview");
            Directory.CreateDirectory(destination);
            StartCoroutine(Run());
        }
        private IEnumerator Run()
        {
            while ((game = FindFirstObjectByType<GameBootstrap>()) == null || game.Session == null || game.Session.Flow.State != GameFlowState.TitleIdle) yield return null;
            game.SetDeveloperMode(false, false);
            yield return new WaitForSecondsRealtime(2f);
            var gateway = new ReviewGateway();
            game.UseAccountServices(gateway);
            // An older-season run on this PC (kept, not ranked) and a member with a previous-season best.
            game.Store.Record(new RankingEntry { Name = "리뷰", Score = 4210, Distance = 2000, ScoreVersion = RunScore.PreviousVersion, Mode = 0, Style = "전신", AccountId = "review", Ticks = DateTime.Now.AddDays(-3).Ticks }, 20);
            game.Account.SignInMember(gateway.Profile, "synthetic-review");
            yield return Stage2Result();
            yield return Stage2Profile();
            yield return Stage3();
            if (failure != null) { Fail(failure); yield break; }
            Debug.Log("MONGSCAPE_EXERCISE_REVIEW_PASS: " + destination);
            UnityEngine.Application.Quit(0);
        }

        // Stage 2: the result report (intensity bars, move counts, week comparison, "새 랭킹 시작") after a synthetic full-body run.
        private IEnumerator Stage2Result()
        {
            // Nobody steers: the run stays invincible until the review ends it.
            game.Session.Invincible = true;
            game.StartPlayerRun(new PlayerProfile { Name = "리뷰", Weight = "65", Style = PlayStyle.FullBody, CalorieGoal = 50 });
            float wait = Time.realtimeSinceStartup;
            while (game.IsLoading || game.IsRevealing || game.Session.Flow.State != GameFlowState.Running) { if (Time.realtimeSinceStartup - wait > 60) { Fail("run did not start"); yield break; } yield return null; }
            yield return new WaitForSecondsRealtime(2.5f);
            var w = game.Session.Workout;
            w.TickIntensity(95, true, true, CadenceStage.Walk, false, false, 0);
            w.TickIntensity(240, true, true, CadenceStage.Run, false, false, 0);
            w.TickIntensity(70, true, true, CadenceStage.Sprint, false, true, 1);
            w.CountMove(MoveKind.Step, 812); w.CountMove(MoveKind.Jump, 24); w.CountMove(MoveKind.Crouch, 17); w.CountMove(MoveKind.KneeStep, 36); w.CountMove(MoveKind.SideStep, 41);
            game.Session.Score.Advance(1240);
            game.Session.Invincible = false; game.Session.ForceCatch();
            wait = Time.realtimeSinceStartup;
            while (game.Session.Flow.State != GameFlowState.Result) { if (Time.realtimeSinceStartup - wait > 30) { Fail("result did not appear"); yield break; } yield return null; }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("S2_01_Result_Report");
        }
        private IEnumerator Stage2Profile()
        {
            game.ExitToMenu(); yield return new WaitForSecondsRealtime(.5f);
            game.Hud.ShowPage(MenuPage.Profile); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("S2_02_Profile_PreviousSeason");
            game.OpenMainMenu();
        }

        // Stage 3: the setup toggle, the running ranking strip (and a break-through mid-effect), red pace edges, ranking tags.
        private IEnumerator Stage3()
        {
            game.OpenStartSetup(); yield return new WaitForSecondsRealtime(.8f);
            yield return Shot("S3_01_Setup_CadenceToggle");
            game.Hud.CloseSetup();
            // Nobody steers: the run stays invincible until the review ends it.
            game.Session.Invincible = true;
            game.StartPlayerRun(new PlayerProfile { Name = "리뷰", Weight = "65", Style = PlayStyle.FullBody, CalorieGoal = 50 });
            float wait = Time.realtimeSinceStartup;
            while (game.IsLoading || game.IsRevealing || game.Session.Flow.State != GameFlowState.Running) { if (Time.realtimeSinceStartup - wait > 60) { Fail("run did not start"); yield break; } yield return null; }
            yield return new WaitForSecondsRealtime(3f);
            // Freeze the simulation (nobody steers) and show synthetic numbers.
            game.enabled = false;
            var race = game.Race;
            race.Load(new[] { new RankRace.Row("김탈옥", 4210), new RankRace.Row("박점프", 3980), new RankRace.Row("이숙임", 3700), new RankRace.Row("최무릎", 2900) });
            race.Update(3830); while (race.TakeOvertake(out _)) { }
            yield return Shot("S3_02_Run_RankStrip");
            race.Update(3950); yield return Shot("S3_03_Run_RankStrip_Close");
            race.Update(4000);
            yield return new WaitForSecondsRealtime(.18f);
            ScreenCapture.CaptureScreenshot(Path.Combine(destination, "S3_04_Run_RankStrip_BreakThrough.png"));
            yield return new WaitForSecondsRealtime(1f);
            game.Session.PacePressure = .6f;
            yield return Shot("S3_05_Run_PacePressureEdges");
            game.Session.PacePressure = 0;
            race.Update(5000); yield return new WaitForSecondsRealtime(1.5f); yield return Shot("S3_06_Run_FirstPlace");
            game.enabled = true;
            game.Session.Invincible = false; game.Session.ForceCatch();
            wait = Time.realtimeSinceStartup;
            while (game.Session.Flow.State != GameFlowState.Result) { if (Time.realtimeSinceStartup - wait > 30) { Fail("result did not appear"); yield break; } yield return null; }
            game.ExitToMenu(); yield return new WaitForSecondsRealtime(.5f);
            game.Store.Record(new RankingEntry { Name = "발구름꺼짐", Score = 640, Distance = 600, ScoreVersion = RunScore.Version, Mode = 0, Style = "전신", CadenceSpeed = 0, Ticks = DateTime.Now.Ticks }, 3);
            game.Store.Record(new RankingEntry { Name = "발구름켜짐", Score = 720, Distance = 650, ScoreVersion = RunScore.Version, Mode = 0, Style = "전신", CadenceSpeed = 1, Ticks = DateTime.Now.Ticks + 1 }, 3);
            for (int i = 0; i < 4; i++) game.Store.Record(new RankingEntry { Name = "게스트" + i, Score = 500 - i * 40, Distance = 400, ScoreVersion = RunScore.Version, Mode = 0, Style = "전신", CadenceSpeed = i % 2, Ticks = DateTime.Now.Ticks + 2 + i }, 2);
            game.Hud.ShowPage(MenuPage.Ranking); game.Hud.Ranking.Select(false, PlayStyle.FullBody, false); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("S3_07_Ranking_CadenceTags");
            game.OpenMainMenu();
        }
        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(.6f);
            string path = Path.Combine(destination, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.5f);
            if (!File.Exists(path)) { Fail("missing " + path); yield break; }
            Debug.Log("EXERCISE_CAPTURE: " + name);
        }
        private static void Fail(string reason) { Debug.LogError("MONGSCAPE_EXERCISE_REVIEW_FAIL: " + reason); UnityEngine.Application.Quit(1); }
        private void LogFailure(string message, string stack, LogType type) { if (type == LogType.Exception && failure == null) failure = message; }
        private void Update() { if (started > 0 && Time.realtimeSinceStartup - started > 300) { started = 0; Fail("review timeout"); } }
        private void OnDestroy() { UnityEngine.Application.logMessageReceived -= LogFailure; }

        // An in-memory account server: a member with a previous-season best, this week's and last week's totals.
        private sealed class ReviewGateway : IAccountGateway
        {
            public readonly AccountProfile Profile = new AccountProfile { id = "review", name = "리뷰", weight = 65, style = (int)PlayStyle.FullBody, goal = 50, coins = 120, unlocked = new int[0], tutorial_done = true };
            private int savedRuns;
            private AccountReply Reply() => new AccountReply { ok = true, profile = Profile };
            public void Health(Action<AccountResult> done) => done?.Invoke(new AccountResult { ok = true });
            public void NameCheck(string name, Action<AccountReply> done) => done?.Invoke(new AccountReply { ok = true, available = true });
            public void Enroll(AccountEnrollment request, Action<AccountReply> done) => done?.Invoke(Reply());
            public void Login(string[] images, Action<AccountReply> done) => done?.Invoke(Reply());
            public void FetchProfile(string token, Action<AccountReply> done) => done?.Invoke(Reply());
            public void UpdateProfile(string token, AccountProfileUpdate update, Action<AccountReply> done) => done?.Invoke(Reply());
            public void SaveRun(string token, AccountRun run, Action<AccountReply> done) { savedRuns++; done?.Invoke(new AccountReply { ok = true, coins = Profile.coins }); }
            public void Buy(string token, ItemKind item, Action<AccountReply> done) => done?.Invoke(Reply());
            public void Leaderboard(string token, PlayStyle mode, bool today, Action<LeaderboardReply> done)
                => done?.Invoke(new LeaderboardReply { ok = true, entries = new[] { new LeaderboardRow { rank = 1, name = "달리기왕", score = 5120 }, new LeaderboardRow { rank = 2, name = "점프맨", score = 4380 } } });
            public void Stats(string token, Action<StatsReply> done) => done?.Invoke(new StatsReply
            {
                ok = true, today_kcal = 38.2, total_kcal = 412, best_score = new[] { savedRuns > 0 ? 3460 : 0, 0, 0 }, previous_best = new[] { 4210, 1880, 0 },
                week_kcal = 96.4, week_seconds = 2460, last_week_kcal = 74.1, last_week_seconds = 1980,
            });
            public void Logout(string token, Action<AccountReply> done) => done?.Invoke(new AccountReply { ok = true });
            public void Delete(string token, Action<AccountReply> done) => done?.Invoke(new AccountReply { ok = true });
        }
    }
}
#endif
