using System;
using System.Net.Http;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.SimpleJSON;

/**
 * Update checker created by Moon, merged with Ari's AutoUpdater on 10/14/2021
 * Checks for and downloads new updates when they are available
 */

namespace TournamentAssistant.Utilities
{
    public static class Updater
    {
        // For easy switching if those ever changed
        // Moon's note: while the repo url is unlikely to change, the filenames are free game. I type and upload those manually, after all
        // private static readonly string _repoURL = "https://github.com/MatrikMoon/TournamentAssistant/releases/latest";
        private static readonly string _repoAPI = "https://api.github.com/repos/MatrikMoon/TournamentAssistant/releases/latest";

        public static async Task<Version> GetLatestRelease()
        {
            using var httpClientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };

            using var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.Add("user-agent", $"{Constants.NAME}");

            var response = await client.GetAsync(_repoAPI);
            var result = JSON.Parse(await response.Content.ReadAsStringAsync());

            return Version.Parse(result["tag_name"]);
        }
    }
}