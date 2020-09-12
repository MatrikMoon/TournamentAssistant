using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantShared
{
    public class HostScraper
    {
        public static async Task<Packet> RequestResponse(CoreServer host, Packet packet, Type responseType, string username, ulong userId)
        {
            return await new IndividualHostScraper
            {
                Host = host,
                UserId = userId,
                Username = username
            }.SendRequest(packet, responseType);
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
                }.ScrapeState();

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
            private AutoResetEvent responseReceived = new AutoResetEvent(false);

            private TemporaryClient StartConnection()
            {
                var client = new TemporaryClient(Host.Address, Host.Port, Username, UserId.ToString(), Connect.ConnectTypes.TemporaryConnection);
                client.ConnectedToServer += Client_ConnectedToServer;
                client.FailedToConnectToServer += Client_FailedToConnectToServer;
                client.Start();
                connected.WaitOne();
                return client;
            }

            internal async Task<State> ScrapeState()
            {
                return await Task.Run(() =>
                {
                    var client = StartConnection();
                    var state = client.Connected ? client.State : null;
                    client.Shutdown();

                    return state;
                });
            }

            internal void SendPacket(Packet requestPacket)
            {
                Task.Run(() =>
                {
                    Packet responsePacket = null;
                    var client = StartConnection();
                    client.Send(requestPacket).AsyncWaitHandle.WaitOne();
                    client.Shutdown();
                    return responsePacket;
                });
            }

            internal async Task<Packet> SendRequest(Packet requestPacket, Type responseType)
            {
                return await Task.Run(() =>
                {
                    Packet responsePacket = null;
                    var client = StartConnection();
                    client.PacketReceived += (packet) =>
                    {
                        if (packet.SpecificPacket.GetType() == responseType)
                        {
                            responsePacket = packet;
                            responseReceived.Set();
                        }
                    };
                    client.Send(requestPacket);
                    responseReceived.WaitOne(6000);
                    client.Shutdown();
                    return responsePacket;
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
