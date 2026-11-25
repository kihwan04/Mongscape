using System;
using System.Collections.Generic;
using System.IO;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Infrastructure
{
    // Player data on this PC: the per-mode score leaderboard and lifetime stats. Players come from AccountSession; an old
    // PC profile.json or wallet.json is left untouched.
    // Everything goes through this class so a server-backed store can replace it later.
    public sealed class PlayerProgressStore
    {
        [Serializable] private sealed class RecordsFile { public List<RankingEntry> Entries = new List<RankingEntry>(); public PlayerStats Stats = new PlayerStats(); public List<ExerciseDay> Exercise = new List<ExerciseDay>(); }
        private const string LegacyBestKey = "Muhanok.BestDistance";
        private readonly string folder;
        private readonly string legacyFolder;
        private readonly bool useLegacyPrefs;
        public readonly Leaderboard Board = new Leaderboard();
        public PlayerStats Stats { get; private set; } = new PlayerStats();
        // Weekly goal, streak and course balance per player (exercise plan 8); this PC only for now.
        public readonly ExerciseLog Exercise = new ExerciseLog();
        // Adds a finished run's exercise time and saves; returns the streak coins it earned (see ExerciseLog.Add).
        public int RecordExercise(string owner, DateTime utcNow, double active, double moderate, double[] kinds)
        {
            int coins = Exercise.Add(owner, utcNow, active, moderate, kinds); SaveRecords(); return coins;
        }
        public PlayerProgressStore(string root = null)
        {
            folder = Path.Combine(root ?? UnityEngine.Application.persistentDataPath, "Player");
            // Folder of the first builds (named after the exhibition they ran at); its rankings are read once, never changed.
            legacyFolder = Path.Combine(root ?? UnityEngine.Application.persistentDataPath,"Exhibition");
            useLegacyPrefs=root==null;
            LoadRecords();
        }
        private string RecordsPath => Path.Combine(folder, "records.json");
        // Adds a finished run; returns its rank among the runs of its play style.
        public int Record(RankingEntry entry, double kcal)
        {
            int rank = Board.Add(entry);
            Stats.Add(entry.Distance, kcal);
            SaveRecords();
            return rank;
        }
        // A guest names the run that was recorded as "게스트"; false when the entry is no longer on the board.
        public bool Rename(RankingEntry entry, string name)
        {
            if (entry == null || string.IsNullOrWhiteSpace(name) || !Board.Contains(entry)) return false;
            entry.Name = name.Trim(); SaveRecords(); return true;
        }
        public void RemoveAccount(string id) { Board.RemoveAccount(id); SaveRecords(); }
        private void LoadRecords()
        {
            var file = Read<RecordsFile>(RecordsPath);
            bool migrate=file==null&&!File.Exists(RecordsPath)&&Directory.Exists(legacyFolder);
            if(migrate)
            {
                file=new RecordsFile();
                foreach(string path in Directory.GetFiles(legacyFolder,"ranking-*.json"))
                {
                    var old=Read<RecordsFile>(path);
                    if(old?.Entries==null)continue;
                    foreach(var entry in old.Entries)
                    {
                        if(entry==null)continue;
                        file.Entries.Add(entry);file.Stats.Add(entry.Distance,0);
                    }
                }
            }
            Board.Load(file?.Entries);
            Exercise.Load(file?.Exercise);
            Stats = file?.Stats ?? new PlayerStats();
            // The best distance used to live in PlayerPrefs; keep it when this file is new.
            if(useLegacyPrefs)Stats.BestDistance = Mathf.Max(Stats.BestDistance, PlayerPrefs.GetFloat(LegacyBestKey, 0f));
            if(migrate&&file.Entries.Count>0)SaveRecords();
        }
        private void SaveRecords() => Write(RecordsPath, new RecordsFile { Entries = new List<RankingEntry>(Board.All), Stats = Stats, Exercise = new List<ExerciseDay>(Exercise.Days) });
        private static T Read<T>(string path) where T : class
        {
            try { return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null; }
            catch (Exception error) { Debug.LogWarning("Player data unreadable: " + path + " " + error.Message); return null; }
        }
        private void Write(string path, object data)
        {
            try { Directory.CreateDirectory(folder); File.WriteAllText(path, JsonUtility.ToJson(data, true)); }
            catch (Exception error) { Debug.LogWarning("Player data not saved: " + error.Message); }
        }
    }
}
