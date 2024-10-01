using System;
using TournamentAssistantShared.Models;

namespace TournamentAssistantShared.Utilities
{
    public static class LeaderboardExtensions
    {
        public static int GetScoreValueByQualifierSettings(this LeaderboardEntry score, QualifierEvent.LeaderboardSort sort, int target = 0)
        {
            return sort switch
            {
                QualifierEvent.LeaderboardSort.NotesMissed or QualifierEvent.LeaderboardSort.NotesMissedAscending or QualifierEvent.LeaderboardSort.NotesMissedTarget => Math.Abs(target - score.NotesMissed),
                QualifierEvent.LeaderboardSort.BadCuts or QualifierEvent.LeaderboardSort.BadCutsAscending or QualifierEvent.LeaderboardSort.BadCutsTarget => Math.Abs(target - score.BadCuts),
                QualifierEvent.LeaderboardSort.GoodCuts or QualifierEvent.LeaderboardSort.GoodCutsAscending or QualifierEvent.LeaderboardSort.GoodCutsTarget => Math.Abs(target - score.GoodCuts),
                QualifierEvent.LeaderboardSort.MaxCombo or QualifierEvent.LeaderboardSort.MaxComboAscending or QualifierEvent.LeaderboardSort.MaxComboTarget => Math.Abs(target - score.MaxCombo),
                _ => Math.Abs(target - score.ModifiedScore),
            };
        }
    }
}
