using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using static TournamentAssistantShared.Models.GameplayModifiers.Types;
using static TournamentAssistantShared.Models.PlayerSpecificSettings.Types;

namespace TournamentAssistant.Behaviors
{
    internal class BackupScoreSubmissions : MonoBehaviour
    {
        private ResultsViewController rvc;
        private const string backupDir = "failsafe_score_submissions";

        public void Awake()
        {
            BS_Utils.Utilities.BSEvents.levelCleared += BSEvents_levelCleared;
        }

        public void Update()
        {
        }

        private void BSEvents_levelCleared(StandardLevelScenesTransitionSetupDataSO arg1, LevelCompletionResults results)
        {
            PlayerUtils.GetPlatformUserData((username, userId) => CreateAndBackupScore(username, userId, arg1, results));
        }

        private static GameOptions GetOptions(GameplayModifiers modifiers)
        {
            int val = 0;
            val |= modifiers.noFailOn0Energy ? 1 : 0;
            val |= modifiers.noBombs ? 2 : 0;
            val |= modifiers.noArrows ? 4 : 0;
            val |= modifiers.enabledObstacleType == GameplayModifiers.EnabledObstacleType.NoObstacles ? 8 : 0;
            val |= modifiers.songSpeed == GameplayModifiers.SongSpeed.Slower ? 16 : 0;
            val |= modifiers.instaFail ? 32 : 0;
            val |= modifiers.failOnSaberClash ? 64 : 0;
            val |= modifiers.energyType == GameplayModifiers.EnergyType.Battery ? 128 : 0;
            val |= modifiers.fastNotes ? 256 : 0;
            val |= modifiers.songSpeed == GameplayModifiers.SongSpeed.Faster ? 512 : 0;
            val |= modifiers.disappearingArrows ? 1024 : 0;
            val |= modifiers.ghostNotes ? 2048 : 0;
            val |= modifiers.demoNoFail ? 4096 : 0;
            val |= modifiers.demoNoObstacles ? 8192 : 0;
            val |= modifiers.strictAngles ? 16384 : 0;
            return (GameOptions)val;
        }

        private static PlayerOptions GetOptions(PlayerSpecificSettings settings)
        {
            int val = 0;
            val |= settings.leftHanded ? 1 : 0;
            val |= settings.staticLights ? 2 : 0;
            val |= settings.noTextsAndHuds ? 4 : 0;
            val |= settings.advancedHud ? 8 : 0;
            val |= settings.reduceDebris ? 16 : 0;
            val |= settings.automaticPlayerHeight ? 32 : 0;
            val |= settings.noFailEffects ? 64 : 0;
            val |= settings.autoRestart ? 128 : 0;
            val |= settings.hideNoteSpawnEffect ? 256 : 0;
            val |= settings.adaptiveSfx ? 512 : 0;
            return (PlayerOptions)val;
        }

        private void CreateAndBackupScore(string username, ulong userId, StandardLevelScenesTransitionSetupDataSO arg1, LevelCompletionResults results)
        {
            var characteristic = new Characteristic
            {
                SerializedName = arg1.difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName
            };
            foreach (var bm in arg1.difficultyBeatmap.parentDifficultyBeatmapSet.difficultyBeatmaps)
            {
                characteristic.Difficulties.Add((TournamentAssistantShared.Models.BeatmapDifficulty)bm.difficultyRank);
            }
            var @params = new GameplayParameters
            {
                Beatmap = new Beatmap
                {
                    Characteristic = characteristic,
                    Difficulty = (TournamentAssistantShared.Models.BeatmapDifficulty)arg1.difficultyBeatmap.difficultyRank,
                    LevelId = arg1.difficultyBeatmap.level.levelID,
                    Name = arg1.difficultyBeatmap.level.songName
                },
                GameplayModifiers = new TournamentAssistantShared.Models.GameplayModifiers
                {
                    Options = GetOptions(results.gameplayModifiers)
                },
                PlayerSettings = new TournamentAssistantShared.Models.PlayerSpecificSettings
                {
                    SaberTrailIntensity = 1,
                    NoteJumpStartBeatOffset = 0,
                    PlayerHeight = 1,
                    SfxVolume = 1,
                    Options = PlayerOptions.AdvancedHud | PlayerOptions.StaticLights
                }
            };
            var pkt = new Packet(new SubmitScore()
            {
                Score = new Score
                {
                    Color = "#ffffff",
                    FullCombo = results.fullCombo,
                    // TODO, this is set to CVRE!
                    EventId = "b69ce5f6-f5f2-4865-aa8d-f50a9dfa9b6f",
                    Score_ = results.modifiedScore,
                    UserId = userId.ToString(CultureInfo.InvariantCulture),
                    Username = username,
                    Parameters = @params
                }
            });
            var filePath = Path.Combine(backupDir, @params.Beatmap.Name + "_" + results.modifiedScore.ToString(CultureInfo.InvariantCulture) + "_" + username + "_" + DateTime.UtcNow.ToUnixTime().ToString(CultureInfo.InvariantCulture) + ".dat");
            TournamentAssistantShared.Logger.Debug("Wrote backup score submission packet to file: " + filePath);
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);
            System.IO.File.WriteAllBytes(filePath, pkt.ToBytes());
        }
    }
}