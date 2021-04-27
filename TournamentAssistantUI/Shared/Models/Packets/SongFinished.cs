using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class SongFinished
    {
        public enum CompletionType
        {
            Passed,
            Failed,
            Quit
        }

        public Player User { get; set; }
        public Beatmap Beatmap { get; set; }
        public CompletionType Type { get; set; }
        public int Score { get; set; }
    }
}
