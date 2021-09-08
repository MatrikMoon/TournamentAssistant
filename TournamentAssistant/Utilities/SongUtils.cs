using SongCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared;
using UnityEngine;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.Utilities
{
    public class SongUtils
    {
        private static AlwaysOwnedContentSO _alwaysOwnedContent;
        private static BeatmapLevelCollectionSO _primaryLevelCollection;
        private static BeatmapLevelCollectionSO _secondaryLevelCollection;
        private static BeatmapLevelCollectionSO _tertiaryLevelCollection;
        private static BeatmapLevelCollectionSO _extrasLevelCollection;
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
            if (_alwaysOwnedContent == null) _alwaysOwnedContent = Resources.FindObjectsOfTypeAll<AlwaysOwnedContentSO>().First();
            if (_primaryLevelCollection == null) _primaryLevelCollection = _alwaysOwnedContent.alwaysOwnedPacks.First(x => x.packID == OstHelper.packs[0].PackID).beatmapLevelCollection as BeatmapLevelCollectionSO;
            if (_secondaryLevelCollection == null) _secondaryLevelCollection = _alwaysOwnedContent.alwaysOwnedPacks.First(x => x.packID == OstHelper.packs[1].PackID).beatmapLevelCollection as BeatmapLevelCollectionSO;
            if (_tertiaryLevelCollection == null) _tertiaryLevelCollection = _alwaysOwnedContent.alwaysOwnedPacks.First(x => x.packID == OstHelper.packs[2].PackID).beatmapLevelCollection as BeatmapLevelCollectionSO;
            if (_extrasLevelCollection == null) _extrasLevelCollection = _alwaysOwnedContent.alwaysOwnedPacks.First(x => x.packID == OstHelper.packs[3].PackID).beatmapLevelCollection as BeatmapLevelCollectionSO;

            masterLevelList = new List<IPreviewBeatmapLevel>();
            masterLevelList.AddRange(_primaryLevelCollection.beatmapLevels);
            masterLevelList.AddRange(_secondaryLevelCollection.beatmapLevels);
            masterLevelList.AddRange(_tertiaryLevelCollection.beatmapLevels);
            masterLevelList.AddRange(_extrasLevelCollection.beatmapLevels);
            masterLevelList.AddRange(Loader.CustomLevelsCollection.beatmapLevels);
        }

        public static async Task<bool> HasDLCLevel(string levelId, AdditionalContentModel additionalContentModel = null)
        {
            additionalContentModel = additionalContentModel ?? Resources.FindObjectsOfTypeAll<AdditionalContentModel>().FirstOrDefault();
            if (additionalContentModel != null)
            {
                getStatusCancellationTokenSource?.Cancel();
                getStatusCancellationTokenSource = new CancellationTokenSource();

                var token = getStatusCancellationTokenSource.Token;
                return await additionalContentModel.GetLevelEntitlementStatusAsync(levelId, token) == AdditionalContentModel.EntitlementStatus.Owned;
            }

            return false;
        }

        public static async Task<BeatmapLevelsModel.GetBeatmapLevelResult?> GetLevelFromPreview(IPreviewBeatmapLevel level, BeatmapLevelsModel beatmapLevelsModel = null)
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
        }

        public static async void PlaySong(IPreviewBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, OverrideEnvironmentSettings overrideEnvironmentSettings = null, ColorScheme colorScheme = null, GameplayModifiers gameplayModifiers = null, PlayerSpecificSettings playerSettings = null, Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> songFinishedCallback = null)
        {
            Action<IBeatmapLevel> SongLoaded = (loadedLevel) =>
            {
                MenuTransitionsHelper _menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuTransitionsHelper>().First();
                _menuSceneSetupData.StartStandardLevel(
                    "Solo",
                    loadedLevel.beatmapLevelData.GetDifficultyBeatmap(characteristic, difficulty),
                    loadedLevel,
                    overrideEnvironmentSettings,
                    colorScheme,
                    gameplayModifiers ?? new GameplayModifiers(),
                    playerSettings ?? new PlayerSpecificSettings(),
                    null,
                    "Menu",
                    false,
                    null,
                    (standardLevelScenesTransitionSetupData, results) => songFinishedCallback?.Invoke(standardLevelScenesTransitionSetupData, results)
                );
            };

            if ((level is PreviewBeatmapLevelSO && await HasDLCLevel(level.levelID)) ||
                        level is CustomPreviewBeatmapLevel)
            {
                Logger.Debug("Loading DLC/Custom level...");
                var result = await GetLevelFromPreview(level);
                if (result != null && !(result?.isError == true))
                {
                    //HTTPstatus requires cover texture to be applied in here, and due to a fluke
                    //of beat saber, it's not applied when the level is loaded, but it *is*
                    //applied to the previewlevel it's loaded from
                    var loadedLevel = result?.beatmapLevel;
                    loadedLevel.SetField("_coverImage", level.GetField<Sprite>("_coverImage"));
                    SongLoaded(loadedLevel);
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

        public static async void LoadSong(string levelId, Action<IBeatmapLevel> loadedCallback)
        {
            IPreviewBeatmapLevel level = masterLevelList.Where(x => x.levelID == levelId).First();

            //Load IBeatmapLevel
            if (level is PreviewBeatmapLevelSO || level is CustomPreviewBeatmapLevel)
            {
                if (level is PreviewBeatmapLevelSO)
                {
                    if (!await HasDLCLevel(level.levelID)) return; //In the case of unowned DLC, just bail out and do nothing
                }

                var result = await GetLevelFromPreview(level);
                if (result != null && !(result?.isError == true))
                {
                    //HTTPstatus requires cover texture to be applied in here, and due to a fluke
                    //of beat saber, it's not applied when the level is loaded, but it *is*
                    //applied to the previewlevel it's loaded from
                    var loadedLevel = result?.beatmapLevel;
                    loadedLevel.SetField("_coverImage", level.GetField<Sprite>("_coverImage"));
                    loadedCallback(loadedLevel);
                }
            }
            else if (level is BeatmapLevelSO)
            {
                loadedCallback(level as IBeatmapLevel);
            }
        }
    }
}
