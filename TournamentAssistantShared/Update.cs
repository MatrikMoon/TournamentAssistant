using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared.SimpleJSON;

/**
 * Created by Moon on 9/12/2020, 1:41AM.
 * This is a very simple extension of a standard SystemClient,
 * with the addition of an event which provides packets as they
 * are received. This is useful for temporary clients (HostScraper)
 * so they can perform one simple action, wait for a reaction, then
 * disconnect. Should not be used for other purposes, use a SystemClient
 * for more robust clients, use a Client directly for more simple ones.
 */

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
                        bool UpdateSuccess = false; //await AutoUpdater.AttemptAutoUpdate();
                        if (!UpdateSuccess)
                        {
                            Logger.Error("AutoUpdate Failed, The server will now shut down. Please update to continue.");
                            //doAfterUpdate();
                        }
                        else
                        {
                            Logger.Warning("Update Successful, exiting...");
                            //doAfterUpdate();
                        }
                    }
                    await Task.Delay(/*1000 * 60 * 10*/ 100, cancellationToken);
                }
            });
        }

        public static async Task<Version> GetLatestRelease()
        {
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = false;

            using var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.Add("user-agent", $"{SharedConstructs.Name}");

            var response = await client.GetAsync($"https://api.github.com/repos/MatrikMoon/TournamentAssistant/releases/latest");
            var result = JSON.Parse(await response.Content.ReadAsStringAsync());

            return Version.Parse(result["tag_name"]);
        }
    }
} 