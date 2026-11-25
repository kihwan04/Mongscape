using System;
using System.Globalization;
using Muhanok.Application;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    public enum ProfileDeleteStep { None, Ask, Confirm }

    // 내 정보 without drawing: nickname and settings saved on the account server, server totals, and a two-step deletion.
    public sealed class ProfileForm
    {
        static readonly CultureInfo Num = CultureInfo.InvariantCulture;
        public string Nickname = "", Weight = "", Goal = "";
        public PlayStyle Style = PlayStyle.FullBody;
        public string Message { get; private set; } = "";
        public bool Failed { get; private set; }
        public bool Busy { get; private set; }
        // Null until /stats answered (or when it failed).
        public StatsReply Stats { get; private set; }
        public string StatsNote { get; private set; } = "";
        public ProfileDeleteStep Deleting { get; private set; }
        private IIdentityData identity;
        private int opened;

        public void Open(IHudData data, IIdentityData account)
        {
            identity = account; int mine = ++opened;
            Busy = Failed = false; Message = ""; Deleting = ProfileDeleteStep.None; Stats = null;
            var profile = data?.SavedProfile ?? new PlayerProfile();
            Nickname = profile.Name; Weight = profile.Weight; Style = profile.Style; Goal = profile.CalorieGoal.ToString("0.##", Num);
            StatsNote = "기록을 불러오는 중…";
            if (identity == null) { StatsNote = ""; return; }
            // Replies for an earlier opening are ignored.
            identity.MemberStats(reply => { if (mine != opened) return; Stats = reply != null && reply.ok ? reply : null; StatsNote = Stats != null ? "" : "기록을 불러오지 못했습니다."; });
        }
        public void Save()
        {
            if (Busy || identity == null) return;
            string problem = AccountSession.NicknameProblem(Nickname);
            if (problem != null) { Fail(problem); return; }
            var probe = new WorkoutEstimate();
            if (!probe.SetWeight(Weight?.Trim())) { Fail("몸무게는 10~350kg 숫자로 입력해 주세요."); return; }
            if (!probe.SetGoal(Goal?.Trim())) { Fail("칼로리 목표는 1~5000 kcal 숫자로 입력해 주세요."); return; }
            var settings = new AccountSettings { Weight = probe.WeightKg, Style = Style, Goal = probe.GoalKcal };
            int mine = opened; Busy = true; Failed = false; Message = "저장 중…"; Deleting = ProfileDeleteStep.None;
            identity.SaveMemberProfile(Nickname.Trim(), settings, (ok, reason) =>
            {
                if (mine != opened) return;
                Busy = false; Failed = !ok; Message = ok ? "저장했습니다." : string.IsNullOrEmpty(reason) ? "저장하지 못했습니다." : reason;
                if (ok) Nickname = Nickname.Trim();
            });
        }
        // 탈퇴 asks twice; the press after the second question deletes. deleted runs once the server removed the account.
        public void RequestDelete(Action deleted)
        {
            if (Busy || identity == null) return;
            if (Deleting != ProfileDeleteStep.Confirm) { Deleting = Deleting == ProfileDeleteStep.None ? ProfileDeleteStep.Ask : ProfileDeleteStep.Confirm; Message = ""; Failed = false; return; }
            int mine = opened; Busy = true; Failed = false; Message = "탈퇴 처리 중…";
            identity.DeleteAccount((ok, reason) =>
            {
                if (mine != opened) return;
                Busy = false; Deleting = ProfileDeleteStep.None;
                if (ok) { Message = ""; deleted?.Invoke(); } else { Failed = true; Message = string.IsNullOrEmpty(reason) ? "탈퇴하지 못했습니다." : reason; }
            });
        }
        public void CancelDelete() { if (!Busy) Deleting = ProfileDeleteStep.None; }
        private void Fail(string reason) { Failed = true; Message = reason; }
    }

    // 내 정보 (members only): nickname, weight, default style and calorie goal, server totals, logout and account deletion.
    internal sealed class ProfileScreen
    {
        static readonly CultureInfo Num = CultureInfo.InvariantCulture;
        readonly OverlaySkin skin;
        bool focusName;

        public ProfileScreen(OverlaySkin overlaySkin) { skin = overlaySkin; }
        public void Open() { focusName = true; }

        // back: main menu without saving; logout: sign out to the login gate; delete: the next deletion step.
        public void Draw(ProfileForm form, Action back, Action logout, Action delete)
        {
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Ground, 0.9f));
            GUI.matrix = oldMatrix;

            // Enter saves, Escape cancels a deletion question or goes back; read before the text fields take the keys.
            var e = Event.current; bool enter = false, escape = false;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) { enter = true; e.Use(); }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { escape = true; e.Use(); }

            float x = 230, w = 820;
            bool asking = form.Deleting != ProfileDeleteStep.None;
            skin.Text(new Rect(x, 36, 600, 48), "내 정보", 44, Palette.Cream, TextAnchor.MiddleLeft);
            skin.Text(new Rect(x, 88, w, 24), "닉네임과 설정은 회원 계정에 저장됩니다. 닉네임은 다른 회원과 겹칠 수 없습니다.", 15, Palette.Sand, TextAnchor.MiddleLeft, false, true);

            bool wasEnabled = GUI.enabled; GUI.enabled = wasEnabled && !form.Busy && !asking;
            skin.Text(new Rect(x, 130, 300, 28), "닉네임 (1~10자)", 18, Palette.Sand, TextAnchor.MiddleLeft, false, true);
            GUI.SetNextControlName("MongscapeProfileName");
            form.Nickname = skin.Input(new Rect(x, 162, 400, 52), form.Nickname, AccountSession.MaxNicknameInput);
            if (focusName && e.type == EventType.Repaint) { GUI.FocusControl("MongscapeProfileName"); focusName = false; }
            skin.Text(new Rect(x, 232, 200, 28), "몸무게", 18, Palette.Sand, TextAnchor.MiddleLeft, false, true);
            GUI.SetNextControlName("MongscapeProfileWeight");
            form.Weight = skin.Input(new Rect(x, 264, 150, 52), form.Weight, 6);
            skin.Text(new Rect(x + 158, 264, 50, 52), "kg", 20, Palette.Sand, TextAnchor.MiddleLeft);
            skin.Text(new Rect(x + 220, 232, 200, 28), "칼로리 목표", 18, Palette.Sand, TextAnchor.MiddleLeft, false, true);
            GUI.SetNextControlName("MongscapeProfileGoal");
            form.Goal = skin.Input(new Rect(x + 220, 264, 120, 52), form.Goal, 6);
            skin.Text(new Rect(x + 348, 264, 70, 52), "kcal", 20, Palette.Sand, TextAnchor.MiddleLeft);
            skin.Text(new Rect(x, 334, 300, 28), "기본 플레이 방식", 18, Palette.Sand, TextAnchor.MiddleLeft, false, true);
            for (int i = 0; i < 3; i++)
            {
                var option = (PlayStyle)i;
                var r = new Rect(x + i * 136, 366, 128, 48);
                bool on = option == form.Style;
                if (skin.Button(r, PlayStyleInfo.Label(option), on ? Palette.Gold : Palette.A(Palette.Ground, 0.9f), on ? Color.clear : Palette.Wood, on ? Palette.Outline : Palette.Cream, 20))
                    form.Style = option;
            }
            GUI.enabled = wasEnabled;
            skin.Text(new Rect(x, 426, 420, 26), form.Message, 16, form.Failed ? Palette.Error : Palette.Sand, TextAnchor.MiddleLeft, false, true);

            DrawStats(form, new Rect(x + 450, 130, w - 450, 300));
            if (asking) DrawDeleteQuestion(form, new Rect(x, 462, w, 128), delete, escape);

            GUI.enabled = wasEnabled && !form.Busy && !asking;
            if (skin.Button(new Rect(242, 606, 190, 56), "저장", Palette.Gold, Color.clear, Palette.Outline, 22) || enter && GUI.enabled) form.Save();
            if (skin.Button(new Rect(444, 606, 190, 56), "로그아웃", Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 22)) logout?.Invoke();
            if (skin.Button(new Rect(646, 606, 190, 56), "회원 탈퇴", Palette.A(Palette.Ground, 0.9f), new Color(.75f, .25f, .18f), new Color(1, .7f, .6f), 22)) delete?.Invoke();
            GUI.enabled = wasEnabled && !form.Busy;
            if (skin.Button(new Rect(848, 606, 190, 56), "뒤로", Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 22) || escape && !asking && GUI.enabled) back?.Invoke();
            GUI.enabled = wasEnabled;
        }

        // Two questions before the account is deleted; each has its own button so one long hand point cannot pass both.
        void DrawDeleteQuestion(ProfileForm form, Rect r, Action delete, bool escape)
        {
            bool last = form.Deleting == ProfileDeleteStep.Confirm;
            skin.BorderBox(r, Palette.A(new Color(.3f, .07f, .05f), 0.96f), new Color(.85f, .3f, .2f), 2);
            skin.Text(new Rect(r.x + 24, r.y + 12, r.width - 48, 30), last ? "정말 탈퇴할까요? 되돌릴 수 없습니다." : "탈퇴하면 얼굴 특징값, 닉네임, 설정, 코인과 아이템, 플레이 기록이 모두 삭제됩니다.", 18, Palette.Cream, TextAnchor.MiddleLeft, false, true);
            skin.Text(new Rect(r.x + 24, r.y + 42, r.width - 48, 24), last ? "확인 2/2" : "확인 1/2: 계속하려면 [계속]을 누르세요.", 14, Palette.Sand, TextAnchor.MiddleLeft, false, true);
            bool wasEnabled = GUI.enabled; GUI.enabled = wasEnabled && !form.Busy;
            var go = last ? new Rect(r.x + 24, r.y + 70, 240, 48) : new Rect(r.xMax - 264, r.y + 70, 240, 48);
            if (skin.Button(go, last ? "탈퇴하기" : "계속", new Color(.7f, .2f, .14f), Color.clear, Palette.Cream, 20)) delete?.Invoke();
            var cancel = last ? new Rect(r.xMax - 264, r.y + 70, 240, 48) : new Rect(r.x + 24, r.y + 70, 240, 48);
            if (skin.Button(cancel, "취소", Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 20) || escape && GUI.enabled) form.CancelDelete();
            GUI.enabled = wasEnabled;
        }

        void DrawStats(ProfileForm form, Rect r)
        {
            skin.BorderBox(r, Palette.A(Palette.Panel, 0.95f), Palette.Wood, 2);
            skin.Text(new Rect(r.x + 24, r.y + 16, r.width - 48, 34), "내 기록", 24, Palette.Gold, TextAnchor.MiddleLeft);
            var stats = form.Stats;
            if (stats == null) { skin.Text(new Rect(r.x + 24, r.y + 70, r.width - 48, 40), form.StatsNote, 16, Palette.Sand, TextAnchor.MiddleLeft, false, true); return; }
            string[] labels = { "오늘 소모 칼로리", "누적 소모 칼로리", "전신 최고 점수", "상체 최고 점수", "키보드 최고 점수" };
            string[] values = { stats.today_kcal.ToString("N0", Num) + "kcal", stats.total_kcal.ToString("N0", Num) + "kcal", Best(stats, 0), Best(stats, 1), Best(stats, 2) };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = r.y + 62 + i * 46;
                skin.Text(new Rect(r.x + 24, y, 200, 40), labels[i], 17, Palette.Sand, TextAnchor.MiddleLeft, false, true);
                skin.Text(new Rect(r.xMax - 184, y, 160, 40), values[i], 22, Palette.Cream, TextAnchor.MiddleRight);
            }
        }
        static string Best(StatsReply stats, int mode) => stats.best_score != null && mode < stats.best_score.Length && stats.best_score[mode] > 0 ? stats.best_score[mode].ToString("N0", Num) + "점" : "-";
    }
}
