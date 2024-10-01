using System;
using System.Linq;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistantServer.Utilities
{
    public static class LeaderboardExtensions
    {
        public static int GetScoreValueByQualifierSettings(this Database.Models.Score score, QualifierEvent.LeaderboardSort sort, int target = 0)
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

        // Returns true if newer score is better than old one
        public static bool IsNewScoreBetter(this Database.Models.Score oldScore, LeaderboardEntry newScore, QualifierEvent.LeaderboardSort sort, int target = 0)
        {
            return sort switch
            {
                QualifierEvent.LeaderboardSort.ModifiedScoreAscending
                    or QualifierEvent.LeaderboardSort.ModifiedScoreTarget
                    or QualifierEvent.LeaderboardSort.NotesMissedAscending
                    or QualifierEvent.LeaderboardSort.NotesMissedTarget
                    or QualifierEvent.LeaderboardSort.BadCutsAscending
                    or QualifierEvent.LeaderboardSort.BadCutsTarget
                    or QualifierEvent.LeaderboardSort.GoodCutsAscending
                    or QualifierEvent.LeaderboardSort.GoodCutsTarget
                    or QualifierEvent.LeaderboardSort.MaxComboAscending
                    or QualifierEvent.LeaderboardSort.MaxComboTarget
                    => oldScore.GetScoreValueByQualifierSettings(sort, target) > newScore.GetScoreValueByQualifierSettings(sort, target),
                _ => oldScore.GetScoreValueByQualifierSettings(sort, target) < newScore.GetScoreValueByQualifierSettings(sort, target),
            };
        }

        public static IOrderedEnumerable<Database.Models.Score> OrderByQualifierSettings(this IQueryable<Database.Models.Score> scores, QualifierEvent.LeaderboardSort sort, int target = 0, bool invert = false)
        {
            return sort switch
            {
                QualifierEvent.LeaderboardSort.ModifiedScoreAscending
                    or QualifierEvent.LeaderboardSort.ModifiedScoreTarget
                    or QualifierEvent.LeaderboardSort.NotesMissedAscending
                    or QualifierEvent.LeaderboardSort.NotesMissedTarget
                    or QualifierEvent.LeaderboardSort.BadCutsAscending
                    or QualifierEvent.LeaderboardSort.BadCutsTarget
                    or QualifierEvent.LeaderboardSort.GoodCutsAscending
                    or QualifierEvent.LeaderboardSort.GoodCutsTarget
                    or QualifierEvent.LeaderboardSort.MaxComboAscending
                    or QualifierEvent.LeaderboardSort.MaxComboTarget
                    => invert ? scores.AsEnumerable().OrderByDescending(x => x.GetScoreValueByQualifierSettings(sort, target)) : scores.AsEnumerable().OrderBy(x => x.GetScoreValueByQualifierSettings(sort, target)),
                _ => invert ? scores.AsEnumerable().OrderBy(x => x.GetScoreValueByQualifierSettings(sort, target)) : scores.AsEnumerable().OrderByDescending(x => x.GetScoreValueByQualifierSettings(sort, target)),
            };
        }
    }
}
