using IPA.Utilities;
using IPA.Utilities.Async;
using SongCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using static ScoreModel;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.Utilities
{
    public class SongUtils
    {
        private static BeatmapLevelsModel _beatmapLevelsModel;
        private static CancellationTokenSource getLevelCancellationTokenSource;
        private static CancellationTokenSource getStatusCancellationTokenSource;

        public static List<BeatmapLevel> masterLevelList;

        public static void OnEnable()
        {
            Loader.SongsLoadedEvent += Loader_SongsLoadedEvent;
        }

        private static void Loader_SongsLoadedEvent(Loader _, ConcurrentDictionary<string, BeatmapLevel> __)
        {
            RefreshLoadedSongs();
        }

        // Returns the closest difficulty to the one provided, preferring lower difficulties first if any exist
        public static BeatmapKey GetClosestDifficultyPreferLower(BeatmapLevel level, BeatmapDifficulty difficulty, string characteristic)
        {
            // First, look at the characteristic parameter. If there's something useful in there, we try to use it, but fall back to Standard
            var desiredCharacteristic = level.GetBeatmapKeys().FirstOrDefault(x => x.beatmapCharacteristic.serializedName == characteristic).beatmapCharacteristic ?? level.GetBeatmapKeys().First().beatmapCharacteristic;

            var availableMaps =
                level
                .GetBeatmapKeys()
                .Where(x => x.beatmapCharacteristic.serializedName == desiredCharacteristic.serializedName)
                .OrderBy(x => x.difficulty)
                .ToArray();

            BeatmapKey? ret = availableMaps.Cast<BeatmapKey?>().FirstOrDefault(x => x.Value.difficulty == difficulty);
            ret = ret != null && (!ret.Value.levelId.StartsWith("custom_level_") || HasRequirements(ret.Value)) ? ret : null;

            ret ??= GetLowerDifficulty(availableMaps, difficulty, desiredCharacteristic);
            ret ??= GetHigherDifficulty(availableMaps, difficulty, desiredCharacteristic);

            return ret.Value;
        }

        // Returns the next-lowest difficulty to the one provided
        private static BeatmapKey? GetLowerDifficulty(BeatmapKey[] availableMaps, BeatmapDifficulty difficulty, BeatmapCharacteristicSO characteristic)
        {
            BeatmapKey? ret = availableMaps.Cast<BeatmapKey?>().TakeWhile(x => x.Value.difficulty < difficulty).LastOrDefault();
            return ret != null && (!ret.Value.levelId.StartsWith("custom_level_") || HasRequirements(ret.Value)) ? ret : null;
        }

        // Returns the next-highest difficulty to the one provided
        private static BeatmapKey? GetHigherDifficulty(BeatmapKey[] availableMaps, BeatmapDifficulty difficulty, BeatmapCharacteristicSO characteristic)
        {
            BeatmapKey? ret = availableMaps.Cast<BeatmapKey?>().SkipWhile(x => x.Value.difficulty < difficulty).FirstOrDefault();
            return ret != null && (!ret.Value.levelId.StartsWith("custom_level_") || HasRequirements(ret.Value)) ? ret : null;
        }

        public static void RefreshLoadedSongs()
        {
            if (_beatmapLevelsModel == null)
            {
                var mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
                _beatmapLevelsModel = mainFlowCoordinator.GetField<BeatmapLevelsModel>("_beatmapLevelsModel");
            }

            masterLevelList = new List<BeatmapLevel>();

            foreach (var pack in _beatmapLevelsModel.GetAllPacks())
            {
                masterLevelList.AddRange(pack.AllBeatmapLevels());
            }

            // This snippet helps me build the hardcoded list that ends up in OstHelper.cs
            /*var output = string.Join("\n", _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.Select(x => $@"
            new Pack
            {{
                PackID = ""{x.packID}"",
                PackName = ""{x.packName}"",
                SongDictionary = new Dictionary<string, string>
                {{{
                                string.Join(",\n", x.beatmapLevelCollection.beatmapLevels.Select(y => $"{{\"{y.levelID}\", \"{y.songName}\"}}").ToArray())
                }}}
            }},
            "));
            File.WriteAllText(Environment.CurrentDirectory + "\\songs.json", output);*/
        }

        public static bool HasRequirements(BeatmapKey key)
        {
            var extras = Collections.RetrieveExtraSongData(key.levelId);
            var requirements = extras?._difficulties.First(x => x._difficulty == key.difficulty).additionalDifficultyData._requirements;

            Logger.Debug($"{key.levelId} is a custom level, checking for requirements on {key.difficulty}...");
            if ((requirements?.Count() > 0) && !requirements.All(Collections.capabilities.Contains))
            {
                Logger.Debug($"At leat one requirement not met: {string.Join(" ", requirements)}");
                return false;
            }
            Logger.Debug("Requirements met");
            return true;
        }

        public static float GetJumpDistance(float bpm, float njs, float offset)
        {
            float hjd(float bpm, float njs, float offset)
            {
                var num = 60f / bpm;
                var hjd = 4f;
                while (njs * num * hjd > 17.999f)
                    hjd /= 2f;

                hjd += offset;

                return Math.Max(hjd, 0.25f);
            }
            return njs * (60f / bpm) * hjd(bpm, njs, offset) * 2;
        }

        public static async Task<bool> HasDLCLevel(string levelId)
        {
            BeatmapLevelsModel beatmapLevelsModel = null;

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                var mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
                beatmapLevelsModel = mainFlowCoordinator.GetField<BeatmapLevelsModel>("_beatmapLevelsModel");
            });

            if (beatmapLevelsModel != null)
            {
                getStatusCancellationTokenSource?.Cancel();
                getStatusCancellationTokenSource = new CancellationTokenSource();

                var token = getStatusCancellationTokenSource.Token;
                return await beatmapLevelsModel.entitlements.GetLevelEntitlementStatusAsync(levelId, token) == EntitlementStatus.Owned;
            }

            return false;
        }

        public static async void PlaySong(BeatmapKey key, OverrideEnvironmentSettings overrideEnvironmentSettings = null, ColorScheme colorScheme = null, GameplayModifiers gameplayModifiers = null, PlayerSpecificSettings playerSettings = null, Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> songFinishedCallback = null, Action<LevelScenesTransitionSetupDataSO, LevelCompletionResults> songRestartedCallback = null)
        {
            var level = masterLevelList.FirstOrDefault(x => x.levelID.ToUpper() == key.levelId.ToUpper());

            if (await HasDLCLevel(key.levelId))
            {
                // TODO: Maybe I should actually use the starter?
                var simpleLevelStarter = Resources.FindObjectsOfTypeAll<SimpleLevelStarter>().First();

                // Try to get overridden colors if this is a custom level
                ColorScheme beatmapOverrideColorScheme = null;
                /*if (level.levelID.StartsWith("custom_level_"))
                {
                    beatmapOverrideColorScheme = level.GetColorScheme(characteristic, key.difficulty);
                }*/

                MenuTransitionsHelper _menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuTransitionsHelper>().First();
                _menuSceneSetupData.StartStandardLevel(
                    "Solo",
                    key,
                    level,
                    overrideEnvironmentSettings,
                    colorScheme,
                    beatmapOverrideColorScheme,
                    gameplayModifiers ?? new GameplayModifiers(),
                    playerSettings ?? new PlayerSpecificSettings(),
                    null,
                    simpleLevelStarter.GetField<EnvironmentsListModel>("_environmentsListModel"),
                    "Menu",
                    false,
                    false,  /* TODO: start paused? Worth looking into to replace the old hacky function */
                    null,
                    null,
                    (standardLevelScenesTransitionSetupData, results) => songFinishedCallback?.Invoke(standardLevelScenesTransitionSetupData, results),
                    (levelScenesTransitionSetupData, results) => songRestartedCallback?.Invoke(levelScenesTransitionSetupData, results)
                );
            }
            else
            {
                Logger.Debug($"Skipping unowned DLC ({level.songName})");
            }
        }

        public static int ComputeMaxMultipliedScoreForBeatmap(IReadonlyBeatmapData beatmapData, bool filterByColorType = false, ColorType colorType = ColorType.ColorA)
        {

            ScoreMultiplierCounter scoreMultiplierCounter = new ScoreMultiplierCounter();
            IEnumerable<NoteData> beatmapDataItems = beatmapData.GetBeatmapDataItems<NoteData>(0);
            IEnumerable<SliderData> beatmapDataItems2 = beatmapData.GetBeatmapDataItems<SliderData>(0);
            List<MaxScoreCounterElement> list = new List<MaxScoreCounterElement>(1000);
            foreach (NoteData item in beatmapDataItems)
            {
                if (item.scoringType != NoteData.ScoringType.Ignore && item.scoringType != NoteData.ScoringType.NoScore &&
                    !(filterByColorType && item.colorType == colorType))
                {
                    list.Add(new MaxScoreCounterElement(item.scoringType, item.time));
                }
            }
            foreach (SliderData item2 in beatmapDataItems2)
            {
                if (item2.sliderType == SliderData.Type.Burst)
                {
                    for (int i = 1; i < item2.sliceCount; i++)
                    {
                        float t = (float)i / (float)(item2.sliceCount - 1);
                        list.Add(new MaxScoreCounterElement(NoteData.ScoringType.BurstSliderElement, Mathf.LerpUnclamped(item2.time, item2.tailTime, t)));
                    }
                }
            }
            list.Sort();
            int num = 0;
            scoreMultiplierCounter.Reset();
            foreach (MaxScoreCounterElement item3 in list)
            {
                scoreMultiplierCounter.ProcessMultiplierEvent(ScoreMultiplierCounter.MultiplierEventType.Positive);
                num += item3.noteScoreDefinition.maxCutScore * scoreMultiplierCounter.multiplier;
            }
            return num;
        }

        public static int GetModifiedScoreForGameplayModifiersScoreMultiplier(int multipliedScore, float gameplayModifiersScoreMultiplier)
        {
            return Mathf.FloorToInt((float)multipliedScore * gameplayModifiersScoreMultiplier);
        }

        private class MaxScoreCounterElement : IComparable<MaxScoreCounterElement>
        {
            public readonly NoteScoreDefinition noteScoreDefinition;

            private readonly float time;

            public MaxScoreCounterElement(NoteData.ScoringType scoringType, float time)
            {
                this.time = time;
                noteScoreDefinition = GetNoteScoreDefinition(scoringType);
            }

            public int CompareTo(MaxScoreCounterElement other)
            {
                float num = time;
                int num2 = num.CompareTo(other.time);
                if (num2 == 0)
                {
                    return noteScoreDefinition.executionOrder.CompareTo(other.noteScoreDefinition.executionOrder);
                }
                return num2;
            }
        }
    }
}
