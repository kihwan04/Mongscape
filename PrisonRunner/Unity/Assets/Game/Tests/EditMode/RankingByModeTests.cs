using System;
using System.Collections.Generic;
using System.IO;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using NUnit.Framework;

namespace Muhanok.Tests
{
    // This PC's ranking keeps one board per play style: filter, rank and the 500 cap all work per mode.
    public sealed class RankingByModeTests
    {
        static RankingEntry Run(string name, int score, PlayStyle mode, long ticks) =>
            new RankingEntry { Name = name, Score = score, Distance = score, ScoreVersion = RunScore.Version, Mode = (int)mode, Style = PlayStyleInfo.Label(mode), Ticks = ticks };

        [Test]
        public void ViewShowsOnlyTheChosenPlayStyle()
        {
            var today = DateTime.Now; long t = today.Ticks; var board = new Leaderboard();
            board.Add(Run("전신A", 900, PlayStyle.FullBody, t)); board.Add(Run("상체A", 1200, PlayStyle.UpperBody, t + 1));
            board.Add(Run("키보드A", 400, PlayStyle.Keyboard, t + 2)); board.Add(Run("전신B", 1000, PlayStyle.FullBody, t + 3));
            board.Add(Run("어제전신", 2000, PlayStyle.FullBody, today.AddDays(-1).Ticks));
            board.Add(new RankingEntry { Name = "미분류", Score = 5000, ScoreVersion = RunScore.Version, Ticks = t + 4 });
            CollectionAssert.AreEqual(new[] { "어제전신", "전신B", "전신A" }, board.View(false, today, PlayStyle.FullBody).ConvertAll(e => e.Name));
            CollectionAssert.AreEqual(new[] { "전신B", "전신A" }, board.View(true, today, PlayStyle.FullBody).ConvertAll(e => e.Name));
            CollectionAssert.AreEqual(new[] { "상체A" }, board.View(false, today, PlayStyle.UpperBody).ConvertAll(e => e.Name));
            CollectionAssert.AreEqual(new[] { "키보드A" }, board.View(true, today, PlayStyle.Keyboard).ConvertAll(e => e.Name));
            Assert.AreEqual(6, board.All.Count, "An entry without a mode is kept but shows on no mode tab");
        }

        [Test]
        public void RankIsCountedWithinThePlayStyle()
        {
            var today = DateTime.Now; long t = today.Ticks; var board = new Leaderboard();
            Assert.AreEqual(1, board.Add(Run("상체", 3000, PlayStyle.UpperBody, t)));
            var full = Run("전신", 1000, PlayStyle.FullBody, t + 1);
            Assert.AreEqual(1, board.Add(full), "A higher upper-body score does not push the full-body run down");
            Assert.AreEqual(2, board.Add(Run("전신2", 900, PlayStyle.FullBody, t + 2)));
            board.Add(Run("전신어제", 5000, PlayStyle.FullBody, today.AddDays(-1).Ticks));
            Assert.AreEqual(2, board.RankOf(full, false, PlayStyle.FullBody)); Assert.AreEqual(1, board.RankOf(full, true, PlayStyle.FullBody));
            Assert.AreEqual(0, board.RankOf(full, false, PlayStyle.UpperBody), "Not ranked on another mode's board");
            Assert.AreEqual(0, board.Add(new RankingEntry { Name = "미분류", Score = 1, ScoreVersion = RunScore.Version, Ticks = t + 3 }));
        }

        [Test]
        public void TheFiveHundredCapAppliesToEachPlayStyle()
        {
            var rows = new List<RankingEntry>();
            for (int i = 0; i < 600; i++) rows.Add(Run("전신", i, PlayStyle.FullBody, i + 1));
            for (int i = 0; i < 10; i++) rows.Add(Run("키보드", i, PlayStyle.Keyboard, 1000 + i));
            var board = new Leaderboard(); board.Load(rows);
            var full = board.View(false, DateTime.MinValue, PlayStyle.FullBody);
            Assert.AreEqual(Leaderboard.MaxEntries, full.Count); Assert.AreEqual(599, full[0].Score); Assert.AreEqual(100, full[499].Score);
            Assert.AreEqual(10, board.View(false, DateTime.MinValue, PlayStyle.Keyboard).Count, "The full-body overflow does not remove keyboard runs");
            board.Add(Run("새전신", 50, PlayStyle.FullBody, 5000));
            Assert.AreEqual(Leaderboard.MaxEntries, board.View(false, DateTime.MinValue, PlayStyle.FullBody).Count);
            Assert.AreEqual(10, board.View(false, DateTime.MinValue, PlayStyle.Keyboard).Count);
        }

        [Test]
        public void RecordsSavedWithOnlyAStyleLabelGetTheirMode()
        {
            string root = Path.Combine(Path.GetTempPath(), "mongscape-mode-" + Guid.NewGuid().ToString("N"));
            try
            {
                long t = DateTime.Now.Ticks; string folder = Path.Combine(root, "Player"); Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "records.json"), "{\"Entries\":["
                    + "{\"Name\":\"상체\",\"Score\":300,\"Distance\":300,\"ScoreVersion\":2,\"Style\":\"상체\",\"Ticks\":" + t + "},"
                    + "{\"Name\":\"키보드\",\"Score\":200,\"Distance\":200,\"ScoreVersion\":2,\"Style\":\"키보드\",\"Ticks\":" + (t + 1) + "},"
                    + "{\"Name\":\"라벨없음\",\"Score\":900,\"Distance\":900,\"ScoreVersion\":2,\"Ticks\":" + (t + 2) + "}],\"Stats\":{}}");
                var store = new PlayerProgressStore(root);
                var byName = new Dictionary<string, RankingEntry>(); foreach (var e in store.Board.All) byName[e.Name] = e;
                Assert.AreEqual((int)PlayStyle.UpperBody, byName["상체"].Mode); Assert.AreEqual((int)PlayStyle.Keyboard, byName["키보드"].Mode);
                Assert.AreEqual(-1, byName["라벨없음"].Mode, "No label: unclassified");
                CollectionAssert.AreEqual(new[] { "상체" }, store.Board.View(false, DateTime.Now, PlayStyle.UpperBody).ConvertAll(e => e.Name));
                Assert.IsEmpty(store.Board.View(false, DateTime.Now, PlayStyle.FullBody), "The unclassified record is on no mode tab");
                // A new run saves its mode and calories.
                store.Record(new RankingEntry { Name = "새판", Score = 50, ScoreVersion = RunScore.Version, Mode = (int)PlayStyle.FullBody, Kcal = 4.5, Ticks = t + 3 }, 4.5);
                var reopened = new PlayerProgressStore(root); RankingEntry saved = null;
                foreach (var e in reopened.Board.All) if (e.Name == "새판") saved = e;
                Assert.IsNotNull(saved); Assert.AreEqual((int)PlayStyle.FullBody, saved.Mode); Assert.AreEqual(4.5, saved.Kcal, .0001);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
