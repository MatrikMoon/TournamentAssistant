using System.Linq;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistantServer.Helpers
{
    public static class LeaderboardExtensions
    {
        public static int GetScoreValueByQualifierSettings(this Database.Models.Score score, QualifierEvent.LeaderboardSort sort)
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

        // Returns true if newer score is better than old one
        public static bool IsNewScoreBetter(this Database.Models.Score oldScore, LeaderboardEntry newScore, QualifierEvent.LeaderboardSort sort)
        {
            return sort switch
            {
                QualifierEvent.LeaderboardSort.ModifiedScoreAscending or QualifierEvent.LeaderboardSort.NotesMissedAscending or QualifierEvent.LeaderboardSort.BadCutsAscending or QualifierEvent.LeaderboardSort.GoodCutsAscending or QualifierEvent.LeaderboardSort.MaxComboAscending => oldScore.GetScoreValueByQualifierSettings(sort) > newScore.GetScoreValueByQualifierSettings(sort),
                _ => oldScore.GetScoreValueByQualifierSettings(sort) < newScore.GetScoreValueByQualifierSettings(sort),
            };
        }

        public static IOrderedQueryable<Database.Models.Score> OrderByQualifierSettings(this IQueryable<Database.Models.Score> scores, QualifierEvent.LeaderboardSort sort, bool invert = false)
        {
            return sort switch
            {
                QualifierEvent.LeaderboardSort.ModifiedScoreAscending => invert ? scores.OrderByDescending(x => x.ModifiedScore) : scores.OrderBy(x => x.ModifiedScore),
                QualifierEvent.LeaderboardSort.NotesMissed => invert ? scores.OrderBy(x => x.NotesMissed) : scores.OrderByDescending(x => x.NotesMissed),
                QualifierEvent.LeaderboardSort.NotesMissedAscending => invert ? scores.OrderByDescending(x => x.NotesMissed) : scores.OrderBy(x => x.NotesMissed),
                QualifierEvent.LeaderboardSort.BadCuts => invert ? scores.OrderBy(x => x.BadCuts) : scores.OrderByDescending(x => x.BadCuts),
                QualifierEvent.LeaderboardSort.BadCutsAscending => invert ? scores.OrderByDescending(x => x.BadCuts) : scores.OrderBy(x => x.BadCuts),
                QualifierEvent.LeaderboardSort.GoodCuts => invert ? scores.OrderBy(x => x.GoodCuts) : scores.OrderByDescending(x => x.GoodCuts),
                QualifierEvent.LeaderboardSort.GoodCutsAscending => invert ? scores.OrderByDescending(x => x.GoodCuts) : scores.OrderBy(x => x.GoodCuts),
                QualifierEvent.LeaderboardSort.MaxCombo => invert ? scores.OrderBy(x => x.MaxCombo) : scores.OrderByDescending(x => x.MaxCombo),
                QualifierEvent.LeaderboardSort.MaxComboAscending => invert ? scores.OrderByDescending(x => x.MaxCombo) : scores.OrderBy(x => x.MaxCombo),
                _ => invert ? scores.OrderBy(x => x.ModifiedScore) : scores.OrderByDescending(x => x.ModifiedScore),
            };
        }
    }
}
