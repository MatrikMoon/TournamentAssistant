using System;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistantUI.Misc
{
    class ScoreboardClient : SystemClient
    {
        public event Action PlaySongSent;

        public ScoreboardClient(string endpoint, int port) : base(endpoint, port, "[Scoreboard]", Connect.ConnectTypes.Coordinator) { }

        protected override async Task Client_PacketReceived(Packet packet)
        {
            await base.Client_PacketReceived(packet);

            if (packet.Type == PacketType.PlaySong)
            {
                PlaySongSent?.Invoke();
            }
        }
    }
}
