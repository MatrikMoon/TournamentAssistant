using System.Threading.Tasks;

namespace TournamentAssistant.Interop
{
    static class EUCInterop
    {
        public static Task<int> CheckRemainingAttempts(string userId, string levelId, int difficulty)
        {
            return EUCModule.EUC.CheckRemainingAttempts(userId, levelId, difficulty);
        }

        public static Task CreateScore(string userId, string levelId, int difficulty)
        {
            return EUCModule.EUC.CreateScore(userId, levelId, difficulty);
        }

        public static Task SubmitScore(string userId, int score)
        {
            return EUCModule.EUC.SubmitScore(userId, score);
        }
    }
}
