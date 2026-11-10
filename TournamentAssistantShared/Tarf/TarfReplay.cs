using System.Collections.Generic;
using System.IO;
using System.Linq;
using TournamentAssistantShared.Models.Tarf;

namespace TournamentAssistantShared.Tarf
{
    // A whole .tarf recording with every MotionBlock unpacked into frames and every EventBatch flattened into one time ordered list
    public class TarfReplay
    {
        public Header Header { get; set; } = new Header();
        public List<TarfFrame> Frames { get; set; } = new List<TarfFrame>();
        public List<Event> Events { get; set; } = new List<Event>();

        // Null when the recording was cut off before the level ended
        public Footer Footer { get; set; }
    }

    public static class TarfWriter
    {
        // Fills in the footer's frame and event counts. Each event goes in right before the first frame after it, so the file stays roughly in time order
        public static byte[] Write(TarfReplay replay)
        {
            var recorder = new TarfRecorder(replay.Header.MotionEncoding);
            var events = replay.Events.OrderBy(x => x.TimeUs).ToList();
            var nextEvent = 0;

            foreach (var frame in replay.Frames)
            {
                while (nextEvent < events.Count && events[nextEvent].TimeUs < frame.TimeUs)
                {
                    recorder.AddEvent(events[nextEvent++]);
                }
                recorder.AddFrame(frame);
            }
            while (nextEvent < events.Count)
            {
                recorder.AddEvent(events[nextEvent++]);
            }

            var footer = replay.Footer ?? new Footer { Completion = Completion.CompletionIncomplete };
            return recorder.Finish(replay.Header, footer);
        }

        // Swaps the header of an existing file, for example to add or remove the map. Every other record is copied byte for byte,
        // so the recording itself is never decoded or quantized again
        public static byte[] ReplaceHeader(byte[] tarf, Header header)
        {
            TarfReader.ReadHeader(tarf, out var headerEnd);

            using var output = new MemoryStream(tarf.Length);
            output.Write(tarf, 0, TarfContainer.FileHeaderLength);
            var hasMapFiles = header.Map?.Files.Count > 0;
            TarfContainer.WriteRecord(output, TarfRecordType.Header, header, tryDeflate: !hasMapFiles);
            output.Write(tarf, headerEnd, tarf.Length - headerEnd);
            return output.ToArray();
        }
    }
}
