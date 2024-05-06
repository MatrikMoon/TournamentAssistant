using System.Threading.Tasks;
using TournamentAssistantServer.Database.Models;
using TournamentAssistantServer.Discord;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    [Module(Packet.packetOneofCase.Command, "packet.Command.TypeCase")]
    class Commands
    {
        public ExecutionContext ExecutionContext { get; set; }
        public QualifierBot QualifierBot { get; set; }

        [AllowUnauthorized]
        [PacketHandler((int)Command.TypeOneofCase.Heartbeat)]
        public void Heartbeat()
        {
            // Do nothing!
        }

        // Seems to be unused, and we probably shouldn't allow random clients to do this
        [PacketHandler((int)Command.TypeOneofCase.send_bot_message)]
        public void SendBotMessage(Packet packet)
        {
            var sendBotMessage = packet.Command.send_bot_message;
            QualifierBot.SendMessage(sendBotMessage.Channel, sendBotMessage.Message);
        }
    }
}
