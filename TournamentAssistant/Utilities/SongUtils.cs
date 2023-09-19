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
using IPA.Utilities;

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

            var availableMaps =
                level
                .beatmapLevelData
                .difficultyBeatmapSets
                .FirstOrDefault(x => x.beatmapCharacteristic.serializedName == desiredCharacteristic.serializedName)
                .difficultyBeatmaps
                .OrderBy(x => x.difficulty)
                .ToArray();

            var ret = availableMaps.FirstOrDefault(x => x.difficulty == difficulty);
            ret = ret is not CustomDifficultyBeatmap || HasRequirements(ret) ? ret : null;

            ret ??= GetLowerDifficulty(availableMaps, difficulty, desiredCharacteristic);
            ret ??= GetHigherDifficulty(availableMaps, difficulty, desiredCharacteristic);

            return ret;
        }

        //Returns the next-lowest difficulty to the one provided
        private static IDifficultyBeatmap GetLowerDifficulty(IDifficultyBeatmap[] availableMaps, BeatmapDifficulty difficulty, BeatmapCharacteristicSO characteristic)
        {
            var ret = availableMaps.TakeWhile(x => x.difficulty < difficulty).LastOrDefault();
            return ret is not CustomDifficultyBeatmap || HasRequirements(ret) ? ret : null;
        }

        //Returns the next-highest difficulty to the one provided
        private static IDifficultyBeatmap GetHigherDifficulty(IDifficultyBeatmap[] availableMaps, BeatmapDifficulty difficulty, BeatmapCharacteristicSO characteristic)
        {
            var ret = availableMaps.SkipWhile(x => x.difficulty < difficulty).FirstOrDefault();
            return ret is not CustomDifficultyBeatmap || HasRequirements(ret) ? ret : null;
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

        public static bool HasRequirements(IDifficultyBeatmap map)
        {
            var extras = Collections.RetrieveExtraSongData(map.level.levelID);
            var requirements = extras?._difficulties.First(x => x._difficulty == map.difficulty).additionalDifficultyData._requirements;

            Logger.Debug($"{map.level.songName} is a custom level, checking for requirements on {map.difficulty}...");
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

        public static async Task<bool> HasDLCLevel(string levelId, AdditionalContentModel additionalContentModel = null)
        {
            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                additionalContentModel ??= Resources.FindObjectsOfTypeAll<AdditionalContentModel>().FirstOrDefault();
            }).ConfigureAwait(false);

            if (additionalContentModel != null)
            {
                getStatusCancellationTokenSource?.Cancel();
                getStatusCancellationTokenSource = new CancellationTokenSource();

                var token = getStatusCancellationTokenSource.Token;
                return await additionalContentModel.GetLevelEntitlementStatusAsync(levelId, token) == AdditionalContentModel.EntitlementStatus.Owned;
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

                    if (result?.isError == true || result?.beatmapLevel == null)
                    {
                        return null; //Null out entirely in case of error
                    }

                    return result;
                }
                return null;
            }).Unwrap();
        }

        public static async void PlaySong(IPreviewBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, OverrideEnvironmentSettings overrideEnvironmentSettings = null, ColorScheme colorScheme = null, GameplayModifiers gameplayModifiers = null, PlayerSpecificSettings playerSettings = null, Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> songFinishedCallback = null, Action<LevelScenesTransitionSetupDataSO, LevelCompletionResults> songRestartedCallback = null)
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
                    false,  /* TODO: start paused? Worth looking into to replace the old hacky function */
                    null,
                    (standardLevelScenesTransitionSetupData, results) => songFinishedCallback?.Invoke(standardLevelScenesTransitionSetupData, results),
                    (levelScenesTransitionSetupData, results) => songRestartedCallback?.Invoke(levelScenesTransitionSetupData, results)
                );
            };

            if ((level is PreviewBeatmapLevelSO && await HasDLCLevel(level.levelID)) || level is CustomPreviewBeatmapLevel)
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
