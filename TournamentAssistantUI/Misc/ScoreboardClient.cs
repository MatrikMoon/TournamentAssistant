using System;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantUI.Misc
{
    class ScoreboardClient : SystemClient
    {
        public event Action PlaySongSent;

        public ScoreboardClient(string endpoint, int port) : base(endpoint, port, "[Scoreboard]", Connect.Types.ConnectTypes.Coordinator) { }

        protected override async Task Client_PacketReceived(Packet packet)
        {
            await base.Client_PacketReceived(packet);

            if (packet.PacketCase == Packet.PacketOneofCase.PlaySong)
            {
                PlaySongSent?.Invoke();
            }
        }
    }
}
