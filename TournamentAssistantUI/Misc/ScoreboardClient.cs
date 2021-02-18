﻿using System;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using static TournamentAssistantShared.Models.Packets.Connect.Types;

namespace TournamentAssistantUI.Misc
{
    internal class ScoreboardClient : SystemClient
    {
        public event Action PlaySongSent;

        public ScoreboardClient(string endpoint, int port) : base(endpoint, port, "[Scoreboard]", ConnectTypes.Coordinator)
        {
        }

        protected override void Client_PacketReceived(Packet packet)
        {
            base.Client_PacketReceived(packet);

            if (packet.Type == PacketType.PlaySong)
            {
                PlaySongSent?.Invoke();
            }
        }
    }
}