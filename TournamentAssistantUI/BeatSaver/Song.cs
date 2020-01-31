using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TournamentAssistantShared;
using TournamentAssistantShared.SimpleJSON;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantUI.BeatSaver
{
    public class Song
    {
        public static readonly string currentDirectory = Directory.GetCurrentDirectory();
        public static readonly string songDirectory = $@"{currentDirectory}\DownloadedSongs\";

        //Currently only used in song downloading
        public enum CharacteristicType
        {
            Standard,
            OneSaber,
            NoArrows
        }

        public CharacteristicType[] Characteristics { get; private set; }
        public string Name { get; }
        string Hash { get; set; }

        private string _infoPath;

        public Song(string songHash)
        {
            Hash = songHash;

            if (!OstHelper.IsOst(Hash))
            {
                _infoPath = GetInfoPath();
                Characteristics = GetCharacteristicTypes();
                Name = GetSongName();
            }
            else
            {
                Name = OstHelper.GetOstSongNameFromLevelId(Hash);
                Characteristics = new CharacteristicType[] { CharacteristicType.Standard, CharacteristicType.OneSaber, CharacteristicType.NoArrows };
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
            return $"{GetSongRootDir()}{node["_coverImageFilename"]}";
        }

        private CharacteristicType[] GetCharacteristicTypes()
        {
            List<CharacteristicType> characteristics = new List<CharacteristicType>();
            var infoText = File.ReadAllText(_infoPath);
            JSONNode node = JSON.Parse(infoText);
            JSONArray difficultyBeatmapSets = node["_difficultyBeatmapSets"].AsArray;
            foreach (var item in difficultyBeatmapSets)
            {
                Enum.TryParse(item.Value["_CharacteristicTypeName"], out CharacteristicType difficulty);
                characteristics.Add(difficulty);
            }
            return characteristics.OrderBy(x => x).ToArray();
        }

        public BeatmapDifficulty[] GetBeatmapDifficulties(CharacteristicType characteristic)
        {
            List<BeatmapDifficulty> difficulties = new List<BeatmapDifficulty>();
            var infoText = File.ReadAllText(_infoPath);
            JSONNode node = JSON.Parse(infoText);
            JSONArray difficultyBeatmapSets = node["_difficultyBeatmapSets"].AsArray;
            var difficultySet = difficultyBeatmapSets.Linq.First(x => x.Value["_beatmapCharacteristicName"] == characteristic.ToString()).Value;
            var difficultyBeatmaps = difficultySet["_difficultyBeatmaps"].AsArray;

            foreach (var item in difficultyBeatmaps)
            {
                Enum.TryParse(item.Value["_difficulty"], out BeatmapDifficulty difficulty);
                difficulties.Add(difficulty);
            }
            return difficulties.OrderBy(x => x).ToArray();
        }

        public int GetNoteCount(BeatmapDifficulty difficulty)
        {
            var infoText = File.ReadAllText(GetDifficultyPath(difficulty));
            JSONNode node = JSON.Parse(infoText);
            return node["_notes"].AsArray.Count;
        }

        public int GetMaxScore(BeatmapDifficulty difficulty)
        {
            int noteCount = GetNoteCount(difficulty);

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
            return num * 110;
        }

        //Returns the closest difficulty to the one provided, preferring lower difficulties first if any exist
        public BeatmapDifficulty GetClosestDifficultyPreferLower(BeatmapDifficulty difficulty) => GetClosestDifficultyPreferLower(CharacteristicType.Standard, difficulty);
        public BeatmapDifficulty GetClosestDifficultyPreferLower(CharacteristicType characteristic, BeatmapDifficulty difficulty)
        {
            if (GetBeatmapDifficulties(characteristic).Contains(difficulty)) return difficulty;

            int ret = -1;
            if (ret == -1)
            {
                ret = GetLowerDifficulty(characteristic, difficulty);
            }
            if (ret == -1)
            {
                ret = GetHigherDifficulty(characteristic, difficulty);
            }
            return (BeatmapDifficulty)ret;
        }

        //Returns the next-lowest difficulty to the one provided
        private int GetLowerDifficulty(CharacteristicType characteristic, BeatmapDifficulty difficulty)
        {
            return GetBeatmapDifficulties(characteristic).Select(x => (int)x).TakeWhile(x => x < (int)difficulty).DefaultIfEmpty(-1).Last();
        }

        //Returns the next-highest difficulty to the one provided
        private int GetHigherDifficulty(CharacteristicType characteristic, BeatmapDifficulty difficulty)
        {
            return GetBeatmapDifficulties(characteristic).Select(x => (int)x).SkipWhile(x => x < (int)difficulty).DefaultIfEmpty(-1).First();
        }

        private string GetInfoPath()
        {
            return Directory.GetFiles(GetSongRootDir(), "info.dat", SearchOption.AllDirectories).First(); //Assuming each song folder has only one info.json
        }

        private string GetDifficultyPath(BeatmapDifficulty difficulty)
        {
            return Directory.GetFiles(GetSongRootDir(), $"{difficulty}.dat", SearchOption.AllDirectories).First(); //Assuming each song folder has only one info.json
        }

        private string GetSongRootDir()
        {
            var idFolder = $"{songDirectory}{Hash}";
            var songFolder = Directory.GetDirectories(idFolder); //Assuming each id folder has only one song folder
            return songFolder.FirstOrDefault() ?? idFolder;
        }

        public static bool Exists(string hash)
        {
            return Directory.Exists($"{songDirectory}{hash}");
        }
    }
}
