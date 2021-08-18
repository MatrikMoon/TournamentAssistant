using IPA.Utilities;
using System;
using System.Linq;
using TournamentAssistant.Models;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using Zenject;

namespace TournamentAssistant.Behaviours
{
    internal class ScoreMonitor : ITickable
    {
        private readonly RoomData _roomData;
        private readonly PluginClient _pluginClient;
        private readonly ScoreController _scoreController;
        private readonly AudioTimeSyncController _audioTimeSyncController;

        private readonly Guid[] _destinationPlayers;

        private int _lastScore = 0;
        private int _scoreUpdateFrequency;
        private int _scoreCheckDelay = 0;

        public ScoreMonitor(RoomData roomData, PluginClient pluginClient, ScoreController scoreController, AudioTimeSyncController audioTimeSyncController)
        {
            _roomData = roomData;
            _pluginClient = pluginClient;
            _scoreController = scoreController;
            _audioTimeSyncController = audioTimeSyncController;

            if (roomData.match != null)
                _destinationPlayers = (_roomData.match != null && !_roomData.matchOptions.UseFloatingScoreboard) ? new Guid[] { _roomData.match.Leader.Id } : _roomData.match.Players.Select(x => x.Id).Union(new Guid[] { _roomData.match.Leader.Id }).ToArray();
            else
                _destinationPlayers = Array.Empty<Guid>();

            // new string[] { "x_x" }; // Note to future moon, this will cause the server to receive the forwarding packet and forward it to no one. Since it's received, though, the scoreboard will get it if connected

            _scoreUpdateFrequency = _pluginClient.State.ServerSettings.ScoreUpdateFrequency;

        }

        public void Tick()
        {
            if (_scoreCheckDelay > _scoreUpdateFrequency)
            {
                _scoreCheckDelay = 0;

                if (_scoreController != null && _scoreController.prevFrameModifiedScore != _lastScore)
                {
                    _lastScore = _scoreController.prevFrameModifiedScore;

                    ScoreUpdated(_scoreController.prevFrameModifiedScore, _scoreController.GetField<int, ScoreController>("_combo"), (float)_scoreController.prevFrameModifiedScore / _scoreController.immediateMaxPossibleRawScore, _audioTimeSyncController.songTime);
                }
            }
            _scoreCheckDelay++;
        }

        private void ScoreUpdated(int score, int combo, float accuracy, float time)
        {
            //Send score update
            (_pluginClient.Self as Player)!.Score = score;
            (_pluginClient.Self as Player)!.Combo = combo;
            (_pluginClient.Self as Player)!.Accuracy = accuracy;
            (_pluginClient.Self as Player)!.SongPosition = time;
            var playerUpdate = new Event
            {
                Type = Event.EventType.PlayerUpdated,
                ChangedObject = _pluginClient.Self
            };

            //NOTE:/TODO: We don't needa be blasting the entire server
            //with score updates. This update will only go out to other
            //players in the current match and the coordinator
            _pluginClient.Send(_destinationPlayers, new Packet(playerUpdate));
        }
    }
}
