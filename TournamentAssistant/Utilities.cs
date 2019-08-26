using SongCore;
using SongCore.Utilities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant
{
    class Utilities
    {
        private static CancellationTokenSource getLevelCancellationTokenSource;
        private static CancellationTokenSource getStatusCancellationTokenSource;

        public static async void PlaySong(IPreviewBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers gameplayModifiers = null, PlayerSpecificSettings playerSettings = null)
        {
            Action<IBeatmapLevel> SongLoaded = (loadedLevel) =>
            {
                MenuTransitionsHelperSO _menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuTransitionsHelperSO>().First();
                _menuSceneSetupData.StartStandardLevel(
                    loadedLevel.beatmapLevelData.GetDifficultyBeatmap(characteristic, difficulty),
                    gameplayModifiers ?? new GameplayModifiers(),
                    playerSettings ?? new PlayerSpecificSettings(),
                    null,
                    "Menu",
                    false,
                    null,
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

        public static void ReturnToMenu()
        {
            Resources.FindObjectsOfTypeAll<StandardLevelScenesTransitionSetupDataSO>().FirstOrDefault()?.PopScenes(0.35f);
        }

        //Returns the closest difficulty to the one provided, preferring lower difficulties first if any exist
        public static IDifficultyBeatmap GetClosestDifficultyPreferLower(IBeatmapLevel level, BeatmapDifficulty difficulty, BeatmapCharacteristicSO characteristic = null)
        {
            //First, look at the characteristic parameter. If there's something useful in there, we try to use it, but fall back to Standard
            var desiredCharacteristic = level.beatmapCharacteristics.FirstOrDefault(x => x.serializedName == (characteristic?.serializedName ?? "Standard")) ?? level.beatmapCharacteristics.First();

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
                if (
                    (requirements?.Count() > 0) &&
                    (!requirements?.ToList().All(x => Collections.capabilities.Contains(x)) ?? false)
                ) ret = null;
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
                if (
                    (requirements?.Count() > 0) &&
                    (!requirements?.ToList().All(x => Collections.capabilities.Contains(x)) ?? false)
                ) ret = null;
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
                if (
                    (requirements?.Count() > 0) &&
                    (!requirements?.ToList().All(x => Collections.capabilities.Contains(x)) ?? false)
                ) ret = null;
            }
            return ret;
        }

        public static async Task<bool> HasDLCLevel(string levelId, AdditionalContentModelSO additionalContentModel = null)
        {
            additionalContentModel = additionalContentModel ?? Resources.FindObjectsOfTypeAll<AdditionalContentModelSO>().FirstOrDefault();
            var additionalContentHandler = additionalContentModel?.GetField<IPlatformAdditionalContentHandler>("_platformAdditionalContentHandler");

            if (additionalContentHandler != null)
            {
                getStatusCancellationTokenSource?.Cancel();
                getStatusCancellationTokenSource = new CancellationTokenSource();

                var token = getStatusCancellationTokenSource.Token;
                return await additionalContentHandler.GetLevelEntitlementStatusAsync(levelId, token) == AdditionalContentModelSO.EntitlementStatus.Owned;
            }

            return false;
        }

        public static async Task<BeatmapLevelsModelSO.GetBeatmapLevelResult?> GetLevelFromPreview(IPreviewBeatmapLevel level, BeatmapLevelsModelSO beatmapLevelsModel = null)
        {
            Logger.Debug("GET LEVEL FROM PREVIEW");
            beatmapLevelsModel = beatmapLevelsModel ?? Resources.FindObjectsOfTypeAll<BeatmapLevelsModelSO>().FirstOrDefault();

            if (beatmapLevelsModel != null)
            {
                getLevelCancellationTokenSource?.Cancel();
                getLevelCancellationTokenSource = new CancellationTokenSource();

                var token = getLevelCancellationTokenSource.Token;

                BeatmapLevelsModelSO.GetBeatmapLevelResult? result = null;
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

        public static async void LoadSong(string levelId, Action<IBeatmapLevel> loadedCallback)
        {
            Logger.Debug("LOAD SONG");
            IPreviewBeatmapLevel level = Plugin.masterLevelList.Where(x => x.levelID == levelId).First();

            //Load IBeatmapLevel
            if (level is PreviewBeatmapLevelSO || level is CustomPreviewBeatmapLevel)
            {
                if (level is PreviewBeatmapLevelSO)
                {
                    if (!await HasDLCLevel(level.levelID)) return; //In the case of unowned DLC, just bail out and do nothing
                }

                var map = ((CustomPreviewBeatmapLevel)level).standardLevelInfoSaveData.difficultyBeatmapSets.First().difficultyBeatmaps.First();

                var result = await GetLevelFromPreview(level);
                if (result != null && !(result?.isError == true))
                {
                    loadedCallback(result?.beatmapLevel);
                }
            }
            else if (level is BeatmapLevelSO)
            {
                loadedCallback(level as IBeatmapLevel);
            }
        }
    }
}
