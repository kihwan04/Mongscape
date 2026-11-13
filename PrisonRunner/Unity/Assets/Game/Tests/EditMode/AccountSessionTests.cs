using System.Collections.Generic;
using Muhanok.Application;
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class AccountSessionTests
    {
        private static AccountProfile Member() => new AccountProfile
        {
            id = "member-1", name = " 민수 ", weight = 72, style = (int)PlayStyle.UpperBody, goal = 120, coins = 90,
            unlocked = new[] { (int)ItemKind.Magnet, 99 }, tutorial_done = true,
        };

        [Test]
        public void StartsAsGuestWithDefaultSettingsAndNoItems()
        {
            var session = new AccountSession();
            Assert.AreEqual(AccountKind.Guest, session.Kind); Assert.IsFalse(session.IsMember);
            Assert.AreEqual("", session.Id); Assert.AreEqual(AccountSession.GuestName, session.Nickname);
            Assert.AreEqual(65, session.Settings.Weight); Assert.AreEqual(PlayStyle.FullBody, session.Settings.Style);
            Assert.AreEqual(0, session.Coins); CollectionAssert.IsEmpty(session.Unlocked); CollectionAssert.IsEmpty(session.RunItems());
            Assert.IsFalse(session.TutorialDone); Assert.AreEqual("65", session.Profile.Weight);
        }

        [Test]
        public void SwitchesBetweenGuestAndMember()
        {
            var session = new AccountSession(); int changes = 0; session.Changed += () => changes++;
            session.SignInMember(Member(), "token-1");
            Assert.AreEqual(AccountKind.Member, session.Kind); Assert.AreEqual("member-1", session.Id); Assert.AreEqual("token-1", session.Token);
            Assert.AreEqual("민수", session.Nickname); Assert.AreEqual("민수", session.Profile.Name);
            Assert.AreEqual(72, session.Settings.Weight); Assert.AreEqual(PlayStyle.UpperBody, session.Settings.Style); Assert.AreEqual(120, session.Settings.Goal);
            Assert.AreEqual(90, session.Coins); Assert.IsTrue(session.TutorialDone);
            CollectionAssert.AreEqual(new List<ItemKind> { ItemKind.Magnet }, session.RunItems(), "Unknown item ids are ignored");
            Assert.AreEqual(1, changes);

            session.SignOut();
            Assert.AreEqual(AccountKind.Guest, session.Kind); Assert.AreEqual("", session.Id); Assert.AreEqual("", session.Token);
            Assert.AreEqual(AccountSession.GuestName, session.Nickname); Assert.AreEqual(0, session.Coins);
            CollectionAssert.IsEmpty(session.Unlocked); Assert.AreEqual(65, session.Settings.Weight); Assert.IsFalse(session.TutorialDone);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void GuestSettingsStayInMemoryAndCoinsNeverReachAGuest()
        {
            var session = new AccountSession();
            session.UpdateSettings(new AccountSettings { Weight = 0, Style = PlayStyle.Keyboard, Goal = 200 });
            Assert.AreEqual(65, session.Settings.Weight, "Weight is never 0: 65 stands for not entered");
            // Same 10-350 kg range as the run's WorkoutEstimate and the server, so a saved weight always starts a run.
            foreach (double kg in new[] { 9.9, 351, 500 }) { session.UpdateSettings(new AccountSettings { Weight = kg }); Assert.AreEqual(65, session.Settings.Weight, kg.ToString()); }
            foreach (double kg in new[] { 10.0, 350 }) { session.UpdateSettings(new AccountSettings { Weight = kg }); Assert.IsTrue(new WorkoutEstimate().SetWeight(session.Profile.Weight), kg.ToString()); }
            session.UpdateSettings(new AccountSettings { Weight = 0, Style = PlayStyle.Keyboard, Goal = 200 });
            Assert.AreEqual(PlayStyle.Keyboard, session.Settings.Style); Assert.AreEqual(200, session.Settings.Goal);
            session.SetCoins(500); Assert.AreEqual(0, session.Coins);
            Assert.IsFalse(session.Apply(Member()), "A server profile does not turn a guest into a member");
            Assert.IsFalse(session.IsMember);
            var copy = session.Settings; copy.Goal = 999; Assert.AreEqual(200, session.Settings.Goal, "Settings hands out a copy");
        }

        [Test]
        public void MemberRepliesUpdateOnlyTheSameAccount()
        {
            var session = new AccountSession(); session.SignInMember(Member(), "t");
            session.SetCoins(140); Assert.AreEqual(140, session.Coins); CollectionAssert.AreEqual(new[] { ItemKind.Magnet }, session.Unlocked);
            var other = Member(); other.id = "someone-else"; other.coins = 5;
            Assert.IsFalse(session.Apply(other)); Assert.AreEqual(140, session.Coins);
            var bought = Member(); bought.coins = 40; bought.unlocked = new[] { (int)ItemKind.Magnet, (int)ItemKind.Shield };
            Assert.IsTrue(session.Apply(bought)); Assert.AreEqual(40, session.Coins);
            CollectionAssert.AreEqual(new List<ItemKind> { ItemKind.Magnet, ItemKind.Shield }, session.RunItems());
        }

        [Test]
        public void ProfileUpdateSendsOnlyTheFieldsThatAreSet()
        {
            Assert.AreEqual("{}", new AccountProfileUpdate().ToJson());
            Assert.AreEqual("{\"name\":\"a\\\"b\",\"tutorial_done\":true}", new AccountProfileUpdate { Name = "a\"b", TutorialDone = true }.ToJson());
            Assert.AreEqual("{\"weight\":65,\"style\":2,\"goal\":50}", AccountProfileUpdate.From(new AccountSettings { Style = PlayStyle.Keyboard }).ToJson());
        }

        [Test]
        public void NicknameRuleMatchesTheServer()
        {
            Assert.IsNull(AccountSession.NicknameProblem("민수")); Assert.IsNull(AccountSession.NicknameProblem(" 1234567890 "));
            Assert.IsNotNull(AccountSession.NicknameProblem("")); Assert.IsNotNull(AccountSession.NicknameProblem("   ")); Assert.IsNotNull(AccountSession.NicknameProblem(null));
            Assert.IsNotNull(AccountSession.NicknameProblem("12345678901")); Assert.IsNotNull(AccountSession.NicknameProblem("a\tb"));
            // Python len() counts code points: an emoji is one character even though it is two UTF-16 units.
            string emoji = "\U0001F600";
            Assert.AreEqual(2, emoji.Length); Assert.AreEqual(1, AccountSession.CodePoints(emoji));
            Assert.IsNull(AccountSession.NicknameProblem(string.Concat(System.Linq.Enumerable.Repeat(emoji, 10))), "10 emoji = 10 characters");
            Assert.IsNotNull(AccountSession.NicknameProblem(string.Concat(System.Linq.Enumerable.Repeat(emoji, 11))));
            Assert.IsNull(AccountSession.NicknameProblem("민수" + emoji));
            // Every Unicode "C" category is refused, not only controls: U+200B (Cf), private use (Co), a lone surrogate (Cs).
            Assert.IsNotNull(AccountSession.NicknameProblem("a​b")); Assert.IsNotNull(AccountSession.NicknameProblem("ab"));
            Assert.IsNotNull(AccountSession.NicknameProblem("a\uD83D")); Assert.IsNotNull(AccountSession.NicknameProblem("\uDE00b"));
            Assert.GreaterOrEqual(AccountSession.MaxNicknameInput, 2 * AccountSession.MaxNickname, "The text field never cuts 10 emoji");
        }
    }
}
