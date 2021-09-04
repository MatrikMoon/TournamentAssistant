using IPA.Utilities;
using IPA.Utilities.Async;
using System;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using Zenject;

namespace TournamentAssistant.Behaviours
{
    public class LevelStateManager : IInitializable, IDisposable
    {
        private PauseController _pauseController;
        private StandardLevelGameplayManager _standardLevelGameplayManager;

        public event Action? LevelFullyStarted;
        private readonly SyncHandler? _syncHandler;
        private readonly PluginClient _pluginClient;
        private readonly ScreenOverlay _screenOverlay;
        private readonly IReturnToMenuController _returnToMenuController;
        private static readonly FieldAccessor<PauseController, bool>.Accessor CanPause = FieldAccessor<PauseController, bool>.GetAccessor("_gameState");
        private static readonly FieldAccessor<StandardLevelGameplayManager, StandardLevelGameplayManager.GameState>.Accessor GameState = FieldAccessor<StandardLevelGameplayManager, StandardLevelGameplayManager.GameState>.GetAccessor("_gameState");

        internal LevelStateManager([InjectOptional] SyncHandler syncHandler, PluginClient pluginClient, ScreenOverlay screenOverlay, IReturnToMenuController returnToMenuController, PauseController pauseController, ILevelEndActions levelEndActions)
        {
            _syncHandler = syncHandler;
            _pluginClient = pluginClient;
            _screenOverlay = screenOverlay;
            _pauseController = pauseController;
            _returnToMenuController = returnToMenuController;
            _standardLevelGameplayManager = (levelEndActions as StandardLevelGameplayManager)!;
        }

        private void Client_PacketReceived(PluginClient sender, Packet packet)
        {
            if (packet.Type == Packet.PacketType.Command && packet.SpecificPacket is Command command)
            {
                if (command.CommandType == Command.CommandTypes.ReturnToMenu)
                {
                    _returnToMenuController.ReturnToMenu();
                }
                else if (command.CommandType == Command.CommandTypes.ScreenOverlay_ShowPng)
                {
                    _screenOverlay.ShowPng();
                }
                else if (command.CommandType == Command.CommandTypes.DelayTest_Finish)
                {
                    UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                    {
                        _screenOverlay.Clear();
                        _syncHandler?.Resume();
                    });
                }
            }
            else if (packet.Type == Packet.PacketType.File && packet.SpecificPacket is File file)
            {
                if (file.Intent == File.Intentions.SetPngToShowWhenTriggered || file.Intent == File.Intentions.ShowPngImmediately)
                {
                    var pngBytes = file.Compressed ? CompressionUtils.Decompress(file.Data) : file.Data;
                    _screenOverlay.SetPngBytes(pngBytes);

                    if (file.Intent == File.Intentions.ShowPngImmediately)
                        _screenOverlay.ShowPng();
                }

                sender.Send(packet.From, new Packet(new Acknowledgement()
                {
                    PacketId = packet.Id,
                    Type = Acknowledgement.AcknowledgementType.FileDownloaded
                }));
            }
        }

        public void Initialize()
        {
            _pluginClient.PacketReceived += Client_PacketReceived;
            _standardLevelGameplayManager.StartCoroutine(Coroutines.WaitForTask(WaitForStart()));
            if (_pluginClient.Self is Player player)
            {
                player.PlayState = Player.PlayStates.InGame;
                var playerUpdated = new Event
                {
                    Type = Event.EventType.PlayerUpdated,
                    ChangedObject = player
                };
                _pluginClient.Send(new Packet(playerUpdated));
            }
        }

        private async Task WaitForStart()
        {
            while (GameState(ref _standardLevelGameplayManager) != StandardLevelGameplayManager.GameState.Playing)
                await Task.Yield();

            while (!CanPause(ref _pauseController))
                await Task.Yield();

            LevelFullyStarted?.Invoke();
        }

        public void Dispose()
        {
            _screenOverlay.Clear();
            _pluginClient.PacketReceived -= Client_PacketReceived;

            if (_pluginClient.Connected && _pluginClient.Self is Player player)
            {
                player.PlayState = Player.PlayStates.Waiting;
                var playerUpdated = new Event
                {
                    Type = Event.EventType.PlayerUpdated,
                    ChangedObject = player
                };
                _pluginClient.Send(new Packet(playerUpdated));
            }
        }
    }
}