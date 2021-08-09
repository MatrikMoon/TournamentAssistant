using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared.SimpleJSON;

namespace TournamentAssistantShared
{
    public class Update
    {
        public static void PollForUpdates(Action doAfterUpdate, CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (Version.Parse(SharedConstructs.Version) < await GetLatestRelease())
                    {
                        bool UpdateSuccess = await AutoUpdater.AttemptAutoUpdate();
                        if (!UpdateSuccess)
                        {
                            Logger.Error("AutoUpdate Failed, The server will now shut down. Please update to continue.");
                            doAfterUpdate();
                        }
                        else
                        {
                            Logger.Warning("Update Successful, exiting...");
                            doAfterUpdate();
                        }
                    }
                    await Task.Delay(1000 * 60 * 10, cancellationToken);
                }
            });
        }

        public static async Task<Version> GetLatestRelease()
        {
            HttpClientHandler httpClientHandler = new();
            httpClientHandler.AllowAutoRedirect = false;

            using var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.Add("user-agent", $"{SharedConstructs.Name}");

            var response = client.GetAsync($"https://api.github.com/repos/MatrikMoon/TournamentAssistant/releases/latest");
            response.Wait();

            var result = JSON.Parse(await response.Result.Content.ReadAsStringAsync());

            return Version.Parse(result["tag_name"]);
        }
    }
}