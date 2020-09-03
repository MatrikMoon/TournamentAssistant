using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistant.Misc
{
    public class HostScraper
    {
        public static async Task<Dictionary<CoreServer, State>> ScrapeHosts(CoreServer[] hosts, string username, ulong userId, Action<CoreServer, State, int, int> onInstanceComplete = null)
        {
            var scrapedHosts = new Dictionary<CoreServer, State>();
            var finishedCount = 0;

            Func<CoreServer, Task> scrapeTask = async (host) =>
            {
                var state = await new IndividualHostScraper()
                {
                    Host = host,
                    Username = username,
                    UserId = userId
                }.ScrapeHost();

                if (state != null) scrapedHosts[host] = state;
                onInstanceComplete?.Invoke(host, state, ++finishedCount, hosts.Length);
            };

            await Task.WhenAll(hosts.ToList().Select(x => scrapeTask(x)));
            return scrapedHosts;
        }

        internal class IndividualHostScraper
        {
            internal CoreServer Host { get; set; }
            internal string Username { get; set; }
            internal ulong UserId { get; set; }

            private AutoResetEvent connected = new AutoResetEvent(false);

            internal async Task<State> ScrapeHost()
            {
                return await Task.Run(() =>
                {
                    var client = new PluginClient(Host.Address, Host.Port, Username, UserId.ToString(), Connect.ConnectTypes.Scraper);
                    client.ConnectedToServer += Client_ConnectedToServer;
                    client.FailedToConnectToServer += Client_FailedToConnectToServer;
                    client.Start();

                    connected.WaitOne();
                    var state = client.Connected ? client.State : null;
                    client.Shutdown();

                    return state;
                });
            }

            protected void Client_ConnectedToServer(ConnectResponse response)
            {
                connected.Set();
            }

            protected void Client_FailedToConnectToServer(ConnectResponse response)
            {
                connected.Set();
            }
        }
    }
}
