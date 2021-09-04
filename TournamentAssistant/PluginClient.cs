using IPA.Utilities.Async;
using SiraUtil.Tools;
using SongCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistant.Managers;
using TournamentAssistant.Models;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.Models.GameplayModifiers;
using static TournamentAssistantShared.Models.Packets.Connect;
using static TournamentAssistantShared.Models.PlayerSpecificSettings;

namespace TournamentAssistant
{
    internal class PluginClient : SystemClient, IDisposable
    {
        public event Action<IBeatmapLevel>? LoadedSong;
        public event Action<PluginClient, Packet>? PacketReceived;
        public event Action<StartLevelOptions, MatchOptions>? StartLevel;

        private readonly Config _config;
        private readonly SiraLog _siraLog;
        private readonly ILevelService _levelService;
        private readonly PlayerDataModel _playerDataModel;
        private readonly IPlatformUserModel _platformUserModel;

        public Match? ActiveMatch { get; set; }
        public MatchOptions? ActiveMatchOptions { get; set; }

        public PluginClient(Config config, SiraLog siraLog, ILevelService levelService, PlayerDataModel playerDataModel, IPlatformUserModel platformUserModel)
        {
            _config = config;
            _siraLog = siraLog;
            _levelService = levelService;
            _playerDataModel = playerDataModel;
            _platformUserModel = platformUserModel;
        }

        public async Task<Dictionary<CoreServer, State>> GetCoreServers()
        {
            var userInfo = await _platformUserModel.GetUserInfo();
            var scraped = (await HostScraper.ScrapeHosts(_config.GetHosts(), userInfo.userName, ulong.Parse(userInfo.platformUserId))).Where(x => x.Value != null).ToDictionary(s => s.Key, s => s.Value);

            //Since we're scraping... Let's save the data we learned about the hosts while we're at it
            //var newHosts = _config.GetHosts().Union(scraped.Values.Where(x => x.KnownHosts != null).SelectMany(x => x.KnownHosts)).ToList();
            //_config.SaveHosts(newHosts.ToArray());

            return scraped;
        }

        public async Task Login(string address, int port)
        {
            var userInfo = await _platformUserModel.GetUserInfo();
            SetConnectionDetails(address, port, userInfo.userName, ConnectTypes.Player, userInfo.platformUserId);
            Start();
        }

        public void Logout()
        {
            if (Connected)
                Shutdown();
        }

        public void Dispose()
        {
            Logout();
        }

        protected override async void Client_PacketReceived(Packet packet)
        {
            base.Client_PacketReceived(packet);
            PacketReceived?.Invoke(this, packet);
            if (packet.Type == Packet.PacketType.PlaySong && packet.SpecificPacket is PlaySong playSong)
            {
                try
                {
                    var level = Loader.GetLevelById(playSong.GameplayParameters.Beatmap.LevelId);
                    if (level is null)
                    {
                        _siraLog.Error("The level could not be found! Cannot play.");
                        return;
                    }

                    var characteristic = level.previewDifficultyBeatmapSets.FirstOrDefault(x => x.beatmapCharacteristic.serializedName == playSong.GameplayParameters.Beatmap.Characteristic.SerializedName).beatmapCharacteristic ?? level.previewDifficultyBeatmapSets[0].beatmapCharacteristic;
                    var difficulty = (BeatmapDifficulty)playSong.GameplayParameters.Beatmap.Difficulty;

                    // Override defaults if we have forced options enabled
                    var playerSettings = _playerDataModel.playerData.playerSpecificSettings;
                    if (playSong.GameplayParameters.PlayerSettings.Options != PlayerOptions.None)
                    {
                        playerSettings = new PlayerSpecificSettings(
                            playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.LeftHanded),
                            playSong.GameplayParameters.PlayerSettings.PlayerHeight,
                            playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AutoPlayerHeight),
                            playSong.GameplayParameters.PlayerSettings.SfxVolume,
                            playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.ReduceDebris),
                            playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AdvancedHud),
                            playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.NoFailEffects),
                            playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.NoHud),
                            playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AutoRestart),
                            playSong.GameplayParameters.PlayerSettings.SaberTrailIntensity,
                            playSong.GameplayParameters.PlayerSettings.NoteJumpStartBeatOffset,
                            playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.HideNoteSpawnEffect),
                            playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AdaptiveSfx),
                            playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights) ? EnvironmentEffectsFilterPreset.NoEffects : EnvironmentEffectsFilterPreset.AllEffects,
                            playSong.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights) ? EnvironmentEffectsFilterPreset.NoEffects : EnvironmentEffectsFilterPreset.AllEffects
                        );
                    }

                    var songSpeed = GameplayModifiers.SongSpeed.Normal;
                    if (playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.SlowSong)) songSpeed = GameplayModifiers.SongSpeed.Slower;
                    if (playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.FastSong)) songSpeed = GameplayModifiers.SongSpeed.Faster;
                    if (playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.SuperFastSong)) songSpeed = GameplayModifiers.SongSpeed.SuperFast;

                    var gameplayModifiers = new GameplayModifiers(
                        playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.DemoNoFail),
                        playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.DemoNoObstacles),
                        playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.BatteryEnergy) ? GameplayModifiers.EnergyType.Battery : GameplayModifiers.EnergyType.Bar,
                        playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoFail),
                        playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.InstaFail),
                        playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.FailOnClash),
                        playSong.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoObstacles) ? GameplayModifiers.EnabledObstacleType.NoObstacles : GameplayModifiers.EnabledObstacleType.All,
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

                    var colorScheme = _playerDataModel.playerData.colorSchemesSettings.overrideDefaultColors ? _playerDataModel.playerData.colorSchemesSettings.GetSelectedColorScheme() : null;

                    var startLevelOptions = new StartLevelOptions(level, characteristic, difficulty, gameplayModifiers, playerSettings, _playerDataModel.playerData.overrideEnvironmentSettings, colorScheme);
                    var matchOptions = new MatchOptions(playSong.DisableScoresaberSubmission, playSong.FloatingScoreboard, playSong.StreamSync, playSong.DisableFail, playSong.DisablePause);
                    _ = UnityMainThreadTaskScheduler.Factory.StartNew(() => StartLevel?.Invoke(startLevelOptions, matchOptions));
                }
                catch (Exception e)
                {
                    _siraLog.Error("Error while trying to read PlaySong");
                    _siraLog.Error(e);
                }
            }
            else if (packet.Type == Packet.PacketType.LoadSong && packet.SpecificPacket is LoadSong loadSong && Self is Player player)
            {
                try
                {
                    if (!loadSong.LevelId.StartsWith("custom_level_"))
                    {
                        var level = _levelService.TryGetLevel(loadSong.LevelId, false);
                        if (level != null && level is IBeatmapLevel beatmap)
                        {
                            _ = UnityMainThreadTaskScheduler.Factory.StartNew(() => LoadedSong?.Invoke(beatmap));
                        }
                        else
                        {
                            _siraLog.Error($"Could not find OST '{loadSong.LevelId}'");
                        }
                        return;
                    }

                    var customLevel = _levelService.TryGetLevel(loadSong.LevelId);
                    if (customLevel == null)
                    {
                        UpdateDownloadState(player, Player.DownloadStates.Downloading);
                        customLevel = await _levelService.DownloadLevel(loadSong.LevelId, loadSong.LevelId, $"https://cdn.beatsaver.com/{loadSong.LevelId}.zip", CancellationToken.None);
                        if (customLevel == null)
                        {
                            UpdateDownloadState(player, Player.DownloadStates.DownloadError);
                        }
                    }

                    if (customLevel != null && customLevel is IBeatmapLevel customBeatmap)
                    {
                        UpdateDownloadState(player, Player.DownloadStates.Downloaded);
                        _ = UnityMainThreadTaskScheduler.Factory.StartNew(() => LoadedSong?.Invoke(customBeatmap));
                    }

                }
                catch (Exception e)
                {
                    _siraLog.Error("Could not load song.");
                    _siraLog.Error(e);
                }
            }
        }

        private void UpdateDownloadState(Player player, Player.DownloadStates dlState)
        {
            player.DownloadState = dlState;
            var update = new Event
            {
                Type = Event.EventType.PlayerUpdated,
                ChangedObject = Self
            };
            Send(new Packet(update));
        }
    }
}