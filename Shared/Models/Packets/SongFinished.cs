using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class SongFinished
    {
        public enum CompletionType {
            Passed,
            Failed,
            Quit
        }

        public User User { get; set; }
        public Beatmap Map { get; set; }
        public CompletionType Type { get; set; }
        public int Score { get; set; }
    }
}
