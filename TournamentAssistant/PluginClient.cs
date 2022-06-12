using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.Behaviors;
using TournamentAssistant.Interop;
using TournamentAssistant.Misc;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using static TournamentAssistantShared.Models.GameplayModifiers;
using static TournamentAssistantShared.Models.PlayerSpecificSettings;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant
{
    public class PluginClient : SystemClient
    {
        public event Func<IBeatmapLevel, Task> LoadedSong;

        public event Func<IPreviewBeatmapLevel, BeatmapCharacteristicSO, BeatmapDifficulty, GameplayModifiers,
            PlayerSpecificSettings, OverrideEnvironmentSettings, ColorScheme, bool, bool, bool, bool, Task> PlaySong;

        public PluginClient(string endpoint, int port, string username, string userId,
            User.ClientTypes clientType = User.ClientTypes.Player) : base(endpoint, port, username,
            clientType, userId)
        {
        }

        protected override async Task Client_PacketReceived(Packet packet)
        {
            await base.Client_PacketReceived(packet);

            if (packet.packetCase == Packet.packetOneofCase.PlaySong)
            {
                var playSong = packet.PlaySong;

                var desiredLevel =
                    SongUtils.masterLevelList.First(x => x.levelID == playSong.GameplayParameters.Beatmap.LevelId);
                var desiredCharacteristic =
                    desiredLevel.previewDifficultyBeatmapSets.FirstOrDefault(x =>
                        x.beatmapCharacteristic.serializedName ==
                        playSong.GameplayParameters.Beatmap.Characteristic.SerializedName).beatmapCharacteristic ??
                    desiredLevel.previewDifficultyBeatmapSets.First().beatmapCharacteristic;
                var desiredDifficulty = (BeatmapDifficulty)playSong.GameplayParameters.Beatmap.Difficulty;

                var playerData = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First().playerData;
                var playerSettings = playerData.playerSpecificSettings;

                //Override defaults if we have forced options enabled
                if (playSong.GameplayParameters.PlayerSettings.Options != PlayerOptions.None)
                {
                    playerSettings = new PlayerSpecificSettings(
                        playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.LeftHanded),
                        playSong.GameplayParameters.PlayerSettings.PlayerHeight,
                        playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AutoPlayerHeight),
                        playSong.GameplayParameters.PlayerSettings.SfxVolume,
                        playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.ReduceDebris),
                        playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.NoFailEffects),
                        playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AdvancedHud),
                        playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.NoHud),
                        playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AutoRestart),
                        playSong.GameplayParameters.PlayerSettings.SaberTrailIntensity,
                        (NoteJumpDurationTypeSettings)playSong.GameplayParameters.PlayerSettings.note_jump_duration_type_settings,
                        playSong.GameplayParameters.PlayerSettings.NoteJumpFixedDuration,
                        playSong.GameplayParameters.PlayerSettings.NoteJumpStartBeatOffset,
                        playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.HideNoteSpawnEffect),
                        playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AdaptiveSfx),
                        playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights)
                            ? EnvironmentEffectsFilterPreset.NoEffects
                            : EnvironmentEffectsFilterPreset.AllEffects,
                        playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights)
                            ? EnvironmentEffectsFilterPreset.NoEffects
                            : EnvironmentEffectsFilterPreset.AllEffects
                    );
                }

                var songSpeed = GameplayModifiers.SongSpeed.Normal;
                if (playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.SlowSong))
                    songSpeed = GameplayModifiers.SongSpeed.Slower;
                if (playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.FastSong))
                    songSpeed = GameplayModifiers.SongSpeed.Faster;
                if (playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.SuperFastSong))
                    songSpeed = GameplayModifiers.SongSpeed.SuperFast;

                var gameplayModifiers = new GameplayModifiers(
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.BatteryEnergy)
                        ? GameplayModifiers.EnergyType.Battery
                        : GameplayModifiers.EnergyType.Bar,
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoFail),
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.InstaFail),
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.FailOnClash),
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoObstacles)
                        ? GameplayModifiers.EnabledObstacleType.NoObstacles
                        : GameplayModifiers.EnabledObstacleType.All,
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoBombs),
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.FastNotes),
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.StrictAngles),
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.DisappearingArrows),
                    songSpeed,
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoArrows),
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.GhostNotes),
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.ProMode),
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.ZenMode),
                    playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.SmallCubes)
                );

                var colorScheme = playerData.colorSchemesSettings.overrideDefaultColors
                    ? playerData.colorSchemesSettings.GetSelectedColorScheme()
                    : null;

                //Disable score submission if nofail is on. This is specifically for Hidden Sabers, though it may stay longer
                if (playSong.DisableScoresaberSubmission)
                    BS_Utils.Gameplay.ScoreSubmission.DisableSubmission(Constants.NAME);
                if (playSong.ShowNormalNotesOnStream)
                {
                    var customNotes = IPA.Loader.PluginManager.GetPluginFromId("CustomNotes");
                    if (customNotes != null)
                    {
                        EnableHMDOnly();
                    }
                }

                PlaySong?.Invoke(desiredLevel, desiredCharacteristic, desiredDifficulty, gameplayModifiers,
                    playerSettings, playerData.overrideEnvironmentSettings, colorScheme, playSong.FloatingScoreboard,
                    playSong.StreamSync, playSong.DisableFail, playSong.DisablePause);
            }
            else if (packet.packetCase == Packet.packetOneofCase.Command)
            {
                var command = packet.Command;
                if (command.CommandType == Command.CommandTypes.ReturnToMenu)
                {
                    if (SyncHandler.Instance != null) ScreenOverlay.Instance.Clear();
                    if (!Plugin.IsInMenu()) PlayerUtils.ReturnToMenu();
                }
                else if (command.CommandType == Command.CommandTypes.ScreenOverlayShowPng)
                {
                    ScreenOverlay.Instance.ShowPng();
                }
                else if (command.CommandType == Command.CommandTypes.DelayTestFinish)
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        ScreenOverlay.Instance.Clear();
                        SyncHandler.Instance.Resume();
                        SyncHandler.Destroy();
                    });
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.LoadSong)
            {
                var loadSong = packet.LoadSong;

                Action<IBeatmapLevel> songLoaded = (loadedLevel) =>
                {
                    //Send updated download status
                    var user = State.Users.FirstOrDefault(x => x.Guid == Self.Guid);
                    user.DownloadState = User.DownloadStates.Downloaded;

                    var playerUpdate = new Event
                    {
                        user_updated_event = new Event.UserUpdatedEvent
                        {
                            User = user
                        }
                    };
                    Send(new Packet
                    {
                        Event = playerUpdate
                    });

                    //Notify any listeners of the client that a song has been loaded
                    LoadedSong?.Invoke(loadedLevel);
                };

                if (SongUtils.masterLevelList.Any(x => x.levelID == loadSong.LevelId))
                {
                    SongUtils.LoadSong(loadSong.LevelId, songLoaded);
                }
                else
                {
                    Action<string, bool> loadSongAction = (hash, succeeded) =>
                    {
                        if (succeeded)
                        {
                            SongUtils.LoadSong(loadSong.LevelId, songLoaded);
                        }
                        else
                        {
                            var user = State.Users.FirstOrDefault(x => x.Guid == Self.Guid);
                            user.DownloadState = User.DownloadStates.DownloadError;

                            var playerUpdated = new Event
                            {
                                user_updated_event = new Event.UserUpdatedEvent
                                {
                                    User = user
                                }
                            };

                            Send(new Packet
                            {
                                Event = playerUpdated
                            });
                        }
                    };

                    var user = State.Users.FirstOrDefault(x => x.Guid == Self.Guid);
                    user.DownloadState = User.DownloadStates.Downloading;

                    var playerUpdate = new Event
                    {
                        user_updated_event = new Event.UserUpdatedEvent
                        {
                            User = user
                        }
                    };
                    await Send(new Packet
                    {
                        Event = playerUpdate
                    });

                    SongDownloader.DownloadSong(loadSong.LevelId, songDownloaded: loadSongAction,
                        downloadProgressChanged: (hash, progress) =>
                            Logger.Debug($"DOWNLOAD PROGRESS ({hash}): {progress}"),
                        customHostUrl: loadSong.CustomHostUrl);
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.File)
            {
                var file = packet.File;
                if (file.Intent == File.Intentions.SetPngToShowWhenTriggered)
                {
                    var pngBytes = file.Compressed
                        ? CompressionUtils.Decompress(file.Data.ToArray())
                        : file.Data.ToArray();
                    ScreenOverlay.Instance.SetPngBytes(pngBytes);
                }
                else if (file.Intent == File.Intentions.ShowPngImmediately)
                {
                    var pngBytes = file.Compressed
                        ? CompressionUtils.Decompress(file.Data.ToArray())
                        : file.Data.ToArray();
                    ScreenOverlay.Instance.SetPngBytes(pngBytes);
                    ScreenOverlay.Instance.ShowPng();
                }

                await Send(Guid.Parse(packet.From), new Packet
                {
                    Acknowledgement = new Acknowledgement()
                    {
                        PacketId = packet.Id,
                        Type = Acknowledgement.AcknowledgementType.FileDownloaded
                    }
                });
            }
        }

        //Broken off so that if custom notes isn't installed, we don't try to load anything from it
        private static void EnableHMDOnly()
        {
            CustomNotesInterop.EnableHMDOnly();
        }
    }
}