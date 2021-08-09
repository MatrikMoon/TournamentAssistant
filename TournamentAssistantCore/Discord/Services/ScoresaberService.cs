using System.Net.Http;
using System.Threading.Tasks;
using TournamentAssistantShared.SimpleJSON;

namespace TournamentAssistantCore.Discord.Services
{
    public class ScoresaberService
    {
        public string GetPlayerRank(JSONNode basicData)
        {
            return basicData["playerInfo"]["rank"];
        }

        public string GetPlayerCountry(JSONNode basicData)
        {
            return basicData["playerInfo"]["country"];
        }

        public async Task<JSONNode> GetBasicPlayerData(string userId)
        {
            JSONNode basicData = null;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", "WorldCupBot");

                var url = $"https://new.scoresaber.com/api/player/{userId}/basic";
                var response = await client.GetStringAsync(url);

                JSONNode node = JSON.Parse(response);
                basicData = node;
            }
            return basicData;
        }
    }
}
