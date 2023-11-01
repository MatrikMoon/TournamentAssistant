using TournamentAssistantShared.Models;

namespace TournamentAssistantShared.Utilities
{
    public static class LeaderboardExtensions
    {
        public static int GetScoreValueByQualifierSettings(this LeaderboardEntry score, QualifierEvent.LeaderboardSort sort)
        {
            return sort switch
            {
                QualifierEvent.LeaderboardSort.NotesMissed or QualifierEvent.LeaderboardSort.NotesMissedAscending => score.NotesMissed,
                QualifierEvent.LeaderboardSort.BadCuts or QualifierEvent.LeaderboardSort.BadCutsAscending => score.BadCuts,
                QualifierEvent.LeaderboardSort.GoodCuts or QualifierEvent.LeaderboardSort.GoodCutsAscending => score.GoodCuts,
                QualifierEvent.LeaderboardSort.MaxCombo or QualifierEvent.LeaderboardSort.MaxComboAscending => score.MaxCombo,
                _ => score.ModifiedScore,
            };
        }
    }
}
