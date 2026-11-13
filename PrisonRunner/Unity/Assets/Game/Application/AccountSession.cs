using System;
using System.Collections.Generic;
using System.Globalization;
using Muhanok.Domain;

namespace Muhanok.Application
{
    public enum AccountKind { Guest, Member }

    // Settings that follow the player: the member's server profile, or session memory for a guest.
    [Serializable]
    public sealed class AccountSettings
    {
        public const double DefaultWeight = 65, DefaultGoal = 50;
        public double Weight = DefaultWeight;
        public PlayStyle Style = PlayStyle.FullBody;
        public double Goal = DefaultGoal;
        public AccountSettings Clone() => (AccountSettings)MemberwiseClone();
    }

    // Who plays on this PC right now. A guest keeps everything in memory; a member mirrors the server profile.
    // Signing out (or quitting) always returns to a fresh guest; there is no automatic login.
    public sealed class AccountSession
    {
        public const string GuestName = "게스트";
        public const int MaxNickname = 10;
        private readonly PlayerWallet wallet = new PlayerWallet();
        private AccountSettings settings = new AccountSettings();
        private PlayerProfile profile;
        public AccountKind Kind { get; private set; } = AccountKind.Guest;
        public bool IsMember => Kind == AccountKind.Member;
        public string Id { get; private set; } = "";
        public string Token { get; private set; } = "";
        public string Nickname { get; private set; } = GuestName;
        public AccountSettings Settings => settings.Clone();
        public int Coins => wallet.Coins;
        public IEnumerable<ItemKind> Unlocked => wallet.Unlocked;
        // Read-only view for the screens (coins and unlocked items); a guest's wallet is always empty.
        public PlayerWallet Wallet => wallet;
        public bool TutorialDone { get; private set; }
        public event Action Changed;

        public void SignInMember(AccountProfile account, string token)
        {
            if (account == null || string.IsNullOrEmpty(account.id)) return;
            Kind = AccountKind.Member; Id = account.id; Token = token ?? "";
            Load(account); Notify();
        }
        // Refreshes the member from a server reply; replies for another account are ignored.
        public bool Apply(AccountProfile account)
        {
            if (!IsMember || account == null || account.id != Id) return false;
            Load(account); Notify(); return true;
        }
        public void SetCoins(int coins)
        {
            if (!IsMember) return;
            wallet.Restore(coins, new List<ItemKind>(wallet.Unlocked)); Notify();
        }
        public void UpdateSettings(AccountSettings value)
        {
            if (value == null) return;
            settings = Sanitize(value.Weight, (int)value.Style, value.Goal); Notify();
        }
        // A guest renames in memory; a member only after the server accepted the name.
        public void SetNickname(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            Nickname = name.Trim(); Notify();
        }
        public void SetTutorialDone(bool done) { TutorialDone = done; Notify(); }
        public void SignOut()
        {
            Kind = AccountKind.Guest; Id = Token = ""; Nickname = GuestName; TutorialDone = false;
            settings = new AccountSettings(); wallet.Restore(0, null); Notify();
        }

        // The account as the existing screens see it: name, weight text, default style and calorie goal.
        public PlayerProfile Profile => profile ??= new PlayerProfile
        {
            Name = Nickname, Weight = settings.Weight.ToString("0.##", CultureInfo.InvariantCulture), Style = settings.Style, CalorieGoal = settings.Goal,
        };
        // Items placed on the map: what a member unlocked in the shop, nothing for a guest.
        public List<ItemKind> RunItems()
        {
            var list = new List<ItemKind>();
            if (!IsMember) return list;
            foreach (var item in ItemCatalog.All) if (wallet.IsUnlocked(item.Kind)) list.Add(item.Kind);
            return list;
        }
        // Server nickname rule (storage.clean_name): 1-10 code points after trimming (Python len()), and no character of
        // a Unicode "C" category (control, format such as U+200B, private use, unassigned, lone surrogate); null when acceptable.
        public static string NicknameProblem(string name)
        {
            string text = name?.Trim() ?? "";
            if (text.Length == 0) return "닉네임을 입력해 주세요.";
            if (CodePoints(text) > MaxNickname) return "닉네임은 " + MaxNickname + "자 이하로 입력해 주세요.";
            for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
                if (Forbidden(CharUnicodeInfo.GetUnicodeCategory(text, i))) return "닉네임에 쓸 수 없는 문자가 있습니다.";
            return null;
        }
        // Text fields count UTF-16 units; room for 10 code points of two units each, the rule above decides.
        public const int MaxNicknameInput = MaxNickname * 2;
        // A surrogate pair (an emoji) is one character, as on the server.
        public static int CodePoints(string text)
        {
            int count = 0;
            for (int i = 0; i < (text?.Length ?? 0); i += char.IsSurrogatePair(text, i) ? 2 : 1) count++;
            return count;
        }
        static bool Forbidden(UnicodeCategory c) => c == UnicodeCategory.Control || c == UnicodeCategory.Format || c == UnicodeCategory.PrivateUse
            || c == UnicodeCategory.OtherNotAssigned || c == UnicodeCategory.Surrogate;
        // Profile screen values; an empty weight means the 65 kg default.
        public static AccountSettings SettingsOf(PlayerProfile value)
        {
            if (value == null) return new AccountSettings();
            double weight = double.TryParse(value.Weight, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double kg) ? kg : AccountSettings.DefaultWeight;
            return Sanitize(weight, (int)value.Style, value.CalorieGoal);
        }

        private void Load(AccountProfile account)
        {
            Nickname = string.IsNullOrWhiteSpace(account.name) ? GuestName : account.name.Trim();
            settings = Sanitize(account.weight, account.style, account.goal);
            TutorialDone = account.tutorial_done;
            var kinds = new List<ItemKind>();
            if (account.unlocked != null) foreach (int id in account.unlocked) if (Enum.IsDefined(typeof(ItemKind), id)) kinds.Add((ItemKind)id);
            wallet.Restore(account.coins, kinds);
        }
        private static AccountSettings Sanitize(double weight, int style, double goal) => new AccountSettings
        {
            Weight = WorkoutEstimate.ValidWeight(weight) ? weight : AccountSettings.DefaultWeight,
            Style = style >= 0 && style <= 2 ? (PlayStyle)style : PlayStyle.FullBody,
            Goal = double.IsNaN(goal) || goal < 1 || goal > 5000 ? AccountSettings.DefaultGoal : goal,
        };
        private void Notify() { profile = null; Changed?.Invoke(); }
    }
}
