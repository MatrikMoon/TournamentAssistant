using System;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantUI.Misc
{
    class ScoreboardClient : SystemClient
    {
        public event Action PlaySongSent;

        public ScoreboardClient(string endpoint, int port) : base(endpoint, port, "[Scoreboard]", TournamentAssistantShared.Models.User.ClientTypes.WebsocketConnection) { }

        protected override async Task Client_PacketReceived(Packet packet)
        {
            await base.Client_PacketReceived(packet);

            if (packet.packetCase == Packet.packetOneofCase.Command && packet.Command.TypeCase == Command.TypeOneofCase.play_song)
            {
                PlaySongSent?.Invoke();
            }
        }
    }
}
