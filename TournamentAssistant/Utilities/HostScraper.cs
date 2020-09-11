using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistant.Utilities
{
    public class HostScraper
    {
        public static void SendPacketToHost(CoreServer host, Packet packet, string username, ulong userId)
        {
            new IndividualHostScraper
            {
                Host = host,
                UserId = userId,
                Username = username
            }.SendPacket(packet);
        }

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

            private PluginClient StartConnection()
            {
                var client = new PluginClient(Host.Address, Host.Port, Username, UserId.ToString(), Connect.ConnectTypes.TemporaryConnection);
                client.ConnectedToServer += Client_ConnectedToServer;
                client.FailedToConnectToServer += Client_FailedToConnectToServer;
                client.Start();
                connected.WaitOne();
                return client;
            }

            internal async Task<State> ScrapeHost()
            {
                return await Task.Run(() =>
                {
                    var client = StartConnection();

                    var state = client.Connected ? client.State : null;
                    client.Shutdown();

                    return state;
                });
            }

            internal void SendPacket(Packet packet)
            {
                Task.Run(() =>
                {
                    var client = StartConnection();
                    client.Send(packet).AsyncWaitHandle.WaitOne();
                    client.Shutdown();
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
