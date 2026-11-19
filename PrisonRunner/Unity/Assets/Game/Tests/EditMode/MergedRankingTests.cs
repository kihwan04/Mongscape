using System.Collections.Generic;
using Muhanok.Application;
using Muhanok.Domain;
using Muhanok.Presentation;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class MergedRankingTests
    {
        private static RankingEntry Guest(string name, int score) => new RankingEntry { Name = name, Score = score };
        private static RankingEntry Member(string name, int score) => new RankingEntry { Name = name, Score = score, AccountId = "id-" + name };
        private static LeaderboardReply Server(LeaderboardMe me, params (string name, int score)[] rows)
        {
            var reply = new LeaderboardReply { ok = true, me = me, entries = new LeaderboardRow[rows.Length] };
            for (int i = 0; i < rows.Length; i++) reply.entries[i] = new LeaderboardRow { rank = i + 1, name = rows[i].name, score = rows[i].score };
            return reply;
        }

        [Test] public void GuestsOfThisPcAndServerMembersInterleaveByScoreWithoutDuplicates()
        {
            var g = Guest("G", 700);
            var local = new List<RankingEntry> { Guest("A", 800), g, Member("M", 600), Guest("B", 100) };
            var rows = RankingBoard.Combined(local, Server(null, ("X", 900), ("Y", 700), ("M", 600)), g, out var mine);
            CollectionAssert.AreEqual(new[] { "X", "A", "G", "Y", "M", "B" }, rows.ConvertAll(r => r.Name), "The local member run is the server's M row, not repeated");
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, rows.ConvertAll(r => r.Rank));
            Assert.AreSame(rows[2], mine, "A tie keeps the guest first; the guest's own run is highlighted");
            Assert.IsTrue(rows[0].Member); Assert.IsFalse(rows[1].Member);
        }

        [Test] public void AMemberRunStillUploadingReplacesThatMembersOlderServerRow()
        {
            var run = Member("Me", 950);
            var rows = RankingBoard.Combined(new List<RankingEntry> { run, Guest("G", 500) }, Server(new LeaderboardMe { rank = 2, score = 400 }, ("X", 900), ("Me", 400)), run, out var mine);
            CollectionAssert.AreEqual(new[] { "Me", "X", "G" }, rows.ConvertAll(r => r.Name));
            Assert.AreEqual(1, mine.Rank); Assert.AreEqual(950, mine.Score);
            // Another play style's tab (the run is not in its local view) never shows it.
            var other = RankingBoard.Combined(new List<RankingEntry>(), new LeaderboardReply { ok = true }, run, out var none);
            Assert.AreEqual(0, other.Count); Assert.IsNull(none);
        }

        [Test] public void AMemberBelowTheServerTopListIsRankedAfterEveryHigherRow()
        {
            var rows = RankingBoard.Combined(new List<RankingEntry> { Guest("G1", 300), Guest("G2", 50) }, Server(new LeaderboardMe { rank = 51, score = 100 }, ("X", 900)), null, out var mine);
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(52, mine.Rank, "50 members and one guest above");
        }
    }
}
