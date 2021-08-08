namespace TournamentAssistant.Models
{
    public class MatchOptions
    {
        public bool UseFloatingScoreboard { get; }
        public bool UseStreamSync { get; }
        public bool DisableFailing { get; }
        public bool DisablePausing { get; }
    }
}