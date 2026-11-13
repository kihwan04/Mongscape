using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Muhanok.Domain;

namespace Muhanok.Application
{
    // Account server calls (identity server /name-check ... /delete). Each call answers once through its callback;
    // a failed or unreachable server answers with ok=false. Field names follow the server JSON.
    public interface IAccountGateway
    {
        // GET /health: ok when the server and its database answer.
        void Health(Action<AccountResult> done);
        void NameCheck(string name, Action<AccountReply> done);
        void Enroll(AccountEnrollment enrollment, Action<AccountReply> done);
        void Login(string[] images, Action<AccountReply> done);
        void FetchProfile(string token, Action<AccountReply> done);
        void UpdateProfile(string token, AccountProfileUpdate update, Action<AccountReply> done);
        void SaveRun(string token, AccountRun run, Action<AccountReply> done);
        void Buy(string token, ItemKind item, Action<AccountReply> done);
        void Leaderboard(string token, PlayStyle mode, bool today, Action<LeaderboardReply> done);
        void Stats(string token, Action<StatsReply> done);
        void Logout(string token, Action<AccountReply> done);
        void Delete(string token, Action<AccountReply> done);
    }

    // Passes every call through; a 401 answer to a call made with a member token (expired or revoked on the server)
    // reports that token through Expired before the caller's callback runs.
    public sealed class ExpiryWatchingGateway : IAccountGateway
    {
        private readonly IAccountGateway inner;
        private readonly Action<string> expired;
        public ExpiryWatchingGateway(IAccountGateway gateway, Action<string> onExpired) { inner = gateway; expired = onExpired; }
        private Action<T> Watch<T>(string token, Action<T> done) where T : AccountResult => reply =>
        {
            if (reply != null && !reply.ok && reply.status == 401 && !string.IsNullOrEmpty(token)) expired?.Invoke(token);
            done?.Invoke(reply);
        };
        public void Health(Action<AccountResult> done) => inner.Health(done);
        public void NameCheck(string name, Action<AccountReply> done) => inner.NameCheck(name, done);
        public void Enroll(AccountEnrollment enrollment, Action<AccountReply> done) => inner.Enroll(enrollment, done);
        public void Login(string[] images, Action<AccountReply> done) => inner.Login(images, done);
        public void FetchProfile(string token, Action<AccountReply> done) => inner.FetchProfile(token, Watch(token, done));
        public void UpdateProfile(string token, AccountProfileUpdate update, Action<AccountReply> done) => inner.UpdateProfile(token, update, Watch(token, done));
        public void SaveRun(string token, AccountRun run, Action<AccountReply> done) => inner.SaveRun(token, run, Watch(token, done));
        public void Buy(string token, ItemKind item, Action<AccountReply> done) => inner.Buy(token, item, Watch(token, done));
        public void Leaderboard(string token, PlayStyle mode, bool today, Action<LeaderboardReply> done) => inner.Leaderboard(token, mode, today, Watch(token, done));
        public void Stats(string token, Action<StatsReply> done) => inner.Stats(token, Watch(token, done));
        // Signing out needs no notice.
        public void Logout(string token, Action<AccountReply> done) => inner.Logout(token, done);
        public void Delete(string token, Action<AccountReply> done) => inner.Delete(token, Watch(token, done));
    }

    [Serializable]
    public class AccountResult
    {
        public bool ok; public string message = "";
        // HTTP status; 0 when the server could not be reached.
        [NonSerialized] public long status;
    }

    // The server profile object.
    [Serializable]
    public sealed class AccountProfile
    {
        public string id = "", name = "";
        public double weight = AccountSettings.DefaultWeight; public int style; public double goal = AccountSettings.DefaultGoal;
        public int coins; public int[] unlocked = new int[0]; public bool tutorial_done;
    }

    // /enroll, /login, /profile, /profile/update, /shop/buy, /name-check, /runs, /logout and /delete.
    [Serializable]
    public sealed class AccountReply : AccountResult
    {
        public string token = "", avatar = "", id = "", name = "";
        public AccountProfile profile;
        public bool available;
        public int coins;
        public bool HasProfile => ok && profile != null && !string.IsNullOrEmpty(profile.id);
    }

    [Serializable] public sealed class LeaderboardRow { public int rank; public string name = ""; public int score; public double distance; public long created_at; }
    [Serializable] public sealed class LeaderboardMe { public int rank; public int score; public double distance; }
    [Serializable]
    public sealed class LeaderboardReply : AccountResult
    {
        public LeaderboardRow[] entries = new LeaderboardRow[0];
        // Rank 0 when the player has no record on this board.
        public LeaderboardMe me;
    }
    [Serializable]
    public sealed class StatsReply : AccountResult
    {
        public double today_kcal, total_kcal;
        // Best score per play style: full body, upper body, keyboard.
        public int[] best_score = new int[3];
    }

    // Weight is never 0: 65 stands for "not entered".
    [Serializable]
    public sealed class AccountEnrollment
    {
        public string name = ""; public bool consent = true; public string[] images;
        public double weight = AccountSettings.DefaultWeight; public int style; public double goal = AccountSettings.DefaultGoal;
    }

    // /runs body. The id makes a resend after a lost reply count once on the server.
    [Serializable]
    public sealed class AccountRun
    {
        public const int MaxCoins = 5000;
        public string id = ""; public float distance; public int score; public double kcal, goal = AccountSettings.DefaultGoal;
        public double[] activities = new double[5];
        public int mode, coins; public double play_seconds; public int score_version = 1;

        // Score uses the current formula (RunScore.Version); play seconds are the run clock without pauses.
        public static AccountRun Create(RunSession run, PlayStyle mode, int coins)
        {
            double playSeconds = run.Elapsed;
            var record = new AccountRun
            {
                id = Guid.NewGuid().ToString(), distance = Math.Max(0f, run.Score.Distance), score = Math.Max(0, run.Score.Score),
                kcal = Math.Max(0, run.Workout.EstimatedKcal), goal = run.Workout.GoalKcal, mode = (int)mode,
                coins = Math.Max(0, Math.Min(MaxCoins, coins)), score_version = RunScore.Version,
                play_seconds = double.IsNaN(playSeconds) || double.IsInfinity(playSeconds) ? 0 : Math.Max(0, Math.Min(86400, playSeconds)),
            };
            for (int i = 0; i < 5; i++) record.activities[i] = Math.Max(0, run.Workout.KcalFor(i));
            return record;
        }
    }

    // /profile/update sends only the fields that are set.
    public sealed class AccountProfileUpdate
    {
        public string Name; public double? Weight; public PlayStyle? Style; public double? Goal; public bool? TutorialDone;
        public static AccountProfileUpdate From(AccountSettings settings) =>
            new AccountProfileUpdate { Weight = settings.Weight, Style = settings.Style, Goal = settings.Goal };
        public string ToJson()
        {
            var parts = new List<string>();
            if (Name != null) parts.Add("\"name\":" + Quote(Name));
            if (Weight.HasValue) parts.Add("\"weight\":" + Weight.Value.ToString("R", CultureInfo.InvariantCulture));
            if (Style.HasValue) parts.Add("\"style\":" + (int)Style.Value);
            if (Goal.HasValue) parts.Add("\"goal\":" + Goal.Value.ToString("R", CultureInfo.InvariantCulture));
            if (TutorialDone.HasValue) parts.Add("\"tutorial_done\":" + (TutorialDone.Value ? "true" : "false"));
            return "{" + string.Join(",", parts) + "}";
        }
        private static string Quote(string value)
        {
            var text = new StringBuilder("\"");
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') text.Append('\\').Append(c);
                else if (c < ' ') text.Append("\\u").Append(((int)c).ToString("x4"));
                else text.Append(c);
            }
            return text.Append('"').ToString();
        }
    }
}
