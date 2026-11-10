using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TournamentAssistantShared.Models.Tarf;

namespace TournamentAssistantShared.Tarf
{
    // Builds a .tarf file while a song is being played
    //
    // Frames and events are packed into records as they come in, so memory only grows with the encoded size of the recording (well under a megabyte for a normal song)
    // The header is written last, so it can hold things only known at the end, like the map zip
    public class TarfRecorder
    {
        public const uint DefaultPositionStepNm = 10_000;
        public const uint DefaultRotationStepNano = 1_000;
        public const uint DefaultAnchorInterval = 60;

        public int FrameCount { get; private set; }
        public int EventCount { get; private set; }

        private readonly MotionEncoding _encoding;
        private readonly MemoryStream _body = new MemoryStream(256 * 1024);
        private readonly List<TarfFrame> _blockFrames;
        private readonly List<Event> _pendingEvents = new List<Event>();

        public TarfRecorder(MotionEncoding encoding = null)
        {
            _encoding = encoding ?? new MotionEncoding
            {
                PositionStepNm = DefaultPositionStepNm,
                RotationStepNano = DefaultRotationStepNano,
                AnchorInterval = DefaultAnchorInterval
            };
            if (_encoding.PositionStepNm == 0 || _encoding.RotationStepNano == 0 || _encoding.AnchorInterval == 0)
            {
                throw new ArgumentException("Motion encoding steps and anchor interval must be greater than zero");
            }
            _blockFrames = new List<TarfFrame>((int)_encoding.AnchorInterval);
        }

        public void AddFrame(TarfFrame frame)
        {
            _blockFrames.Add(frame);
            FrameCount++;
            if (_blockFrames.Count == _encoding.AnchorInterval) WriteBlock();
        }

        // Events don't need to arrive in time order, a note is only known once its swing has finished
        public void AddEvent(Event tarfEvent)
        {
            _pendingEvents.Add(tarfEvent);
            EventCount++;
        }

        // Fills in the header's motion encoding and the footer's counts
        public byte[] Finish(Header header, Footer footer)
        {
            WriteBlock();

            header.MotionEncoding = _encoding;
            footer.FrameCount = (uint)FrameCount;
            footer.EventCount = (uint)EventCount;

            using var output = new MemoryStream((int)_body.Length + 64 * 1024);
            TarfContainer.WriteFileHeader(output);

            // Embedded map files are already compressed, deflating them again only costs time
            var hasMapFiles = header.Map?.Files.Count > 0;
            TarfContainer.WriteRecord(output, TarfRecordType.Header, header, tryDeflate: !hasMapFiles);

            _body.Position = 0;
            _body.CopyTo(output);

            TarfContainer.WriteRecord(output, TarfRecordType.Footer, footer);
            return output.ToArray();
        }

        // Writes the frames collected so far as one motion block, followed by the events that came in meanwhile
        private void WriteBlock()
        {
            if (_blockFrames.Count > 0)
            {
                var firstFrameIndex = (uint)(FrameCount - _blockFrames.Count);
                TarfContainer.WriteRecord(_body, TarfRecordType.MotionBlock, MotionCodec.Pack(_blockFrames, firstFrameIndex, _encoding));
                _blockFrames.Clear();
            }

            if (_pendingEvents.Count > 0)
            {
                var batch = new EventBatch();
                batch.Events.AddRange(_pendingEvents.OrderBy(x => x.TimeUs));
                TarfContainer.WriteRecord(_body, TarfRecordType.EventBatch, batch);
                _pendingEvents.Clear();
            }
        }
    }
}
