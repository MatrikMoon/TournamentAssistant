using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantShared.BeatSaver
{
    public class Uploader
    {
        public int id { get; set; }
        public string name { get; set; }
        public bool uniqueSet { get; set; }
        public string hash { get; set; }
        public string avatar { get; set; }
        public string type { get; set; }
    }

    public class Metadata
    {
        public double bpm { get; set; }
        public int duration { get; set; }
        public string songName { get; set; }
        public string songSubName { get; set; }
        public string songAuthorName { get; set; }
        public string levelAuthorName { get; set; }
    }

    public class Stats
    {
        public int plays { get; set; }
        public int downloads { get; set; }
        public int upvotes { get; set; }
        public int downvotes { get; set; }
        public double score { get; set; }
    }

    public class ParitySummary
    {
        public int errors { get; set; }
        public int warns { get; set; }
        public int resets { get; set; }
    }

    public class Diff
    {
        public double njs { get; set; }
        public double offset { get; set; }
        public int notes { get; set; }
        public int bombs { get; set; }
        public int obstacles { get; set; }
        public double nps { get; set; }
        public double length { get; set; }
        public string characteristic { get; set; }
        public string difficulty { get; set; }
        public int events { get; set; }
        public bool chroma { get; set; }
        public bool me { get; set; }
        public bool ne { get; set; }
        public bool cinema { get; set; }
        public double seconds { get; set; }
        public ParitySummary paritySummary { get; set; }
    }

    public class Version
    {
        public string hash { get; set; }
        public string state { get; set; }
        public DateTime createdAt { get; set; }
        public int sageScore { get; set; }
        public List<Diff> diffs { get; set; }
        public string downloadURL { get; set; }
        public string coverURL { get; set; }
        public string previewURL { get; set; }
    }

    public class SongInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public Uploader uploader { get; set; }
        public Metadata metadata { get; set; }
        public Stats stats { get; set; }
        public DateTime uploaded { get; set; }
        public bool automapper { get; set; }
        public bool ranked { get; set; }
        public bool qualified { get; set; }
        public List<Version> versions { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
        public DateTime lastPublishedAt { get; set; }

        public Version CurrentVersion { get
            {
                return versions.FirstOrDefault(x => x.state == "Published");
            }
        }

        public string[] Characteristics
        {
            get
            {
                return CurrentVersion.diffs.Select(x => x.characteristic).ToArray();
            }
        }

        public BeatmapDifficulty GetClosestDifficultyPreferLower(string characteristic, BeatmapDifficulty difficulty)
        {
            if (HasDifficulty(characteristic, difficulty)) return difficulty;

            int ret = GetLowerDifficulty(characteristic, difficulty);
            if (ret == -1)
            {
                ret = GetHigherDifficulty(characteristic, difficulty);
            }
            return (BeatmapDifficulty)ret;
        }

        private int GetLowerDifficulty(string characteristic, BeatmapDifficulty difficulty)
        {
            return GetDifficultiesAsIntArray(characteristic).TakeWhile(x => x < (int)difficulty).DefaultIfEmpty(-1).Last();
        }

        private int GetHigherDifficulty(string characteristic, BeatmapDifficulty difficulty)
        {
            return GetDifficultiesAsIntArray(characteristic).SkipWhile(x => x < (int)difficulty).DefaultIfEmpty(-1).First();
        }

        private int[] GetDifficultiesAsIntArray(string characteristic)
        {
            var characteristicDiffs = CurrentVersion.diffs.Where(x => x.characteristic.ToLower() == characteristic.ToLower());
            if (characteristicDiffs.Any())
            {
                return characteristicDiffs.Select(x => (int)Enum.Parse(typeof(BeatmapDifficulty), x.difficulty)).OrderBy(x => x).ToArray();
            }
            
            return new int[] { };
        }

        public bool HasDifficulty(string characteristic, BeatmapDifficulty difficulty)
        {
            var characteristicDiffs = CurrentVersion.diffs.Where(x => x.characteristic.ToLower() == characteristic.ToLower());
            return characteristicDiffs.Any(x => x.difficulty.ToString().ToLower() == difficulty.ToString().ToLower());
        }
    }
}
