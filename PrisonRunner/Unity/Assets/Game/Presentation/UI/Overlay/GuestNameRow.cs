using Muhanok.Application;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Result screen for a guest: the run is already on this PC's ranking as "게스트"; [저장] renames it, [건너뛰기] keeps it.
    // Developer mode and members never see it (their runs are named already).
    internal sealed class GuestNameRow
    {
        readonly OverlaySkin skin;
        string text = "", message = "";
        bool focus;
        // Called after a rename so the result's 이 PC board shows the new name at once.
        readonly System.Action renamed;
        public bool Open { get; private set; }
        public GuestNameRow(OverlaySkin overlaySkin, System.Action onRenamed = null) { skin = overlaySkin; renamed = onRenamed; }

        public void Show(IHudData data) { Open = data != null && data.CanNameLastRun; text = message = ""; focus = Open; }
        public void Skip() { Open = false; }
        // False (with the reason shown) when the name breaks the nickname rule or the run is gone.
        public bool Save(IHudData data, string name)
        {
            if (!Open || data == null) return false;
            string problem = AccountSession.NicknameProblem(name);
            if (problem != null) { message = problem; return false; }
            if (!data.NameLastRun(name)) { message = "기록 이름을 바꾸지 못했습니다."; return false; }
            Open = false; renamed?.Invoke(); return true;
        }

        public void Draw(IHudData data, Rect r)
        {
            if (!Open) return;
            // Enter saves and Escape skips; read before the text field takes the keys.
            var e = Event.current; bool enter = false, escape = false;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) { enter = true; e.Use(); }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { escape = true; e.Use(); }
            skin.BorderBox(r, Palette.A(Palette.Panel, 0.97f), Palette.Gold, 2);
            skin.Text(new Rect(r.x + 24, r.y + 14, r.width - 48, 34), "이 PC 랭킹에 남길 닉네임", 24, Palette.Gold, TextAnchor.MiddleLeft);
            skin.Text(new Rect(r.x + 24, r.y + 48, r.width - 48, 24), "최대 " + AccountSession.MaxNickname + "자. 건너뛰면 \"" + AccountSession.GuestName + "\"로 남습니다.", 15, Palette.Sand, TextAnchor.MiddleLeft, false, true);
            var field = new Rect(r.x + 24, r.y + 84, r.width - 300, 52);
            GUI.SetNextControlName("MongscapeGuestName");
            text = skin.Input(field, text, AccountSession.MaxNicknameInput);
            if (focus && e.type == EventType.Repaint) { GUI.FocusControl("MongscapeGuestName"); focus = false; }
            if (skin.Button(new Rect(field.xMax + 12, field.y, 110, 52), "저장", Palette.Gold, Color.clear, Palette.Outline, 20) || enter) Save(data, text);
            if (skin.Button(new Rect(field.xMax + 134, field.y, 130, 52), "건너뛰기", Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 20) || escape) Skip();
            skin.Text(new Rect(r.x + 24, r.y + 144, r.width - 48, 26), message, 15, Palette.Error, TextAnchor.MiddleLeft, false, true);
        }
    }
}
