using SongCore;
using IPA.Utilities.Async;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Logger = TournamentAssistantShared.Logger;
using static AdditionalContentModel;

namespace TournamentAssistant.Utilities
{
    public class SongUtils
    {
        private static BeatmapLevelsModel _beatmapLevelsModel;
        private static CancellationTokenSource getLevelCancellationTokenSource;
        private static CancellationTokenSource getStatusCancellationTokenSource;

        public static List<IPreviewBeatmapLevel> masterLevelList;

        public static void OnEnable()
        {
            Loader.SongsLoadedEvent += Loader_SongsLoadedEvent;
        }

        private static void Loader_SongsLoadedEvent(Loader _, ConcurrentDictionary<string, CustomPreviewBeatmapLevel> __)
        {
            RefreshLoadedSongs();
        }

        //Returns the closest difficulty to the one provided, preferring lower difficulties first if any exist
        public static IDifficultyBeatmap GetClosestDifficultyPreferLower(IBeatmapLevel level, BeatmapDifficulty difficulty, string characteristic)
        {
            //First, look at the characteristic parameter. If there's something useful in there, we try to use it, but fall back to Standard
            var desiredCharacteristic = level.previewDifficultyBeatmapSets.FirstOrDefault(x => x.beatmapCharacteristic.serializedName == characteristic).beatmapCharacteristic ?? level.previewDifficultyBeatmapSets.First().beatmapCharacteristic;

            IDifficultyBeatmap[] availableMaps =
                level
                .beatmapLevelData
                .difficultyBeatmapSets
                .FirstOrDefault(x => x.beatmapCharacteristic.serializedName == desiredCharacteristic.serializedName)
                .difficultyBeatmaps
                .OrderBy(x => x.difficulty)
                .ToArray();

            IDifficultyBeatmap ret = availableMaps.FirstOrDefault(x => x.difficulty == difficulty);
            if (ret is CustomDifficultyBeatmap)
            {
                var extras = Collections.RetrieveExtraSongData(ret.level.levelID);
                var requirements = extras?._difficulties.First(x => x._difficulty == ret.difficulty).additionalDifficultyData._requirements;
                //Logger.Debug($"{ret.level.songName} is a custom level, checking for requirements on {ret.difficulty}...");
                if (
                    (requirements?.Count() > 0) &&
                    (!requirements?.ToList().All(x => Collections.capabilities.Contains(x)) ?? false)
                ) ret = null;
                //Logger.Debug((ret == null ? "Requirement not met." : "Requirement met!"));
            }

            if (ret == null)
            {
                ret = GetLowerDifficulty(availableMaps, difficulty, desiredCharacteristic);
            }
            if (ret == null)
            {
                ret = GetHigherDifficulty(availableMaps, difficulty, desiredCharacteristic);
            }

            return ret;
        }

        //Returns the next-lowest difficulty to the one provided
        private static IDifficultyBeatmap GetLowerDifficulty(IDifficultyBeatmap[] availableMaps, BeatmapDifficulty difficulty, BeatmapCharacteristicSO characteristic)
        {
            var ret = availableMaps.TakeWhile(x => x.difficulty < difficulty).LastOrDefault();
            if (ret is CustomDifficultyBeatmap)
            {
                var extras = Collections.RetrieveExtraSongData(ret.level.levelID);
                var requirements = extras?._difficulties.First(x => x._difficulty == ret.difficulty).additionalDifficultyData._requirements;
                Logger.Debug($"{ret.level.songName} is a custom level, checking for requirements on {ret.difficulty}...");
                if (
                    (requirements?.Count() > 0) &&
                    (!requirements?.ToList().All(x => Collections.capabilities.Contains(x)) ?? false)
                ) ret = null;
                Logger.Debug((ret == null ? "Requirement not met." : "Requirement met!"));
            }
            return ret;
        }

        //Returns the next-highest difficulty to the one provided
        private static IDifficultyBeatmap GetHigherDifficulty(IDifficultyBeatmap[] availableMaps, BeatmapDifficulty difficulty, BeatmapCharacteristicSO characteristic)
        {
            var ret = availableMaps.SkipWhile(x => x.difficulty < difficulty).FirstOrDefault();
            if (ret is CustomDifficultyBeatmap)
            {
                var extras = Collections.RetrieveExtraSongData(ret.level.levelID);
                var requirements = extras?._difficulties.First(x => x._difficulty == ret.difficulty).additionalDifficultyData._requirements;
                Logger.Debug($"{ret.level.songName} is a custom level, checking for requirements on {ret.difficulty}...");
                if (
                    (requirements?.Count() > 0) &&
                    (!requirements?.ToList().All(x => Collections.capabilities.Contains(x)) ?? false)
                ) ret = null;
                Logger.Debug((ret == null ? "Requirement not met." : "Requirement met!"));
            }
            return ret;
        }

        public static void RefreshLoadedSongs()
        {
            if (_beatmapLevelsModel == null) _beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().First();

            masterLevelList = new List<IPreviewBeatmapLevel>();

            foreach (var pack in _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks)
            {
                masterLevelList.AddRange(pack.beatmapLevelCollection.beatmapLevels);
            }

            //This snippet helps me build the hardcoded list that ends up in OstHelper.cs
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
            AdditionalContentModel additionalContentModel = null;

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                var beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().FirstOrDefault();
                additionalContentModel = beatmapLevelsModel.GetField<AdditionalContentModel>("_additionalContentModel");
            }).ConfigureAwait(false);
            if (additionalContentModel != null)
            {
                getStatusCancellationTokenSource?.Cancel();
                getStatusCancellationTokenSource = new CancellationTokenSource();

                var token = getStatusCancellationTokenSource.Token;
                return await additionalContentModel.GetLevelEntitlementStatusAsync(levelId, token) == EntitlementStatus.Owned;
            }

            return false;
        }

        public static Task<BeatmapLevelsModel.GetBeatmapLevelResult?> GetLevelFromPreview(IPreviewBeatmapLevel level, BeatmapLevelsModel beatmapLevelsModel = null)
        {
            return UnityMainThreadTaskScheduler.Factory.StartNew(async () =>
            {
                beatmapLevelsModel = beatmapLevelsModel ?? Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().FirstOrDefault();

                if (beatmapLevelsModel != null)
                {
                    getLevelCancellationTokenSource?.Cancel();
                    getLevelCancellationTokenSource = new CancellationTokenSource();

                    var token = getLevelCancellationTokenSource.Token;

                    BeatmapLevelsModel.GetBeatmapLevelResult? result = null;
                    try
                    {
                        result = await beatmapLevelsModel.GetBeatmapLevelAsync(level.levelID, token);
                    }
                    catch (OperationCanceledException) { }
                    if (result?.isError == true || result?.beatmapLevel == null) return null; //Null out entirely in case of error
                    return result;
                }
                return null;
            }).Unwrap();
        }

        public static async void PlaySong(IPreviewBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, OverrideEnvironmentSettings overrideEnvironmentSettings = null, ColorScheme colorScheme = null, GameplayModifiers gameplayModifiers = null, PlayerSpecificSettings playerSettings = null, Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> songFinishedCallback = null)
        {
            Action<IBeatmapLevel> SongLoaded = (loadedLevel) =>
            {
                var difficultyBeatmap = loadedLevel.beatmapLevelData.GetDifficultyBeatmap(characteristic, difficulty);

                // Try to get overridden colors if this is a custom level
                ColorScheme beatmapOverrideColorScheme = null;
                CustomBeatmapLevel customBeatmapLevel = loadedLevel as CustomBeatmapLevel;
                if (customBeatmapLevel != null)
                {
                    CustomDifficultyBeatmap customDifficultyBeatmap = difficultyBeatmap as CustomDifficultyBeatmap;
                    if (customDifficultyBeatmap != null)
                    {
                        beatmapOverrideColorScheme = customBeatmapLevel.GetBeatmapLevelColorScheme(customDifficultyBeatmap.beatmapColorSchemeIdx);
                    }
                }

                MenuTransitionsHelper _menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuTransitionsHelper>().First();
                _menuSceneSetupData.StartStandardLevel(
                    "Solo",
                    difficultyBeatmap,
                    loadedLevel,
                    overrideEnvironmentSettings,
                    colorScheme,
                    beatmapOverrideColorScheme,
                    gameplayModifiers ?? new GameplayModifiers(),
                    playerSettings ?? new PlayerSpecificSettings(),
                    null,
                    "Menu",
                    false,
                    false,  /* TODO: start paused? Worth looking into to replace the old hacky function */
                    null,
                    (standardLevelScenesTransitionSetupData, results) => songFinishedCallback?.Invoke(standardLevelScenesTransitionSetupData, results),
                    null
                );
            };

            if ((level is PreviewBeatmapLevelSO && await HasDLCLevel(level.levelID)) ||
                        level is CustomPreviewBeatmapLevel)
            {
                Logger.Debug("Loading DLC/Custom level...");
                var result = await GetLevelFromPreview(level);
                if (result != null && !(result?.isError == true))
                {
                    SongLoaded(result?.beatmapLevel);
                }
            }
            else if (level is BeatmapLevelSO)
            {
                Logger.Debug("Reading OST data without songloader...");
                SongLoaded(level as IBeatmapLevel);
            }
            else
            {
                Logger.Debug($"Skipping unowned DLC ({level.songName})");
            }
        }

        public static async Task<IBeatmapLevel> LoadSong(string levelId)
        {
            IPreviewBeatmapLevel level = masterLevelList.Where(x => x.levelID == levelId).First();

            //Load IBeatmapLevel
            if (level is PreviewBeatmapLevelSO || level is CustomPreviewBeatmapLevel)
            {
                if (level is PreviewBeatmapLevelSO)
                {
                    if (!await HasDLCLevel(level.levelID))
                    {
                        //In the case of unowned DLC, just bail out and do nothing
                        return null;
                    }
                }

                var result = await GetLevelFromPreview(level).ConfigureAwait(false);
                if (result != null && !(result?.isError == true))
                {
                    return result?.beatmapLevel;
                }
            }
            else if (level is BeatmapLevelSO)
            {
                return level as IBeatmapLevel;
            }
            return null;
        }
    }
}
