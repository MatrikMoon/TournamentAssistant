using Microsoft.EntityFrameworkCore.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantShared.BeatSaver
{
    public partial class SongInfo
    {
        [JsonProperty("metadata")]
        public Metadata Metadata { get; set; }

        [JsonProperty("stats")]
        public Stats Stats { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("deletedAt")]
        public DateTimeOffset? DeletedAt { get; set; }

        [JsonProperty("_id")]
        public string Id { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("uploader")]
        public Uploader Uploader { get; set; }

        [JsonProperty("uploaded")]
        public DateTimeOffset Uploaded { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("directDownload")]
        public string DirectDownload { get; set; }

        [JsonProperty("downloadURL")]
        public string DownloadUrl { get; set; }

        [JsonProperty("coverURL")]
        public string CoverUrl { get; set; }

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
            var characteristicInfo = Metadata.Characteristics.FirstOrDefault(x => x.Name.ToLower() == characteristic.ToLower());
            if (characteristicInfo != null)
            {
                var ret = new List<int>();
                if (characteristicInfo.Difficulties.Easy != null) ret.Add((int)BeatmapDifficulty.Easy);
                if (characteristicInfo.Difficulties.Normal != null) ret.Add((int)BeatmapDifficulty.Normal);
                if (characteristicInfo.Difficulties.Hard != null) ret.Add((int)BeatmapDifficulty.Hard);
                if (characteristicInfo.Difficulties.Expert != null) ret.Add((int)BeatmapDifficulty.Expert);
                if (characteristicInfo.Difficulties.ExpertPlus != null) ret.Add((int)BeatmapDifficulty.ExpertPlus);
                return ret.OrderBy(x => x).ToArray();
            }
            return new int[] { };
        }

        public bool HasDifficulty(string characteristic, BeatmapDifficulty difficulty)
        {
            var characteristicInfo = Metadata.Characteristics.FirstOrDefault(x => x.Name.ToLower() == characteristic.ToLower());
            if (characteristicInfo != null)
            {
                switch (difficulty)
                {
                    case BeatmapDifficulty.Easy:
                        return characteristicInfo.Difficulties.Easy != null;
                    case BeatmapDifficulty.Normal:
                        return characteristicInfo.Difficulties.Normal != null;
                    case BeatmapDifficulty.Hard:
                        return characteristicInfo.Difficulties.Hard != null;
                    case BeatmapDifficulty.Expert:
                        return characteristicInfo.Difficulties.Expert != null;
                    case BeatmapDifficulty.ExpertPlus:
                        return characteristicInfo.Difficulties.ExpertPlus != null;
                }
            }
            return false;
        }
    }

    public partial class Metadata
    {
        [JsonProperty("difficulties")]
        public MetadataDifficulties Difficulties { get; set; }

        [JsonProperty("duration")]
        public long Duration { get; set; }

        [JsonProperty("automapper")]
        public object Automapper { get; set; }

        [JsonProperty("characteristics")]
        public MetadataCharacteristic[] Characteristics { get; set; }

        [JsonProperty("songName")]
        public string SongName { get; set; }

        [JsonProperty("songSubName")]
        public string SongSubName { get; set; }

        [JsonProperty("songAuthorName")]
        public string SongAuthorName { get; set; }

        [JsonProperty("levelAuthorName")]
        public string LevelAuthorName { get; set; }

        [JsonProperty("bpm")]
        public long Bpm { get; set; }
    }

    public partial class MetadataCharacteristic
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("difficulties")]
        public CharacteristicDifficulties Difficulties { get; set; }
    }

    public partial class CharacteristicDifficulties
    {
        [JsonProperty("easy")]
        public DifficultyInfo Easy { get; set; }

        [JsonProperty("normal")]
        public DifficultyInfo Normal { get; set; }

        [JsonProperty("hard")]
        public DifficultyInfo Hard { get; set; }

        [JsonProperty("expert")]
        public DifficultyInfo Expert { get; set; }

        [JsonProperty("expertPlus")]
        public DifficultyInfo ExpertPlus { get; set; }
    }

    public partial class DifficultyInfo
    {
        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("length")]
        public long Length { get; set; }

        [JsonProperty("bombs")]
        public long Bombs { get; set; }

        [JsonProperty("notes")]
        public long Notes { get; set; }

        [JsonProperty("obstacles")]
        public long Obstacles { get; set; }

        [JsonProperty("njs")]
        public double Njs { get; set; }

        [JsonProperty("njsOffset")]
        public double NjsOffset { get; set; }
    }

    public partial class MetadataDifficulties
    {
        [JsonProperty("easy")]
        public bool Easy { get; set; }

        [JsonProperty("normal")]
        public bool Normal { get; set; }

        [JsonProperty("hard")]
        public bool Hard { get; set; }

        [JsonProperty("expert")]
        public bool Expert { get; set; }

        [JsonProperty("expertPlus")]
        public bool ExpertPlus { get; set; }
    }

    public partial class Stats
    {
        [JsonProperty("downloads")]
        public long Downloads { get; set; }

        [JsonProperty("plays")]
        public long Plays { get; set; }

        [JsonProperty("downVotes")]
        public long DownVotes { get; set; }

        [JsonProperty("upVotes")]
        public long UpVotes { get; set; }

        [JsonProperty("heat")]
        public double Heat { get; set; }

        [JsonProperty("rating")]
        public double Rating { get; set; }
    }

    public partial class Uploader
    {
        [JsonProperty("_id")]
        public string Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }
    }
}
