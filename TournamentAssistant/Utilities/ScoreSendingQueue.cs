using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.Utilities
{
    class ScoreSendingQueue
    {
        private readonly ConcurrentQueue<(string[] audience, RealtimeScore score)> _scoreQueue = new ConcurrentQueue<(string[], RealtimeScore)>();

        private bool _sendingScores;
        private readonly PluginClient _client;

        public ScoreSendingQueue(PluginClient client)
        {
            _client = client;
        }

        public void Enqueue(string[] audience, RealtimeScore score)
        {
            _scoreQueue.Enqueue((audience, score));

            // Only spin up the processor if it isn't already running
            if (!_sendingScores)
            {
                _ = ProcessQueue();
            }
        }

        private async Task ProcessQueue()
        {
            _sendingScores = true;
            try
            {
                while (_scoreQueue.TryDequeue(out var item))
                {
                    try
                    {
                        // Send one score at a time, in order
                        await _client.SendRealtimeScore(item.audience, item.score);
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Failed to send score: {e}");
                    }
                }
            }
            finally
            {
                _sendingScores = false;
            }
        }
    }
}