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
            data = hud; identity = account; ResultMode = MemberResult = false; MineEntry = null;
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
            Mode = hud?.Player?.Style ?? PlayStyle.Keyboard; TodayOnly = true;
            Reload();
        }
        public void Select(bool memberTab, PlayStyle mode, bool todayOnly)
        {
            if (memberTab == MemberTab && mode == Mode && todayOnly == TodayOnly) return;
            MemberTab = memberTab; Mode = mode; TodayOnly = todayOnly; Reload();
        }

        bool SignedIn => identity != null && identity.SignedIn;
        // Rows hold copied names: after this PC's records change (a guest named the run) the 이 PC tab is rebuilt.
        public void RefreshLocal() { if (!MemberTab) Reload(); }
        // The failed member board asks the server again.
        public void Retry() { if (Status == RankingStatus.Failed) Reload(); }

        void Reload()
        {
            Rows.Clear(); Mine = null; int mine = ++request;
            if (!MemberTab)
            {
                Status = RankingStatus.Ready;
                var list = data?.Board != null ? data.Board.View(TodayOnly, DateTime.Now, Mode) : new List<RankingEntry>();
                for (int i = 0; i < list.Count; i++)
                {
                    var row = new RankingRow { Rank = i + 1, Name = list[i].Name ?? "", Score = list[i].Score, Member = list[i].Member };
                    Rows.Add(row);
                    if (MineEntry != null && list[i] == MineEntry) Mine = row;
                }
                return;
            }
            if (identity == null) { Status = RankingStatus.Failed; return; }
            Status = RankingStatus.Loading;
            identity.MemberLeaderboard(Mode, TodayOnly, reply =>
            {
                // A later tab change wins over an older reply.
                if (mine != request) return;
                if (reply == null || !reply.ok) { Status = RankingStatus.Failed; return; }
                Status = RankingStatus.Ready;
                if (reply.entries != null) foreach (var e in reply.entries) if (e != null) Rows.Add(new RankingRow { Rank = e.rank, Name = e.name ?? "", Score = e.score, Member = true });
                if (reply.me != null && reply.me.rank > 0) Mine = new RankingRow { Rank = reply.me.rank, Name = "내 기록", Score = reply.me.score, Member = true };
            });
        }

        public string StatusText => Status == RankingStatus.Loading ? LoadingText : Status == RankingStatus.Failed ? OfflineText : "";

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

        // Calorie panel lines (label, value): this run, then a member's today and lifetime totals once /stats answered.
        public List<KeyValuePair<string, string>> KcalLines(RunSession session)
        {
            var lines = new List<KeyValuePair<string, string>>();
            bool camera = PlayStyleInfo.UsesCamera(data?.Player?.Style ?? PlayStyle.Keyboard);
            // Keyboard runs keep the existing note: this run's calories are not counted.
            lines.Add(new KeyValuePair<string, string>("이번 판", camera && session != null ? Kcal(session.Workout.EstimatedKcal) : "키보드 제외"));
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
