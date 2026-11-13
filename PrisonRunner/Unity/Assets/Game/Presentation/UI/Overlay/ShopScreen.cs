using System;
using System.Globalization;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Item shop: a member's coins unlock items (bought on the account server) that then appear on the map.
    // Guests see the items and their effects without a buy button.
    internal sealed class ShopScreen
    {
        public const string GuestNote = "회원가입하면 코인을 모아 아이템을 해금할 수 있어요";
        static readonly CultureInfo Num = CultureInfo.InvariantCulture;
        readonly OverlaySkin skin;
        string message = "";
        bool failed, buying;
        int opened;
        GUIStyle wrapped;
        public ShopScreen(OverlaySkin overlaySkin) { skin = overlaySkin; }
        public void Open() { message = ""; failed = buying = false; opened++; }

        public void Draw(IHudData data, Action back)
        {
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Ground, 0.9f));
            GUI.matrix = oldMatrix;

            float x = 230, w = 820;
            skin.Text(new Rect(x, 36, 300, 48), "상점", 44, Palette.Cream, TextAnchor.MiddleLeft);
            var wallet = data?.Wallet;
            bool developer = data != null && data.DeveloperMode;
            bool guest = !developer && !(data is IIdentityData identity && identity.SignedIn);
            var coins = new Rect(x + w - 190, 38, 190, 44);
            skin.BorderBox(coins, Palette.A(Palette.Ground, 0.9f), Palette.Wood, 2);
            skin.Circle(new Rect(coins.x + 10, coins.y + 9, 26, 26), Palette.Gold);
            skin.Text(new Rect(coins.x + 44, coins.y, coins.width - 58, coins.height), (wallet != null ? wallet.Coins : 0).ToString("N0", Num), 20, Palette.Cream, TextAnchor.MiddleRight);

            skin.Text(new Rect(x, 100, w, 26), wallet != null && wallet.UnlockAll
                ? "개발자 모드에서는 모든 아이템이 해금되어 있습니다."
                : guest ? GuestNote : "달리면서 모은 코인으로 아이템을 해금하면 맵에 등장합니다.", 16, guest ? Palette.Gold : Palette.Sand, TextAnchor.MiddleLeft, false, true);

            float cardW = (w - 32) / 3f;
            for (int i = 0; i < ItemCatalog.All.Length; i++)
            {
                var item = ItemCatalog.All[i];
                var r = new Rect(x + i * (cardW + 16), 150, cardW, 400);
                bool owned = wallet != null && wallet.IsUnlocked(item.Kind);
                skin.BorderBox(r, Palette.A(Palette.Panel, 0.95f), owned ? Palette.Gold : Palette.Wood, 2);
                skin.Circle(new Rect(r.center.x - 48, r.y + 34, 96, 96), Palette.A(Accent(item.Kind), 0.9f));
                skin.Text(new Rect(r.x, r.y + 150, r.width, 36), item.Name, 28, Palette.Cream, TextAnchor.MiddleCenter);
                // Descriptions can need two lines inside the card.
                if (wrapped == null) wrapped = new GUIStyle(skin.Style(16, false, TextAnchor.MiddleCenter, true)) { wordWrap = true };
                var oldColor = GUI.color; GUI.color = Palette.Sand;
                GUI.Label(new Rect(r.x + 14, r.y + 188, r.width - 28, 48), item.Description, wrapped);
                GUI.color = oldColor;
                skin.Text(new Rect(r.x, r.y + 246, r.width, 24), item.Seconds.ToString("0", Num) + "초 지속", 16, Palette.Gold, TextAnchor.MiddleCenter, false, true);
                var button = new Rect(r.x + 24, r.yMax - 82, r.width - 48, 56);
                if (owned)
                {
                    skin.BorderBox(button, Palette.A(Palette.Ground, 0.9f), Palette.Gold, 2);
                    skin.Text(button, "보유 중", 20, Palette.Gold, TextAnchor.MiddleCenter);
                }
                else if (guest)
                {
                    // Price only: guests collect no coins, so there is nothing to press.
                    skin.BorderBox(button, Palette.A(Palette.Ground, 0.6f), Palette.WoodDim, 2);
                    skin.Text(new Rect(button.x, button.y + 4, button.width, 26), item.UnlockCost.ToString("N0", Num) + "코인", 18, Palette.Muted, TextAnchor.MiddleCenter);
                    skin.Text(new Rect(button.x, button.y + 28, button.width, 24), "회원 전용", 13, Palette.Muted, TextAnchor.MiddleCenter, false, true);
                }
                else
                {
                    int have = wallet != null ? wallet.Coins : 0;
                    string price = item.UnlockCost.ToString("N0", Num) + "코인";
                    if (have >= item.UnlockCost)
                    {
                        bool wasEnabled = GUI.enabled; GUI.enabled = wasEnabled && !buying;
                        if (skin.Button(button, "해금  " + price, Palette.Gold, Color.clear, Palette.Outline, 20)) Buy(data, item);
                        GUI.enabled = wasEnabled;
                    }
                    else
                    {
                        // Not a button: nothing happens until enough coins are collected, so say how many are missing.
                        skin.BorderBox(button, Palette.A(Palette.Ground, 0.6f), Palette.WoodDim, 2);
                        skin.Text(new Rect(button.x, button.y + 4, button.width, 26), price, 18, Palette.Muted, TextAnchor.MiddleCenter);
                        skin.Text(new Rect(button.x, button.y + 28, button.width, 24), (item.UnlockCost - have).ToString("N0", Num) + "코인 더 필요", 13, Palette.Muted, TextAnchor.MiddleCenter, false, true);
                    }
                }
            }
            skin.Text(new Rect(x, 558, w, 30), buying ? "해금 중…" : message, 16, failed ? Palette.Error : Palette.Gold, TextAnchor.MiddleCenter, false, true);

            if (skin.Button(new Rect(540, 606, 200, 56), "뒤로", Palette.A(Palette.Ground, 0.9f), Palette.Wood, Palette.Cream, 22)) back?.Invoke();
        }

        // The server's balance and unlocked items replace the session's on success; a refusal shows its reason.
        void Buy(IHudData data, ItemDefinition item)
        {
            int mine = opened; buying = true; message = ""; failed = false;
            data.Unlock(item.Kind, (ok, reason) => { if (mine != opened) return; buying = false; failed = !ok; message = ok ? item.Name + " 해금 완료! 다음 판부터 맵에 나옵니다." : reason; });
        }

        static Color Accent(ItemKind kind) =>
            kind == ItemKind.Magnet ? new Color(.75f, .2f, .18f) : kind == ItemKind.DoubleScore ? new Color(.9f, .68f, .12f) : new Color(.2f, .5f, .85f);
    }
}
