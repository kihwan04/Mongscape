using System;
using System.Collections.Generic;
using Muhanok.Application;
using Muhanok.Domain;

namespace Muhanok.Tests
{
    // In-memory account server for tests: answers at once and records every call.
    public sealed class FakeAccountGateway : IAccountGateway
    {
        public readonly List<AccountRun> SavedRuns = new List<AccountRun>();
        public readonly List<string> RunTokens = new List<string>();
        public readonly List<string> Calls = new List<string>();
        public AccountProfile Profile = new AccountProfile { id = "fake-member", name = "회원", coins = 0 };
        public bool FailRuns;
        public int Balance;
        public int Logouts;
        // Offline: /health fails. TakenName: /name-check refuses it. FailEnroll: the next /enroll is refused (like a face already registered).
        public bool Offline, FailEnroll;
        public string TakenName = "";
        public readonly List<AccountEnrollment> Enrollments = new List<AccountEnrollment>();
        // /profile/update applies the sent fields (TakenName answers 409), /shop/buy checks coins like the server, /stats answers StatsResult.
        public readonly List<AccountProfileUpdate> Updates = new List<AccountProfileUpdate>();
        public StatsReply StatsResult = new StatsReply { ok = true };
        public int Deletes;
        // /leaderboard answers BoardResult (or fails with FailBoard) and records each request; FailStats makes /stats fail.
        public sealed class BoardRequest { public string Token; public PlayStyle Mode; public bool Today; }
        public readonly List<BoardRequest> BoardRequests = new List<BoardRequest>();
        public LeaderboardReply BoardResult = new LeaderboardReply { ok = true };
        public bool FailBoard, FailStats;
        // Expired: every call with a member token answers 401 like the server's expired session (/logout too is not needed).
        public bool Expired;
        private const string ExpiredMessage = "로그인이 만료되었습니다. 얼굴로 다시 로그인하세요.";
        private bool Refused<T>(Action<T> done) where T : AccountResult, new()
        {
            if (!Expired) return false;
            done?.Invoke(new T { ok = false, status = 401, message = ExpiredMessage }); return true;
        }

        public void Health(Action<AccountResult> done) { Calls.Add("health"); done?.Invoke(new AccountResult { ok = !Offline, message = Offline ? "얼굴 서버 연결 실패" : "" }); }
        private bool Taken(string name) => !string.IsNullOrEmpty(TakenName) && string.Equals(name?.Trim(), TakenName, StringComparison.OrdinalIgnoreCase);
        public void NameCheck(string name, Action<AccountReply> done)
        {
            Calls.Add("name-check");
            bool taken = Taken(name);
            done?.Invoke(new AccountReply { ok = true, available = !taken, message = taken ? "이미 사용 중인 닉네임입니다." : "사용할 수 있는 닉네임입니다." });
        }
        public void Enroll(AccountEnrollment enrollment, Action<AccountReply> done)
        {
            Calls.Add("enroll"); Enrollments.Add(enrollment);
            if (FailEnroll) { FailEnroll = false; done?.Invoke(new AccountReply { ok = false, message = "이미 등록된 얼굴입니다.", status = 409 }); return; }
            Profile.name = enrollment.name; Profile.weight = enrollment.weight; Profile.style = enrollment.style; done?.Invoke(SignedIn());
        }
        public void Login(string[] images, Action<AccountReply> done) { Calls.Add("login"); done?.Invoke(SignedIn()); }
        public void FetchProfile(string token, Action<AccountReply> done) { Calls.Add("profile"); if (Refused(done)) return; done?.Invoke(new AccountReply { ok = true, profile = Profile }); }
        public void UpdateProfile(string token, AccountProfileUpdate update, Action<AccountReply> done)
        {
            Calls.Add("profile/update"); Updates.Add(update);
            if (Refused(done)) return;
            if (update?.Name != null && Taken(update.Name)) { done?.Invoke(new AccountReply { ok = false, status = 409, message = "이미 사용 중인 닉네임입니다." }); return; }
            if (update != null)
            {
                if (update.Name != null) Profile.name = update.Name;
                if (update.Weight.HasValue) Profile.weight = update.Weight.Value;
                if (update.Style.HasValue) Profile.style = (int)update.Style.Value;
                if (update.Goal.HasValue) Profile.goal = update.Goal.Value;
                if (update.TutorialDone.HasValue) Profile.tutorial_done = update.TutorialDone.Value;
            }
            done?.Invoke(new AccountReply { ok = true, profile = Profile });
        }
        public void SaveRun(string token, AccountRun run, Action<AccountReply> done)
        {
            Calls.Add("runs"); if (Refused(done)) return;
            SavedRuns.Add(run); RunTokens.Add(token);
            if (FailRuns) { done?.Invoke(new AccountReply { ok = false, message = "offline" }); return; }
            Balance += run.coins; done?.Invoke(new AccountReply { ok = true, coins = Balance });
        }
        public void Buy(string token, ItemKind item, Action<AccountReply> done)
        {
            Calls.Add("shop/buy"); if (Refused(done)) return;
            var owned = new List<int>(Profile.unlocked ?? new int[0]);
            if (owned.Contains((int)item)) { done?.Invoke(new AccountReply { ok = false, status = 409, message = "이미 해금한 아이템입니다." }); return; }
            if (Profile.coins < ItemCatalog.Get(item).UnlockCost) { done?.Invoke(new AccountReply { ok = false, status = 400, message = "코인이 부족합니다." }); return; }
            Profile.coins -= ItemCatalog.Get(item).UnlockCost; owned.Add((int)item); Profile.unlocked = owned.ToArray();
            done?.Invoke(new AccountReply { ok = true, profile = Profile });
        }
        public void Leaderboard(string token, PlayStyle mode, bool today, Action<LeaderboardReply> done)
        {
            Calls.Add("leaderboard"); BoardRequests.Add(new BoardRequest { Token = token, Mode = mode, Today = today });
            done?.Invoke(FailBoard ? new LeaderboardReply { ok = false, message = "offline" } : BoardResult);
        }
        public void Stats(string token, Action<StatsReply> done) { Calls.Add("stats"); if (Refused(done)) return; done?.Invoke(FailStats ? new StatsReply { ok = false, message = "offline" } : StatsResult); }
        public void Logout(string token, Action<AccountReply> done) { Calls.Add("logout"); Logouts++; done?.Invoke(new AccountReply { ok = true }); }
        public void Delete(string token, Action<AccountReply> done) { Calls.Add("delete"); if (Refused(done)) return; Deletes++; done?.Invoke(new AccountReply { ok = true }); }
        private AccountReply SignedIn() => new AccountReply { ok = true, token = "fake-token", profile = Profile, id = Profile.id, name = Profile.name };
    }
}
