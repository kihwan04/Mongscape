using System;
using System.Collections.Generic;
using System.IO;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using Muhanok.Presentation;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class MuhanokTestIntegrationTests
    {
        [Test]
        public void LegacyCoinsAndRankingsMigrateOnceWithoutChangingOriginals()
        {
            string root=Path.Combine(Path.GetTempPath(),"mongscape-merge-"+Guid.NewGuid().ToString("N"));
            try
            {
                string legacy=Path.Combine(root,"Exhibition");Directory.CreateDirectory(legacy);
                string wallet="{\"CoinPool\":90,\"Unlocked\":[0]}";
                long ticks=new DateTime(2026,10,26).Ticks;
                string ranking="{\"Entries\":[{\"Name\":\"A\",\"Distance\":120,\"Ticks\":"+ticks+"},{\"Name\":\"B\",\"Distance\":340,\"Ticks\":"+(ticks+1)+"}]}";
                string walletPath=Path.Combine(legacy,"progress.json"),rankPath=Path.Combine(legacy,"ranking-20261026.json");
                File.WriteAllText(walletPath,wallet);File.WriteAllText(rankPath,ranking);
                var store=new PlayerProgressStore(root);
                Assert.AreEqual(90,store.Wallet.Coins);Assert.IsTrue(store.Wallet.IsUnlocked(ItemKind.Magnet));
                Assert.AreEqual(2,store.Board.All.Count);Assert.AreEqual(340,store.Board.All[0].Distance);
                Assert.AreEqual(1,store.Board.All[0].ScoreVersion,"Old rankings load as score version 1");Assert.IsEmpty(store.Board.View(false,DateTime.Now,PlayStyle.FullBody));Assert.AreEqual(-1,store.Board.All[0].Mode,"No Style label: no mode tab");
                Assert.AreEqual(2,store.Stats.Runs);Assert.AreEqual(460,store.Stats.TotalDistance,.001);
                Assert.AreEqual(0,store.Stats.TotalKcal,"Old rankings did not store calories");
                var reopened=new PlayerProgressStore(root);
                Assert.AreEqual(90,reopened.Wallet.Coins);Assert.AreEqual(2,reopened.Stats.Runs);
                Assert.AreEqual(wallet,File.ReadAllText(walletPath));Assert.AreEqual(ranking,File.ReadAllText(rankPath));
            }
            finally
            {
                string full=Path.GetFullPath(root),parent=Path.GetFullPath(Path.GetTempPath());
                if(!full.StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("mongscape-merge-"))throw new InvalidOperationException("Unexpected test folder");
                if(Directory.Exists(full))Directory.Delete(full,true);
            }
        }

        [Test]
        public void LeaderboardLoadKeepsTheBestFiveHundredScores()
        {
            var rows=new List<RankingEntry>();rows.Add(null);
            for(int i=0;i<600;i++)rows.Add(new RankingEntry{Name="Test",Distance=i,Score=i,ScoreVersion=RunScore.Version,Mode=(int)PlayStyle.FullBody,Ticks=i+1});
            var board=new Leaderboard();board.Load(rows);
            Assert.AreEqual(Leaderboard.MaxEntries,board.All.Count);
            Assert.AreEqual(599,board.All[0].Distance);Assert.AreEqual(100,board.All[499].Distance);
        }

        [Test]
        public void NewPlayerProfileRetainsIndependentCalorieGoal()
        {
            var profile=new PlayerProfile{Name="Test",CalorieGoal=180,Style=PlayStyle.UpperBody};
            var copy=profile.Clone();copy.Name="Other";copy.CalorieGoal=250;
            Assert.AreEqual("Test",profile.Name);Assert.AreEqual(180,profile.CalorieGoal);
            Assert.AreEqual(250,copy.CalorieGoal);Assert.AreEqual(PlayStyle.UpperBody,copy.Style);
        }

        [Test]
        public void NewMenuKeepsTheFaceLoginEntryAndDataContract()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(MenuPage),"Identity"));
            Assert.IsTrue(Enum.IsDefined(typeof(MenuPage),"Login"));Assert.IsTrue(Enum.IsDefined(typeof(MenuPage),"SignUp"));
            Assert.IsTrue(Enum.IsDefined(typeof(MenuAction),"Identity"));
            Assert.IsNotNull(typeof(IIdentityData).GetProperty("SignedIn"));
            Assert.IsNotNull(typeof(HUDPresenter).GetMethod("ShowIdentity"));
        }
    }
}
