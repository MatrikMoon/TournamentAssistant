using System;
using TournamentAssistantShared.Models.Packets;

/**
 * Created by Moon on 9/12/2020, 1:41AM.
 * This is a very simple extension of a standard SystemClient,
 * with the addition of an event which provides packets as they
 * are received. This is useful for temporary clients (HostScraper)
 * so they can perform one simple action, wait for a reaction, then
 * disconnect. Should not be used for other purposes, use a SystemClient
 * for more robust clients, use a Client directly for more simple ones.
 */

namespace TournamentAssistantShared
{
    public class TemporaryClient : SystemClient
    {
        public event Action<Packet> PacketReceived;
        public TemporaryClient(string endpoint, int port, string username, string userId, Connect.ConnectTypes connectType = Connect.ConnectTypes.Player) : base(endpoint, port, username, connectType, userId) { }

        protected override void Client_PacketReceived(Packet packet)
        {
            base.Client_PacketReceived(packet);

            PacketReceived?.Invoke(packet);
        }
    }
}