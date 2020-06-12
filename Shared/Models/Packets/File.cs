using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class File
    {
        public enum Intentions
        {
            None,
            SetPngToShowWhenTriggered,  //Image will be stored in the StreamSyncController and displayed when the DelayTest_Trigger command is recieved
            ShowPngImmediately //Image will be immediately displayed if the StreamSyncController is active
        }

        public string FileId { get; set; }
        public Intentions Intention { get; set; }
        public bool Compressed { get; set; }
        public byte[] Data { get; set; }
    }
}
