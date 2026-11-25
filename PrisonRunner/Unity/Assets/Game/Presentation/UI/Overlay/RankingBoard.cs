using System;
using System.Collections.Generic;
using System.Globalization;
using Muhanok.Application;
using Muhanok.Domain;

namespace Muhanok.Presentation
{
    public enum RankingStatus { Ready, Loading, Failed }

    // One ranking row as drawn: the server board (회원 랭킹) or this PC's board (이 PC, member runs marked).
    public sealed class RankingRow
    {
        // Runs with and without "발구름으로 속도 조절" share one ranking and are not marked.
        public int Rank; public string Name = ""; public int Score; public bool Member;
    }

    // Ranking tabs [회원 랭킹 | 이 PC] × [전신 | 상체 | 키보드] × [전체 | 오늘], and on the result the best score and calorie lines.
    // Kept apart from the IMGUI drawing so tests can read what the screen shows.
    public sealed class RankingBoard
    {
        public const string LoadingText = "불러오는 중", OfflineText = "서버에 연결할 수 없어요";
        public const string GuestKcalNote = "회원가입하면 누적 칼로리를 볼 수 있어요", GuestBestNote = "회원가입하면 내 최고 기록을 볼 수 있어요";
        public const string NewBestText = "신기록!";
        static readonly CultureInfo Num = CultureInfo.InvariantCulture;

        public bool MemberTab { get; private set; }
        public PlayStyle Mode { get; private set; }
        public bool TodayOnly { get; private set; }
        // Today's challenge board (exercise plan 10): today's runs of the day's map, kept apart from the ordinary boards.
        public bool Challenge { get; private set; }
        public bool ResultMode { get; private set; }
        public RankingStatus Status { get; private set; }
        public readonly List<RankingRow> Rows = new List<RankingRow>();
        // The player's own row (this run on 이 PC, the member's best on 회원 랭킹); null when there is none.
        public RankingRow Mine { get; private set; }
        // The run just finished on this PC's board (to highlight it).
        public RankingEntry MineEntry { get; private set; }
        // The result belongs to a member (signed in when the run ended).
        public bool MemberResult { get; private set; }
        IHudData data;
        IIdentityData identity;
        int request;

        public void OpenBoard(IHudData hud, IIdentityData account)
        {
            data = hud; identity = account; ResultMode = MemberResult = false; MineEntry = null; Challenge = false;
            MemberTab = SignedIn; Mode = hud?.SavedProfile?.Style ?? PlayStyle.FullBody;
            // Developer (exhibition) mode starts on today's tab.
            TodayOnly = hud != null && hud.DeveloperMode;
            Reload();
        }
        // After a run: a member starts on 회원 랭킹, a guest on 이 PC; the mode just played and today.
        public void OpenResult(IHudData hud, IIdentityData account)
        {
            data = hud; identity = account; ResultMode = true; MineEntry = hud?.LastEntry;
            MemberResult = SignedIn; MemberTab = MemberResult;
            Mode = hud?.Player?.Style ?? PlayStyle.Keyboard; TodayOnly = true; Challenge = hud != null && hud.ChallengeRun;
            Reload();
        }
        public void Select(bool memberTab, PlayStyle mode, bool todayOnly, bool challenge = false)
        {
            if (memberTab == MemberTab && mode == Mode && todayOnly == TodayOnly && challenge == Challenge) return;
            MemberTab = memberTab; Mode = mode; TodayOnly = todayOnly; Challenge = challenge; Reload();
        }
        // The server keeps challenge runs under their own mode numbers (DailyChallenge.Mode), always for today.
        PlayStyle ServerMode => Challenge ? (PlayStyle)DailyChallenge.Mode(Mode) : Mode;
        bool ServerToday => Challenge || TodayOnly;

        bool SignedIn => identity != null && identity.SignedIn;
        // Rows hold copied names: after this PC's records change (a guest named the run) the 이 PC tab is rebuilt.
        public void RefreshLocal() { if (!MemberTab) Reload(); }
        // The failed member board asks the server again.
        public void Retry() { if (Status == RankingStatus.Failed) Reload(); }

        void Reload()
        {
            Rows.Clear(); Mine = null; MembersMissing = false; int mine = ++request;
            if (!MemberTab)
            {
                // 통합 랭킹: this PC's runs at once, then this PC's guest runs merged with every member's best from the server.
                Status = RankingStatus.Ready;
                var list = data?.Board == null ? new List<RankingEntry>() : Challenge ? data.Board.ChallengeView(DailyChallenge.Day(DateTime.UtcNow), Mode) : data.Board.View(TodayOnly, DateTime.Now, Mode);
                ShowLocal(list);
                if (identity == null) { MembersMissing = true; return; }
                identity.MemberLeaderboard(ServerMode, ServerToday, reply =>
                {
                    if (mine != request) return;
                    // Offline: keep this PC's rows (members included) and say the other PCs' members are missing.
                    if (reply == null || !reply.ok) { MembersMissing = true; return; }
                    Rows.Clear(); Mine = null;
                    var merged = Combined(list, reply, MineEntry, out var mineRow);
                    Rows.AddRange(merged); Mine = mineRow;
                });
                return;
            }
            if (identity == null) { Status = RankingStatus.Failed; return; }
            Status = RankingStatus.Loading;
            identity.MemberLeaderboard(ServerMode, ServerToday, reply =>
            {
                // A later tab change wins over an older reply.
                if (mine != request) return;
                if (reply == null || !reply.ok) { Status = RankingStatus.Failed; return; }
                Status = RankingStatus.Ready;
                if (reply.entries != null) foreach (var e in reply.entries) if (e != null) Rows.Add(new RankingRow { Rank = e.rank, Name = e.name ?? "", Score = e.score, Member = true });
                if (reply.me != null && reply.me.rank > 0) Mine = new RankingRow { Rank = reply.me.rank, Name = "내 기록", Score = reply.me.score, Member = true };
            });
        }

        void ShowLocal(List<RankingEntry> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var row = new RankingRow { Rank = i + 1, Name = list[i].Name ?? "", Score = list[i].Score, Member = list[i].Member };
                Rows.Add(row);
                if (MineEntry != null && list[i] == MineEntry) Mine = row;
            }
        }
        // The member board could not be read: 통합 랭킹 shows only this PC's runs.
        public bool MembersMissing { get; private set; }
        public const string MembersMissingText = "회원 기록 연결 안 됨\n이 PC 기록만 표시";

        // This PC's guest runs (every run) and the server's members (best run each), by score; ties keep the guest first.
        // This PC's member runs are left out because the server already has them, except a member run that just finished
        // and is not on the server yet (its upload is still on the way), which stands in for that member's server row.
        public static List<RankingRow> Combined(IList<RankingEntry> local, LeaderboardReply reply, RankingEntry mineEntry, out RankingRow mine)
        {
            mine = null;
            var guests = new List<RankingEntry>();
            foreach (var entry in local) if (entry != null && !entry.Member) guests.Add(entry);
            var members = new List<RankingRow>();
            int serverMine = reply.me != null && reply.me.rank > 0 ? reply.me.rank : 0;
            // Only a run on this view (its play style and period) can stand in for the member's server row.
            bool pending = mineEntry != null && mineEntry.Member && local.Contains(mineEntry) && (serverMine == 0 || reply.me.score < mineEntry.Score);
            RankingRow mineMember = null;
            if (reply.entries != null)
                foreach (var e in reply.entries)
                {
                    if (e == null) continue;
                    var row = new RankingRow { Name = e.name ?? "", Score = e.score, Member = true };
                    // The member's own server row gives way to the newer run that is still uploading.
                    if (e.rank == serverMine && serverMine > 0) { if (pending) continue; mineMember = row; }
                    members.Add(row);
                }
            if (pending)
            {
                mineMember = new RankingRow { Name = mineEntry.Name ?? "", Score = mineEntry.Score, Member = true };
                int at = 0; while (at < members.Count && members[at].Score >= mineMember.Score) at++;
                members.Insert(at, mineMember);
            }
            var rows = new List<RankingRow>(guests.Count + members.Count);
            int g = 0, m = 0;
            while (g < guests.Count || m < members.Count)
            {
                bool guest = m >= members.Count || g < guests.Count && guests[g].Score >= members[m].Score;
                if (guest)
                {
                    var row = new RankingRow { Name = guests[g].Name ?? "", Score = guests[g].Score, Member = false };
                    if (mineEntry != null && guests[g] == mineEntry) mine = row;
                    rows.Add(row); g++;
                }
                else rows.Add(members[m++]);
                rows[rows.Count - 1].Rank = rows.Count;
            }
            if (mineMember != null) mine = mineMember;
            // A member below the server's top list: ranked after every row that scores higher.
            else if (mine == null && serverMine > 0 && (mineEntry == null || mineEntry.Member))
            {
                int above = serverMine - 1;
                foreach (var guest in guests) if (guest.Score > reply.me.score) above++;
                mine = new RankingRow { Rank = above + 1, Name = "내 기록", Score = reply.me.score, Member = true };
            }
            return rows;
        }

        public string StatusText =>Status == RankingStatus.Loading ? LoadingText : Status == RankingStatus.Failed ? OfflineText : "";

        // Result title: "내 최고 N점" for a member (or an exhibition run), the sign-up note for a guest.
        public string BestLine
        {
            get
            {
                if (!ResultMode || data == null) return "";
                if (data.LastBestScore > 0) return "내 최고 " + RunHudModel.Points(data.LastBestScore);
                return !MemberResult && !data.DeveloperMode ? GuestBestNote : "";
            }
        }
        public bool NewBest => ResultMode && data != null && data.LastNewBest;

        // Score version 3 started a new ranking: the result says so while the new season is just starting for this player.
        // A member: a previous-season best exists for this play style and no new-season best before this run.
        // Otherwise: this PC has older-formula records for the play style and at most three runs of the new season.
        public const string NewSeasonText = "새 랭킹 시작";
        public const string NewSeasonMemberLine = "점수 계산이 바뀌어 랭킹을 새로 시작했어요 · 이전 기록은 내 정보에", NewSeasonGuestLine = "점수 계산이 바뀌어 랭킹을 새로 시작했어요 · 이전 기록은 보관돼요";
        public bool NewSeason
        {
            get
            {
                if (!ResultMode || data == null) return false;
                if (MemberResult) return data.LastBestScore <= 0 && data.LastStats != null && data.LastStats.PreviousBest((int)Mode) > 0;
                return data.Board != null && data.Board.HasOlderSeason(Mode) && data.Board.View(false, DateTime.Now, Mode).Count <= 3;
            }
        }
        public string SeasonLine => !NewSeason ? "" : MemberResult ? NewSeasonMemberLine : NewSeasonGuestLine;

        // Calorie panel lines (label, value): this run, then a member's today and lifetime totals once /stats answered.
        public List<KeyValuePair<string, string>> KcalLines(RunSession session)
        {
            var lines = new List<KeyValuePair<string, string>>();
            bool camera = PlayStyleInfo.UsesCamera(data?.Player?.Style ?? PlayStyle.Keyboard);
            // Keyboard runs keep the existing note: this run's calories are not counted.
            lines.Add(new KeyValuePair<string, string>("이번 판 (추정)", camera && session != null ? Kcal(session.Workout.EstimatedKcal) : "키보드 제외"));
            if (!MemberResult) return lines;
            var stats = data?.LastStats;
            if (stats != null)
            {
                lines.Add(new KeyValuePair<string, string>("오늘 합계", Kcal(stats.today_kcal)));
                lines.Add(new KeyValuePair<string, string>("누적 합계", Kcal(stats.total_kcal)));
            }
            return lines;
        }
        // Below the lines: the guest note, or a member's loading/failure text for the totals.
        public string KcalNote => !MemberResult ? GuestKcalNote : data == null || data.LastStats != null ? "" : data.LastStatsFailed ? OfflineText : "합계 " + LoadingText;

        static string Kcal(double value) => (double.IsNaN(value) ? 0 : Math.Max(0, value)).ToString(value >= 100 ? "N0" : "F1", Num) + " kcal";
    }
}
