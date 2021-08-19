using TournamentAssistantShared.SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantShared.BeatSaver
{
    public class DownloadedSong
    {
        public static readonly string currentDirectory = Directory.GetCurrentDirectory();
        public static readonly string songDirectory = $@"{currentDirectory}\DownloadedSongs\";

        public string[] Characteristics { get; private set; }
        public string Name { get; }
        string Hash { get; set; }

        private string _infoPath;

        public DownloadedSong(string songHash)
        {
            Hash = songHash;

            if (!OstHelper.IsOst(Hash))
            {
                _infoPath = GetInfoPath();
                Characteristics = GetBeatmapCharacteristics();
                Name = GetSongName();
            }
            else
            {
                Name = OstHelper.GetOstSongNameFromLevelId(Hash);
                Characteristics = new string[] { "Standard", "OneSaber", "NoArrows", "90Degree", "360Degree" };
            }
        }

        //Compatibility overload for new models
        public DownloadedSong(string songHash, string infoDatPath)
        {
            Hash = songHash;

            if (!OstHelper.IsOst(Hash))
            {
                _infoPath = infoDatPath;
                Characteristics = GetBeatmapCharacteristics();
                Name = GetSongName();
            }
            else
            {
                Name = OstHelper.GetOstSongNameFromLevelId(Hash);
                Characteristics = new string[] { "Standard", "OneSaber", "NoArrows", "90Degree", "360Degree" };
            }
        }

        //Looks at info.json and gets the song name
        private string GetSongName()
        {
            var infoText = File.ReadAllText(_infoPath);
            JSONNode node = JSON.Parse(infoText);
            return node["_songName"];
        }

        private string GetIconPath()
        {
            var infoText = File.ReadAllText(_infoPath);
            JSONNode node = JSON.Parse(infoText);
            return $"{GetSongRootDir()}\\{node["_coverImageFilename"].Value}";
        }

        public string GetAudioPath()
        {
            var infoText = File.ReadAllText(_infoPath);
            JSONNode node = JSON.Parse(infoText);
            return $"{GetSongRootDir()}\\{node["_songFilename"].Value}";
        }

        private string[] GetBeatmapCharacteristics()
        {
            List<string> characteristics = new List<string>();
            var infoText = File.ReadAllText(_infoPath);
            JSONNode node = JSON.Parse(infoText);
            JSONArray difficultyBeatmapSets = node["_difficultyBeatmapSets"].AsArray;
            foreach (var item in difficultyBeatmapSets)
            {
                characteristics.Add(item.Value["_beatmapCharacteristicName"]);
            }
            return characteristics.OrderBy(x => x).ToArray();
        }

        public BeatmapDifficulty[] GetBeatmapDifficulties(string characteristicSerializedName)
        {
            List<BeatmapDifficulty> difficulties = new List<BeatmapDifficulty>();
            var infoText = File.ReadAllText(_infoPath);
            JSONNode node = JSON.Parse(infoText);
            JSONArray difficultyBeatmapSets = node["_difficultyBeatmapSets"].AsArray;
            var difficultySet = difficultyBeatmapSets.Linq.First(x => x.Value["_beatmapCharacteristicName"] == characteristicSerializedName).Value;
            var difficultyBeatmaps = difficultySet["_difficultyBeatmaps"].AsArray;

            foreach (var item in difficultyBeatmaps)
            {
                Enum.TryParse(item.Value["_difficulty"], out BeatmapDifficulty difficulty);
                difficulties.Add(difficulty);
            }
            return difficulties.OrderBy(x => x).ToArray();
        }

        public string GetPathForDifficulty(string characteristicSerializedName, BeatmapDifficulty difficulty)
        {
            List<BeatmapDifficulty> difficulties = new List<BeatmapDifficulty>();
            var infoText = File.ReadAllText(_infoPath);
            JSONNode node = JSON.Parse(infoText);
            JSONArray difficultyBeatmapSets = node["_difficultyBeatmapSets"].AsArray;
            var difficultySet = difficultyBeatmapSets.Linq.First(x => x.Value["_beatmapCharacteristicName"] == characteristicSerializedName).Value;
            var difficultyBeatmap = difficultySet["_difficultyBeatmaps"].Linq.First(x => x.Value["_difficulty"].Value == difficulty.ToString()).Value;
            var fileName = difficultyBeatmap["_beatmapFilename"].Value;

            var idFolder = $"{songDirectory}{Hash}";
            var songFolder = Directory.GetDirectories(idFolder); //Assuming each id folder has only one song folder
            var subFolder = songFolder.FirstOrDefault() ?? idFolder;
            return Directory.GetFiles(subFolder, fileName, SearchOption.AllDirectories).First(); //Assuming each song folder has only one info.json
        }

        public int GetNoteCount(string characteristicSerializedName, BeatmapDifficulty difficulty)
        {
            var infoText = File.ReadAllText(GetPathForDifficulty(characteristicSerializedName, difficulty));
            JSONNode node = JSON.Parse(infoText);
            return node["_notes"].AsArray.Count;
        }

        public int GetMaxScore(string characteristicSerializedName, BeatmapDifficulty difficulty) => GetMaxScore(GetNoteCount(characteristicSerializedName, difficulty));
        public int GetMaxScore(int noteCount)
        {
            //Copied from game files
            int num = 0;
            int num2 = 1;
            while (num2 < 8)
            {
                if (noteCount >= num2 * 2)
                {
                    num += num2 * num2 * 2 + num2;
                    noteCount -= num2 * 2;
                    num2 *= 2;
                    continue;
                }
                num += num2 * noteCount;
                noteCount = 0;
                break;
            }
            num += noteCount * num2;
            return num * 115;
        }

        private string GetInfoPath()
        {
            return Directory.GetFiles(GetSongRootDir(), "info.dat", SearchOption.AllDirectories).First(); //Assuming each song folder has only one info.json
        }

        private string GetSongRootDir()
        {
            var idFolder = $"{songDirectory}{Hash}";
            var songFolder = Directory.GetDirectories(idFolder); //Assuming each id folder has only one song folder
            return songFolder.FirstOrDefault() ?? idFolder;
        }

        #region GetClosestDifficulty
        //Returns the closest difficulty to the one provided, preferring lower difficulties first if any exist
        public BeatmapDifficulty GetClosestDifficultyPreferLower(BeatmapDifficulty difficulty) => GetClosestDifficultyPreferLower("Standard", difficulty);
        public BeatmapDifficulty GetClosestDifficultyPreferLower(string characteristicSerializedName, BeatmapDifficulty difficulty)
        {
            if (GetBeatmapDifficulties(characteristicSerializedName).Contains(difficulty)) return difficulty;

            int ret = -1;
            if (ret == -1)
            {
                ret = GetLowerDifficulty(characteristicSerializedName, difficulty);
            }
            if (ret == -1)
            {
                ret = GetHigherDifficulty(characteristicSerializedName, difficulty);
            }
            return (BeatmapDifficulty)ret;
        }

        //Returns the next-lowest difficulty to the one provided
        private int GetLowerDifficulty(string characteristicSerializedName, BeatmapDifficulty difficulty)
        {
            return GetBeatmapDifficulties(characteristicSerializedName).Select(x => (int)x).TakeWhile(x => x < (int)difficulty).DefaultIfEmpty(-1).Last();
        }

        //Returns the next-highest difficulty to the one provided
        private int GetHigherDifficulty(string characteristicSerializedName, BeatmapDifficulty difficulty)
        {
            return GetBeatmapDifficulties(characteristicSerializedName).Select(x => (int)x).SkipWhile(x => x < (int)difficulty).DefaultIfEmpty(-1).First();
        }
        #endregion GetClosestDifficulty

        public static bool Exists(string hash)
        {
            return Directory.Exists($"{songDirectory}{hash}");
        }
    }
}
