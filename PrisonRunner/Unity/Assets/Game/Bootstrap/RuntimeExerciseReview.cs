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
            yield return Stage5();
            yield return Stage6();
            yield return FollowupCadenceOff();
            yield return FollowupCoins();
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
            // Stage 4: coach subtitles (praise for a big jump; the pace line with the red edges).
            game.CoachState.Reset(); game.CoachState.Tick(Time.realtimeSinceStartup, .1f, new Coach.Input { BigJump = true });
            yield return new WaitForSecondsRealtime(.1f); ScreenCapture.CaptureScreenshot(Path.Combine(destination, "S4_01_Run_CoachPraise.png")); yield return new WaitForSecondsRealtime(2.5f);
            game.Session.PacePressure = .5f; game.CoachState.Tick(Time.realtimeSinceStartup, .1f, new Coach.Input { Lagging = true });
            yield return new WaitForSecondsRealtime(.1f); ScreenCapture.CaptureScreenshot(Path.Combine(destination, "S4_02_Run_CoachPace.png")); yield return new WaitForSecondsRealtime(.6f);
            game.Session.PacePressure = 0;
            game.enabled = true;
            game.Session.Invincible = false; game.Session.ForceCatch();
            wait = Time.realtimeSinceStartup;
            while (game.Session.Flow.State != GameFlowState.Result) { if (Time.realtimeSinceStartup - wait > 30) { Fail("result did not appear"); yield break; } yield return null; }
            game.ExitToMenu(); yield return new WaitForSecondsRealtime(.5f);
            // Runs with the cadence option on and off share one board, without an on/off tag.
            game.Store.Record(new RankingEntry { Name = "속도조절끄고", Score = 640, Distance = 600, ScoreVersion = RunScore.Version, Mode = 0, Style = "전신", Ticks = DateTime.Now.Ticks }, 3);
            game.Store.Record(new RankingEntry { Name = "속도조절켜고", Score = 720, Distance = 650, ScoreVersion = RunScore.Version, Mode = 0, Style = "전신", Ticks = DateTime.Now.Ticks + 1 }, 3);
            for (int i = 0; i < 4; i++) game.Store.Record(new RankingEntry { Name = "게스트" + i, Score = 500 - i * 40, Distance = 400, ScoreVersion = RunScore.Version, Mode = 0, Style = "전신", Ticks = DateTime.Now.Ticks + 2 + i }, 2);
            game.Hud.ShowPage(MenuPage.Ranking); game.Hud.Ranking.Select(false, PlayStyle.FullBody, false); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("F1_03_Ranking_Merged");
            game.OpenMainMenu();
        }
        // Stage 5: fork preview and signs, the course banner, a banana with its danger marker, a challenge result with the
        // weekly goal and streak, the profile's week card.
        private IEnumerator Stage5()
        {
            var owner = "review"; var now = DateTime.UtcNow;
            for (int d = 1; d <= 3; d++) game.Store.RecordExercise(owner, now.AddDays(-d), 900, 700, new double[] { 0, 400, 0, 300 });
            game.OpenStartSetup(); yield return new WaitForSecondsRealtime(.5f);
            ((IHudData)game).SetDailyChallenge(true); yield return Shot("S5_01_Setup_DailyChallenge");
            game.Hud.CloseSetup();
            game.Session.Invincible = true;
            game.StartPlayerRun(new PlayerProfile { Name = "리뷰", Weight = "65", Style = PlayStyle.FullBody, CalorieGoal = 50 });
            float wait = Time.realtimeSinceStartup;
            while (game.IsLoading || game.IsRevealing || game.Session.Flow.State != GameFlowState.Running) { if (Time.realtimeSinceStartup - wait > 60) { Fail("run did not start"); yield break; } yield return null; }
            yield return new WaitForSecondsRealtime(2.5f);
            game.enabled = false; game.Race.Clear();
            Seek(CartFork.Decision - 22, 1); yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("S5_02_Fork_Preview_Signs");
            Seek(CartFork.Decision + 3, 1); yield return new WaitForSecondsRealtime(.15f);
            ScreenCapture.CaptureScreenshot(Path.Combine(destination, "S5_03_Course_Banner.png")); yield return new WaitForSecondsRealtime(1.5f);
            // A banana ahead on the course.
            float peel = -1; int peelLane = 0;
            for (float z = 300; z < 3000 && peel < 0; z += 96)
            {
                Seek(z, 0);
                foreach (var item in game.Map.Active) foreach (var b in item.Chunk.Layout.Bananas) { float at = item.Chunk.Sequence * RouteSurface.Length + b.Z; if (at > z + 6 && (peel < 0 || at < peel)) { peel = at; peelLane = b.Lane; } }
            }
            if (peel > 0) { Seek(peel - 5, peelLane); yield return new WaitForSecondsRealtime(.6f); yield return Shot("S5_04_Banana_DangerMarker"); }
            else Debug.LogWarning("EXERCISE_REVIEW: no banana found for the shot");
            game.enabled = true; game.Session.Workout.TickIntensity(700, true, true, CadenceStage.Run, false, false, 0);
            game.Session.Invincible = false; game.Session.ForceCatch();
            wait = Time.realtimeSinceStartup;
            while (game.Session.Flow.State != GameFlowState.Result) { if (Time.realtimeSinceStartup - wait > 30) { Fail("result did not appear"); yield break; } yield return null; }
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("S5_05_Result_Challenge_WeekGoal_Streak");
            ((IHudData)game).SetDailyChallenge(false);
            game.ExitToMenu(); yield return new WaitForSecondsRealtime(.5f);
            game.Hud.ShowPage(MenuPage.Profile); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("S5_06_Profile_WeekGoal");
            game.OpenMainMenu();
        }
        // Follow-up 1: "달리기 속도 조절" off. Stepping earns nothing; the report shows the motion-moment time. The goal bar
        // shows the server's weekly minutes (every PC of the member).
        private IEnumerator FollowupCadenceOff()
        {
            game.ExitToMenu(); yield return new WaitForSecondsRealtime(.5f);
            var hud = (IHudData)game; hud.SetCadenceSpeed(false);
            game.OpenStartSetup(); yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("F1_01_Setup_CadenceOff");
            game.Hud.CloseSetup();
            game.Session.Invincible = true;
            game.StartPlayerRun(new PlayerProfile { Name = "리뷰", Weight = "65", Style = PlayStyle.FullBody, CalorieGoal = 50 });
            float wait = Time.realtimeSinceStartup;
            while (game.IsLoading || game.IsRevealing || game.Session.Flow.State != GameFlowState.Running) { if (Time.realtimeSinceStartup - wait > 60) { Fail("run did not start"); yield break; } yield return null; }
            yield return new WaitForSecondsRealtime(2f);
            var w = game.Session.Workout;
            w.TickIntensity(300, true, true, CadenceStage.Run, false, false, 0); // stepping: counts nothing with the option off
            w.TickIntensity(140, true, true, CadenceStage.Run, true, false, 1);
            w.TickIntensity(45, true, true, CadenceStage.Standing, true, true, 2);
            w.CountMove(MoveKind.Step, 640); w.CountMove(MoveKind.Jump, 31); w.CountMove(MoveKind.Crouch, 22); w.CountMove(MoveKind.SideStep, 38);
            if (w.CadenceCounts || w.StageSeconds(CadenceStage.Run) > 0) { Fail("cadence-off run counted stepping"); yield break; }
            game.Session.Score.Advance(900);
            game.Session.Invincible = false; game.Session.ForceCatch();
            wait = Time.realtimeSinceStartup;
            while (game.Session.Flow.State != GameFlowState.Result) { if (Time.realtimeSinceStartup - wait > 30) { Fail("result did not appear"); yield break; } yield return null; }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("F1_02_Result_CadenceOff_ServerWeekGoal");
            hud.SetCadenceSpeed(true);
            game.ExitToMenu(); yield return new WaitForSecondsRealtime(.5f);
        }
        // Follow-up 2: the coin line under the bars (far half for a deep slide) and the coins over a jump row. Obstacles look
        // as before (no red outline; only bananas wear the red ring, decision 2026-11-25); coins stay yellow.
        private IEnumerator FollowupCoins()
        {
            game.Session.Invincible = true;
            game.StartPlayerRun(new PlayerProfile { Name = "리뷰", Weight = "65", Style = PlayStyle.FullBody, CalorieGoal = 50 });
            float wait = Time.realtimeSinceStartup;
            while (game.IsLoading || game.IsRevealing || game.Session.Flow.State != GameFlowState.Running) { if (Time.realtimeSinceStartup - wait > 60) { Fail("run did not start"); yield break; } yield return null; }
            yield return new WaitForSecondsRealtime(2f);
            game.enabled = false; game.Race.Clear();
            foreach (var kind in new[] { BonusCoin.UnderBar, BonusCoin.Air })
            {
                float at = -1;
                for (float z = 30; z < 3000 && at < 0; z += 48)
                {
                    Seek(z, 0);
                    foreach (var item in game.Map.Active) foreach (var coin in item.Chunk.BonusCoinViews)
                        if (coin.gameObject.activeSelf && coin.Bonus == kind && coin.transform.position.z > z + 10 && (at < 0 || coin.transform.position.z < at)) at = coin.transform.position.z;
                }
                if (at < 0) { Debug.LogWarning("EXERCISE_REVIEW: no " + kind + " coins found"); continue; }
                Seek(at - (kind == BonusCoin.Air ? 7 : 6), 0); yield return new WaitForSecondsRealtime(.6f);
                yield return Shot(kind == BonusCoin.Air ? "F2_02_Jump_AirCoins" : "F2_01_Bars_CoinLine");
            }
            game.enabled = true; game.Session.Invincible = false; game.Session.ForceCatch();
            wait = Time.realtimeSinceStartup;
            while (game.Session.Flow.State != GameFlowState.Result) { if (Time.realtimeSinceStartup - wait > 30) { Fail("result did not appear"); yield break; } yield return null; }
            game.ExitToMenu(); yield return new WaitForSecondsRealtime(.5f);
        }
        // Stage 6: the new tutorial (camera check, pace gauge with the demo window, police, a lesson with the demo, the
        // follow-along view after three misses, the banana lesson). The camera inputs are synthetic.
        private IEnumerator Stage6()
        {
            game.ExitToMenu(); yield return new WaitForSecondsRealtime(.5f);
            var t = game.Tutorial; bool framed = false; float spm = 0, pressure = 0;
            ((IHudData)game).PreviewStyle(PlayStyle.FullBody);
            game.StartTutorial();
            float wait = Time.realtimeSinceStartup;
            while (game.IsLoading || game.IsRevealing || game.Session.Flow.State != GameFlowState.Running) { if (Time.realtimeSinceStartup - wait > 60) { Fail("tutorial did not start"); yield break; } yield return null; }
            t.NoCamera = () => false; t.Framed = () => framed; t.StepsPerMinute = () => spm; t.PaceScale = () => Mathf.Lerp(.7f, 1.3f, Mathf.InverseLerp(80, 160, spm)); t.PacePressure = () => pressure; t.Tracking = () => true;
            yield return Until(() => t.Script.Current == TutorialStep.Camera, 5);
            yield return Shot("S6_01_Tutorial_CameraCheck");
            framed = true; yield return Until(() => t.Script.Current != TutorialStep.Camera, 6);
            yield return Until(() => t.Script.Current == TutorialStep.Cadence, 10);
            spm = 118; yield return new WaitForSecondsRealtime(1.5f); yield return Shot("S6_02_Tutorial_Cadence_Gauge_Demo");
            yield return Until(() => t.Script.Current == TutorialStep.Police, 12);
            spm = 0; pressure = .5f; yield return new WaitForSecondsRealtime(.8f); yield return Shot("S6_03_Tutorial_Police");
            pressure = 0; spm = 120; yield return Until(() => t.Script.Current == TutorialStep.Lane, 6);
            // Jump: skip ahead to its lesson; three misses grow the demo into "따라 하기".
            while (t.Script.Current != TutorialStep.Jump) t.Script.Pass();
            game.Session.Runner.SetStartZ(TutorialCourse.RestartZ(TutorialStep.Jump) + 2); game.Map.Tick(game.Session.Runner.Z);
            yield return Until(() => t.Waiting, 8);
            yield return Shot("S6_04_Tutorial_Jump_Demo");
            t.Script.Fail(); t.Script.Fail(); t.Script.Fail(); yield return Shot("S6_05_Tutorial_FollowAlong");
            // Follow-up 3: the joint comparison beside "따라 하기" (a synthetic player whose hips stop short of the jump line).
            var oldPose = t.MyPose; var oldBase = t.MyBaseline;
            var mine = new PoseFrame(); var myBase = JointDemo.ReferenceBaseline(PlayStyle.FullBody);
            t.MyBaseline = () => myBase; t.MyPose = () => { JointDemo.Reference(TutorialStep.Jump, PlayStyle.FullBody, JointDemo.LoopSeconds * .08, mine); return mine; };
            yield return Shot("F3_01_Tutorial_JointCompare_Jump");
            while (t.Script.Current != TutorialStep.Crouch) t.Script.Pass();
            t.Script.Fail(); t.Script.Fail(); t.Script.Fail();
            t.MyPose = () => { JointDemo.Reference(TutorialStep.Crouch, PlayStyle.FullBody, JointDemo.LoopSeconds * .5, mine); return mine; };
            yield return Shot("F3_02_Tutorial_JointCompare_Crouch_Crossed");
            t.MyPose = oldPose; t.MyBaseline = oldBase;
            while (t.Script.Current != TutorialStep.Banana) t.Script.Pass();
            game.Session.Runner.SetStartZ(TutorialCourse.RestartZ(TutorialStep.Banana)); game.Map.Tick(game.Session.Runner.Z);
            yield return Until(() => t.Waiting, 8);
            yield return Shot("S6_06_Tutorial_Banana");
            game.ExitToMenu(); yield return new WaitForSecondsRealtime(.5f);
        }
        private IEnumerator Until(Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
            if (!done()) Debug.LogWarning("EXERCISE_REVIEW: wait timed out");
        }
        // Puts the frozen run at route z (the review's own plan), leaning toward a branch.
        private void Seek(float z, int branch)
        {
            var run = game.Session;
            run.Runner.Command(branch < 0 ? MotionCommand.Left : branch > 0 ? MotionCommand.Right : MotionCommand.Center); run.Runner.Tick(.25f, 0);
            for (int i = 0; i < 3 && run.Runner.Lane != branch; i++) { run.Runner.Command(run.Runner.Lane > branch ? MotionCommand.Left : MotionCommand.Right); run.Runner.Tick(.25f, 0); }
            for (float at = run.Runner.Z; at < z; at += 12) game.Map.Tick(at);
            game.Map.Tick(z); run.Runner.SetStartZ(z); game.Hero.Apply(run.Runner); game.Map.AnimateGates(z);
            if (run.Runner.Riding) game.CameraRig.FollowCart(game.Hero.Head, 1, -run.Runner.CartLean * 12); else game.CameraRig.Follow(game.Hero.Head, false, 1);
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
                // 62 minutes from this member's other PCs and runs: the goal bar shows this, not the PC's own log.
                week_moderate_seconds = 3720 + savedRuns * 300, last_week_moderate_seconds = 5400,
            });
            public void Logout(string token, Action<AccountReply> done) => done?.Invoke(new AccountReply { ok = true });
            public void Delete(string token, Action<AccountReply> done) => done?.Invoke(new AccountReply { ok = true });
        }
    }
}
#endif
