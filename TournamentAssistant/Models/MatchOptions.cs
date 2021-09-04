namespace TournamentAssistant.Models
{
    public class MatchOptions
    {
        public bool DisableScoreSubmission { get; }
        public bool UseFloatingScoreboard { get; }
        public bool UseStreamSync { get; }
        public bool DisableFailing { get; }
        public bool DisablePausing { get; }

        public MatchOptions(bool disableScoreSubmission, bool useFloatingScoreboard, bool useStreamSync, bool disableFailing, bool disablePausing)
        {
            DisableScoreSubmission = disableScoreSubmission;
            UseFloatingScoreboard = useFloatingScoreboard;
            UseStreamSync = useStreamSync;
            DisableFailing = disableFailing;
            DisablePausing = disablePausing;
        }
    }
}