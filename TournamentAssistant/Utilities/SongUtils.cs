using IPA.Utilities;
using IPA.Utilities.Async;
using SongCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared.BeatSaver;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using static BeatSaberMarkupLanguage.Components.KEYBOARD;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.Utilities
{
    public class SongUtils
    {
        internal static BeatmapLevelsModel BeatmapLevelsModel
        {
            get
            {
                if(_beatmapLevelsModel == null)
                    _beatmapLevelsModel = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First().GetField<BeatmapLevelsModel>("_beatmapLevelsModel");
                return _beatmapLevelsModel;
            }
        }

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
            var beatmapCharacteristic = Loader.beatmapCharacteristicCollection.GetBeatmapCharacteristicBySerializedName(characteristic);

            // First, look at the characteristic parameter. If there's something useful in there, we try to use it, but fall back to Standard

            

            var availableMaps = level.GetBeatmapKeys();
            return availableMaps.Where(x => x.beatmapCharacteristic == beatmapCharacteristic && x.difficulty == difficulty).FirstOrDefault();
            /*
            if (level.GetDifficultyBeatmapData(beatmapCharacteristic, difficulty) != null)


            var ret = availableMaps.FirstOrDefault(x => x.difficulty == difficulty);
            ret = ret is not CustomDifficultyBeatmap || HasRequirements(ret) ? ret : null;

            ret = GetLowerDifficulty(availableMaps, difficulty);
            ret ??= GetHigherDifficulty(availableMaps, difficulty);

            return ret;*/
        }

        private static bool IsCustomLevel(BeatmapLevel level)
        {
            return level.levelID.StartsWith("custom_level");
        }

        // Returns the next-lowest difficulty to the one provided
        private static BeatmapKey GetLowerDifficulty(IEnumerable<BeatmapKey> availableMaps, BeatmapDifficulty difficulty)
        {
            return availableMaps.TakeWhile(x => x.difficulty < difficulty).LastOrDefault();
        }

        // Returns the next-highest difficulty to the one provided
        private static BeatmapKey GetHigherDifficulty(IEnumerable<BeatmapKey> availableMaps, BeatmapDifficulty difficulty)
        {
            return availableMaps.SkipWhile(x => x.difficulty < difficulty).FirstOrDefault();
        }

        public static IEnumerable<BeatmapLevel> GetAllLevelsFromRepository(BeatmapLevelsRepository repository)
        {
            return repository.GetField<Dictionary<string, BeatmapLevel>>("_idToBeatmapLevel").Values;
        }

        public static void RefreshLoadedSongs()
        {
            masterLevelList =
            [
                .. GetAllLevelsFromRepository(BeatmapLevelsModel.ostAndExtrasBeatmapLevelsRepository),
                .. GetAllLevelsFromRepository(BeatmapLevelsModel.dlcBeatmapLevelsRepository),
                .. GetAllLevelsFromRepository(Loader.CustomLevelsRepository),
            ];



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

        public static bool HasRequirements(BeatmapLevel map, BeatmapKey key)
        {
            var extras = Collections.RetrieveExtraSongData(map.levelID);
            var requirements = extras?._difficulties.First(x => x._difficulty == key.difficulty).additionalDifficultyData._requirements;

            Logger.Debug($"{map.songName} is a custom level, checking for requirements on {key.difficulty}...");
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
            AdditionalContentModel additionalContentModel = null;

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                additionalContentModel = BeatmapLevelsModel.GetField<AdditionalContentModel>("_additionalContentModel");
            });

            if (additionalContentModel != null)
            {
                getStatusCancellationTokenSource?.Cancel();
                getStatusCancellationTokenSource = new CancellationTokenSource();

                var token = getStatusCancellationTokenSource.Token;
                return await additionalContentModel.GetLevelEntitlementStatusAsync(levelId, token) == EntitlementStatus.Owned;
            }

            return false;
        }

        /*
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
        }*/

        public static async void PlaySong(BeatmapLevel level, BeatmapKey beatmapKey, OverrideEnvironmentSettings overrideEnvironmentSettings = null, ColorScheme colorScheme = null, GameplayModifiers gameplayModifiers = null, PlayerSpecificSettings playerSettings = null, Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> songFinishedCallback = null, Action<LevelScenesTransitionSetupDataSO, LevelCompletionResults> songRestartedCallback = null)
        {
            Action<BeatmapLevel> SongLoaded = (loadedLevel) =>
            {

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
                var _playerSettings = Resources.FindObjectsOfTypeAll<PlayerDataModel>().FirstOrDefault().playerData;
               // var _soloFreePlayFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();

                if (gameplayModifiers == null)
                {
                    gameplayModifiers = new GameplayModifiers();
                    gameplayModifiers.IsWithoutModifiers();
                }
                if (playerSettings == null)
                    playerSettings = _playerSettings.playerSpecificSettings;


                MenuTransitionsHelper _menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuTransitionsHelper>().First();

                _menuSceneSetupData.StartStandardLevel(
                    "Solo",
                    beatmapKey,
                    level,
                    _playerSettings.overrideEnvironmentSettings.overrideEnvironments ? _playerSettings.overrideEnvironmentSettings : null,
                    _playerSettings.colorSchemesSettings.overrideDefaultColors ? _playerSettings.colorSchemesSettings.GetSelectedColorScheme() : null,
                    level.GetColorScheme(beatmapKey.beatmapCharacteristic, beatmapKey.difficulty),
                    gameplayModifiers,
                    _playerSettings.playerSpecificSettings,
                    _playerSettings.practiceSettings,
                    //_soloFreePlayFlowCoordinator._environmentsListModel,
                    null,
                    "Menu",
                    false,
                    false,
                    delegate { }, //before scene switch
                    delegate { }, //after scene switch
                    (standardLevelScenesTransitionSetupData, results) => songFinishedCallback?.Invoke(standardLevelScenesTransitionSetupData, results),//level finished
                    (levelScenesTransitionSetupData, results) => songRestartedCallback?.Invoke(levelScenesTransitionSetupData, results)); //level restarted
                /* TODO: start paused? Worth looking into to replace the old hacky function */

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

            // Load IBeatmapLevel
            if (level is PreviewBeatmapLevelSO || level is CustomPreviewBeatmapLevel)
            {
                if (level is PreviewBeatmapLevelSO)
                {
                    if (!await HasDLCLevel(level.levelID))
                    {
                        // In the case of unowned DLC, just bail out and do nothing
                        return null;
                    }
                }

                var result = await GetLevelFromPreview(level);
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
