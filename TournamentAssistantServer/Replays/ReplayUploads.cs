using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.Replays
{
    // Puts replays back together from the chunks players send them in
    public class ReplayUploads
    {
        // A replay with its map baked in is a few megabytes, this leaves room for long songs
        public const long MaxReplaySize = 64L * 1024 * 1024;
        public const int MaxChunkSize = 1024 * 1024;
        private const int MaxUploadsPerUser = 2;
        private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

        private class Upload
        {
            public string TournamentId;
            public int ChunkCount;
            public long TotalSize;
            public int NextChunk;
            public MemoryStream Data;
            public DateTime LastChunkAt;
        }

        private readonly ConcurrentDictionary<(string UserId, string UploadId), Upload> _uploads = new ConcurrentDictionary<(string, string), Upload>();

        // Returns the whole replay once its last chunk arrives, and null before that. Throws InvalidDataException for chunks that don't fit the upload
        public byte[] AddChunk(string userId, Request.SubmitReplay chunk)
        {
            if (string.IsNullOrWhiteSpace(chunk.UploadId) || chunk.UploadId.Length > 64) throw new InvalidDataException("Invalid upload id");
            if (chunk.TotalSize <= 0 || chunk.TotalSize > MaxReplaySize) throw new InvalidDataException("Replay is too large");
            if (chunk.ChunkCount <= 0 || chunk.ChunkCount > MaxReplaySize / 1024) throw new InvalidDataException("Invalid chunk count");
            if (chunk.Data == null || chunk.Data.Length == 0 || chunk.Data.Length > MaxChunkSize) throw new InvalidDataException("Invalid chunk size");

            var key = (userId, chunk.UploadId);
            if (chunk.ChunkIndex == 0)
            {
                DiscardStale();
                if (_uploads.Keys.Count(x => x.UserId == userId) >= MaxUploadsPerUser) throw new InvalidDataException("Too many replays are being uploaded at once");

                _uploads[key] = new Upload
                {
                    TournamentId = chunk.TournamentId,
                    ChunkCount = chunk.ChunkCount,
                    TotalSize = chunk.TotalSize,
                    Data = new MemoryStream((int)chunk.TotalSize),
                };
            }

            if (!_uploads.TryGetValue(key, out var upload)) throw new InvalidDataException("Unknown upload, start again from the first chunk");

            // Chunks are sent one at a time, so anything out of order means the upload went wrong
            if (chunk.ChunkIndex != upload.NextChunk || chunk.ChunkCount != upload.ChunkCount || chunk.TotalSize != upload.TotalSize
                || chunk.TournamentId != upload.TournamentId || upload.Data.Length + chunk.Data.Length > upload.TotalSize)
            {
                _uploads.TryRemove(key, out _);
                throw new InvalidDataException("Replay chunk doesn't match its upload");
            }

            upload.Data.Write(chunk.Data, 0, chunk.Data.Length);
            upload.NextChunk++;
            upload.LastChunkAt = DateTime.UtcNow;

            if (upload.NextChunk < upload.ChunkCount) return null;

            _uploads.TryRemove(key, out _);
            if (upload.Data.Length != upload.TotalSize) throw new InvalidDataException("Replay is smaller than announced");
            return upload.Data.ToArray();
        }

        public void DiscardStale()
        {
            foreach (var entry in _uploads.Where(x => DateTime.UtcNow - x.Value.LastChunkAt > StaleAfter).ToList())
            {
                _uploads.TryRemove(entry.Key, out _);
            }
        }
    }
}
