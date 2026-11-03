using System;
using System.Globalization;
using Muhanok.Application;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    public enum SignUpStep { Consent, Nickname, Settings, Capture }

    // Sign-up steps without drawing: consent, nickname (server check), weight and style (skippable), face capture.
    // A failed capture stays on the capture step and keeps every earlier answer.
    public sealed class SignUpFlow
    {
        public SignUpStep Step { get; private set; }
        // The nickname the server accepted; the capture step enrolls with it.
        public string Nickname { get; private set; } = "";
        public string NameMessage { get; private set; } = "";
        public bool Checking { get; private set; }
        public string Validation { get; private set; } = "";
        public AccountSettings Settings { get; private set; } = new AccountSettings();
        private IIdentityData identity;
        private int ticket;

        public void Open(IIdentityData account)
        {
            identity = account; Step = SignUpStep.Consent; Nickname = NameMessage = Validation = ""; Checking = false; Settings = new AccountSettings(); ticket++;
        }
        public void Agree() { if (Step == SignUpStep.Consent) Step = SignUpStep.Nickname; }
        public void SubmitNickname(string name)
        {
            if (Step != SignUpStep.Nickname || Checking) return;
            string problem = AccountSession.NicknameProblem(name);
            if (problem != null) { NameMessage = problem; return; }
            if (identity == null) return;
            string trimmed = name.Trim(); int mine = ++ticket;
            Checking = true; NameMessage = "닉네임 확인 중…";
            identity.CheckNickname(trimmed, (available, reason) =>
            {
                // A reply after leaving the step (back, reopen) is ignored.
                if (mine != ticket || Step != SignUpStep.Nickname) return;
                Checking = false;
                if (available) { Nickname = trimmed; NameMessage = ""; Step = SignUpStep.Settings; }
                else NameMessage = string.IsNullOrEmpty(reason) ? "사용할 수 없는 닉네임입니다." : reason;
            });
        }
        // Empty weight means 65 kg; a typed weight must be 10-350 kg so the member can always start a run.
        public bool SubmitSettings(string weight, PlayStyle style)
        {
            if (Step != SignUpStep.Settings) return false;
            string text = weight?.Trim() ?? "";
            if (!new WorkoutEstimate().SetWeight(text)) { Validation = "몸무게는 10~350kg 숫자로 입력해 주세요."; return false; }
            double kg = text.Length == 0 ? AccountSettings.DefaultWeight : double.Parse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            Settings = new AccountSettings { Weight = kg, Style = style }; Validation = ""; Step = SignUpStep.Capture; return true;
        }
        public void SkipSettings() { if (Step != SignUpStep.Settings) return; Settings = new AccountSettings(); Validation = ""; Step = SignUpStep.Capture; }
        public void Capture(Action<bool> done)
        {
            if (Step != SignUpStep.Capture || identity == null || identity.IdentityBusy) return;
            identity.FaceEnroll(Nickname, Settings.Clone(), done);
        }
        // One step back; false on the consent step (the screen then leaves sign-up).
        public bool Back()
        {
            ticket++; Checking = false; Validation = "";
            if (Step == SignUpStep.Consent) return false;
            Step = (SignUpStep)((int)Step - 1); return true;
        }
        public static string Summary(string nickname, AccountSettings settings) =>
            "닉네임 " + nickname + ", 몸무게 " + settings.Weight.ToString("0.##", CultureInfo.InvariantCulture) + "kg, " + PlayStyleInfo.Label(settings.Style);
    }

    // Draws the sign-up steps; the capture step is the shared face capture screen.
    internal sealed class SignUpScreen
    {
        private static readonly string[] StepNames = { "저장 동의", "닉네임", "몸무게·방식", "얼굴 등록" };
        private readonly HudSkin skin;
        private readonly IdentityScreen capture;
        private string nameText = "", weightText = "";
        private PlayStyle style = PlayStyle.FullBody;
        private bool focusName;
        private GUIStyle paragraph;
        public SignUpScreen(HudSkin hudSkin, IdentityScreen captureScreen) { skin = hudSkin; capture = captureScreen; }
        public void Open() { nameText = weightText = ""; style = PlayStyle.FullBody; focusName = true; }

        // enroll: starts the face capture; leave: back to the login gate.
        public void Draw(IHudData data, IIdentityData identity, SignUpFlow flow, Action enroll, Action leave)
        {
            if (flow.Step == SignUpStep.Capture)
            {
                capture.Draw(data, identity, "회원가입: 얼굴 등록",
                    SignUpFlow.Summary(flow.Nickname, flow.Settings) + "\n정면을 보고 틀 안에 얼굴을 맞춘 뒤 등록하세요.\n촬영에 실패해도 입력한 내용은 그대로 남습니다.",
                    "얼굴 등록하기", enroll, () => flow.Back());
                return;
            }
            paragraph ??= new GUIStyle(skin.Style(22, FontStyle.Normal)) { wordWrap = true };
            skin.Card(new Rect(0, 0, 1280, 720), Palette.A(Palette.Ground, .9f));
            skin.Panel(new Rect(150, 60, 980, 600), .97f);
            GUI.color = Palette.Gold; GUI.Label(new Rect(195, 82, 500, 50), "회원가입", skin.Style(36, FontStyle.Bold)); GUI.color = Color.white;
            for (int i = 0; i < StepNames.Length; i++)
            {
                bool on = i == (int)flow.Step;
                GUI.color = on ? Palette.Gold : new Color(1, 1, 1, i < (int)flow.Step ? .8f : .4f);
                GUI.Label(new Rect(195 + i * 225, 140, 220, 30), (i + 1) + ". " + StepNames[i], skin.Style(18, on ? FontStyle.Bold : FontStyle.Normal));
            }
            GUI.color = Color.white;
            var body = new Rect(195, 190, 890, 330);
            if (flow.Step == SignUpStep.Consent) Consent(body, flow);
            else if (flow.Step == SignUpStep.Nickname) Nickname(body, flow);
            else Settings(body, flow);
            GUI.color = new Color(1, 1, 1, .7f);
            GUI.Label(new Rect(195, 610, 700, 30), GesturePointer.Hint("Enter를 누르세요."), skin.Style(16, FontStyle.Normal)); GUI.color = Color.white;
            if (skin.FlatButton(new Rect(940, 600, 150, 46), flow.Step == SignUpStep.Consent ? "뒤로" : "이전", false) && !flow.Back()) leave?.Invoke();
        }
        private void Consent(Rect r, SignUpFlow flow)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 200), "연결된 얼굴 서버에 얼굴 특징값, 닉네임, 설정(몸무게·기본 방식), 플레이 기록을 저장합니다.\n얼굴 사진은 저장하지 않습니다.\n가입 후에는 이름 입력 없이 얼굴로 로그인하고, 게임을 끄면 로그아웃됩니다.", paragraph);
            GUI.color = new Color(1, 1, 1, .75f);
            GUI.Label(new Rect(r.x, r.y + 210, r.width, 60), "전시용 얼굴 로그인입니다. 사진·영상 위조를 차단하는 보안 인증 기능은 포함하지 않습니다.", skin.Style(17, FontStyle.Normal)); GUI.color = Color.white;
            if (skin.PrimaryButton(new Rect(r.x, 540, 450, 58), "동의하고 계속") || IdentityScreen.Enter()) { flow.Agree(); focusName = true; }
        }
        private void Nickname(Rect r, SignUpFlow flow)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 40), "랭킹과 메뉴에 표시될 닉네임 (1~10자)", skin.Label);
            bool enter = IdentityScreen.Enter();
            skin.Panel(new Rect(r.x, r.y + 50, 420, 56), 1f);
            GUI.SetNextControlName("MongscapeSignUpName");
            bool wasEnabled = GUI.enabled; GUI.enabled = wasEnabled && !flow.Checking;
            nameText = GUI.TextField(new Rect(r.x, r.y + 50, 420, 56), nameText, AccountSession.MaxNicknameInput, skin.Input);
            GUI.enabled = wasEnabled;
            if (focusName && Event.current.type == EventType.Repaint) { GUI.FocusControl("MongscapeSignUpName"); focusName = false; }
            GUI.color = flow.Checking ? Color.white : Palette.Error;
            GUI.Label(new Rect(r.x, r.y + 120, r.width, 34), flow.NameMessage, skin.Small); GUI.color = Color.white;
            GUI.enabled = wasEnabled && !flow.Checking;
            if (skin.PrimaryButton(new Rect(r.x, 540, 450, 58), flow.Checking ? "확인 중…" : "중복 확인 후 다음") || enter && !flow.Checking) flow.SubmitNickname(nameText);
            GUI.enabled = wasEnabled;
        }
        private void Settings(Rect r, SignUpFlow flow)
        {
            GUI.Label(new Rect(r.x, r.y, 400, 34), "몸무게 (선택)", skin.Label);
            bool enter = IdentityScreen.Enter();
            skin.Panel(new Rect(r.x, r.y + 40, 300, 52), 1f); GUI.SetNextControlName("MongscapeSignUpWeight");
            weightText = GUI.TextField(new Rect(r.x, r.y + 40, 300, 52), weightText, 5, skin.Input);
            GUI.Label(new Rect(r.x + 315, r.y + 47, 60, 40), "kg", skin.Label);
            GUI.Label(new Rect(r.x + 390, r.y + 50, 500, 34), "비우면 65kg으로 계산합니다. 10~350kg", skin.Small);
            GUI.Label(new Rect(r.x, r.y + 120, 400, 34), "기본 플레이 방식", skin.Label);
            for (int i = 0; i < 3; i++)
            {
                var option = (PlayStyle)i; var rect = new Rect(r.x + i * 136, r.y + 160, 128, 48);
                skin.Card(rect, option == style ? Palette.Gold : Palette.Panel);
                GUI.color = option == style ? Palette.Outline : Palette.Cream;
                bool pressed = GUI.Button(rect, GUIContent.none, GUIStyle.none) || GesturePointer.Activate(rect, PlayStyleInfo.Label(option));
                GUI.Label(rect, PlayStyleInfo.Label(option), skin.Centered(22, FontStyle.Bold)); GUI.color = Color.white;
                if (pressed) style = option;
            }
            GUI.color = Palette.Error; GUI.Label(new Rect(r.x, r.y + 230, r.width, 34), flow.Validation, skin.Small); GUI.color = Color.white;
            if (skin.PrimaryButton(new Rect(r.x, 540, 300, 58), "다음") || enter) flow.SubmitSettings(weightText, style);
            if (skin.FlatButton(new Rect(r.x + 320, 540, 300, 58), "건너뛰기 (65kg, 전신)", false)) { weightText = ""; style = PlayStyle.FullBody; flow.SkipSettings(); }
        }
    }
}
