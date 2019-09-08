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
            var idFolder = $"{songDirectory}{Hash}";
            var songFolder = Directory.GetDirectories(idFolder); //Assuming each id folder has only one song folder
            var subFolder = songFolder.FirstOrDefault() ?? idFolder;
            return Directory.GetFiles(subFolder, "info.dat", SearchOption.AllDirectories).First(); //Assuming each song folder has only one info.json
        }

        private string GetDifficultyPath(BeatmapDifficulty difficulty)
        {
            var idFolder = $"{songDirectory}{Hash}";
            var songFolder = Directory.GetDirectories(idFolder); //Assuming each id folder has only one song folder
            var subFolder = songFolder.FirstOrDefault() ?? idFolder;
            return Directory.GetFiles(subFolder, $"{difficulty}.dat", SearchOption.AllDirectories).First(); //Assuming each song folder has only one info.json
        }

        public static bool Exists(string hash)
        {
            return Directory.Exists($"{songDirectory}{hash}");
        }








        //Temp-------------------------------------------------------------------------------------------------------------------
        public class Obstacle
        {
            public double _time;
            public double _lineIndex;
            public double _type;
            public double _duration;
            public double _width;

            public Obstacle(double _time, double _lineIndex, double _type, double _duration, double _width)
            {
                this._time = _time;
                this._lineIndex = _lineIndex;
                this._type = _type;
                this._duration = _duration;
                this._width = _width;
            }
        }

        public Obstacle[] GetObstacles(BeatmapDifficulty difficulty)
        {
            var infoText = File.ReadAllText(GetDifficultyPath(difficulty));
            JSONNode node = JSON.Parse(infoText);

            List<Obstacle> obstacles = new List<Obstacle>();
            foreach (JSONNode obstacle in node["_obstacles"].AsArray)
            {
                obstacles.Add(new Obstacle(obstacle["_time"].AsDouble, obstacle["_lineIndex"].AsDouble, obstacle["_type"].AsDouble, obstacle["_duration"].AsDouble, obstacle["_width"].AsDouble));
            }

            return obstacles.ToArray();
        }

        public int GetAmountOf3WideWalls(Obstacle[] obstacles)
        {
            obstacles = obstacles.OrderBy(x => x._time).ToArray();

            int wideWalls = 0;
            int currentSavedWallsWidthSum = 0;
            var savedWalls = new List<Obstacle>();
            var groupedWalls = new Dictionary<double, List<Obstacle>>();

            bool wallEndsAfterAnySavedWall = false;

            foreach (var obstacle in obstacles)
            {
                int savedWallsToRemove = 0;
                foreach (var savedWall in savedWalls)
                {
                    if (!wallEndsAfterAnySavedWall && obstacle._time + obstacle._duration > savedWall._time + savedWall._duration)
                        wallEndsAfterAnySavedWall = true;

                    if (obstacle._time > savedWall._time + savedWall._duration)
                        savedWallsToRemove++;
                }

                for (int i = 0; i < savedWallsToRemove; i++)
                {
                    currentSavedWallsWidthSum -= (int)savedWalls.First()._width;
                    savedWalls.RemoveAt(0);
                }

                if (obstacle._type != 0)
                    continue;

                if (!groupedWalls.ContainsKey(obstacle._time))
                    groupedWalls.Add(obstacle._time, new List<Obstacle>());

                if (savedWalls.Count == 0 || wallEndsAfterAnySavedWall)
                {
                    savedWalls.Add(obstacle);
                    currentSavedWallsWidthSum += (int)obstacle._width;

                    foreach (var savedWall in savedWalls)
                        groupedWalls[obstacle._time].Add(savedWall);
                }

                if (!obstacle.Equals(savedWalls.Last()))
                    groupedWalls[obstacle._time].Add(obstacle);
            }

            foreach (var groupedWall in groupedWalls)
            {
                int widthSum = groupedWall.Value.Sum(x => (int)x._width);
                if (widthSum < 2)
                    continue;

                bool leftLaneCovered = false;
                bool rightLaneCovered = false;
                foreach (var wall in groupedWall.Value)
                {
                    if (wall._lineIndex == 0)
                        leftLaneCovered = true;

                    if (wall._width + wall._lineIndex > 3)
                        rightLaneCovered = true;
                }

                if (widthSum >= 3)
                {
                    if (!leftLaneCovered ^ !rightLaneCovered)
                        wideWalls++;
                }
                else
                {
                    if (!leftLaneCovered && !rightLaneCovered)
                        wideWalls++;
                }
            }

            return wideWalls;
        }
    }
}
