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
        public string Hash { get; private set; }

        private string _infoPath;

        public DownloadedSong(string songHash)
        {
            Hash = songHash;

            if (!OstHelper.IsOst(Hash))
            {
                _infoPath = GetInfoPath();
            }
        }

        //Looks at info.json and gets the song name
        private string GetSongName()
        {
            if (OstHelper.IsOst(Hash))
            {
                return OstHelper.GetOstSongNameFromLevelId(Hash);
            }
            else
            {
                var infoText = File.ReadAllText(_infoPath);
                JSONNode node = JSON.Parse(infoText);
                return node["_songName"];
            }
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
            if (OstHelper.IsOst(Hash))
            {
                return new string[] { "Standard", "OneSaber", "NoArrows", "90Degree", "360Degree" };
            }
            else
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

            var idFolder = $"{AppDataSongDataPath}{Hash}";
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
            var idFolder = $"{AppDataSongDataPath}{Hash}";
            var songFolder = Directory.GetDirectories(idFolder); //Assuming each id folder has only one song folder
            return songFolder.FirstOrDefault() ?? idFolder;
        }

        public static bool Exists(string hash)
        {
            return Directory.Exists($"{AppDataSongDataPath}{hash}");
        }
    }
}
