using System;
using System.IO;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class File
    {
        public enum Intentions
        {
            None,
            SetPngToShowWhenTriggered, //Image will be stored in the StreamSyncController and displayed when the DelayTest_Trigger command is received
            ShowPngImmediately //Image will be immediately displayed if the StreamSyncController is active
        }

        public File() { }

        public File(MemoryStream sourceStream, bool compression = true, Intentions intentions = Intentions.None) : this(sourceStream.ToArray(), compression, intentions) { }

        public File(byte[] source, bool compression = true, Intentions intentions = Intentions.None)
        {
            FileId = Guid.NewGuid().ToString();
            Compressed = compression;
            Intent = intentions;
            Data = compression ? CompressionUtils.Compress(source) : source;
        }

        public string FileId { get; set; }
        public Intentions Intent { get; set; }
        public bool Compressed { get; set; }
        public byte[] Data { get; set; }
    }
}
