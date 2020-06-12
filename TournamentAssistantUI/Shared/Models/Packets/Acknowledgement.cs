using System;

/**
 * Created by Moon on 6/4/2020
 * TODO: This file ack system should likely be expanded and used for all messages sent across the network
 * But for now, it's only for files, since I only need it for files at the moment
 */

namespace TournamentAssistantUI.Shared.Models.Packets
{
    [Serializable]
    class Acknowledgement
    {
        public string FileId { get; set; }
    }
}
