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
        public static async Task<Packet> RequestResponse(CoreServer host, Packet packet,
            Packet.packetOneofCase responseType,
            string username, ulong userId)
        {
            return await new IndividualHostScraper
            {
                Host = host,
                UserId = userId,
                Username = username
            }.SendRequest(packet, responseType);
        }

        public static async Task<State> ScrapeHost(CoreServer host, string username, ulong userId,
            CoreServer self = null, Action<CoreServer, State, int, int> onInstanceComplete = null) =>
            (await ScrapeHosts(new CoreServer[] { host }, username, userId, self, onInstanceComplete)).First().Value;

        public static async Task<Dictionary<CoreServer, State>> ScrapeHosts(CoreServer[] hosts, string username,
            ulong userId, CoreServer self = null, Action<CoreServer, State, int, int> onInstanceComplete = null)
        {
            var scrapedHosts = new Dictionary<CoreServer, State>();
            var finishedCount = 0;

            Func<CoreServer, Task> scrapeTask = async (host) =>
            {
                await Task.Run(async () =>
                {
                    Logger.Success($"Starting scrape task {host}");

                    var state = await new IndividualHostScraper()
                    {
                        Host = host,
                        Username = username,
                        UserId = userId
                    }.ScrapeState(self);

                    if (state != null) scrapedHosts[host] = state;
                    onInstanceComplete?.Invoke(host, state, ++finishedCount, hosts.Length);
                });
            };

            await Task.WhenAll(hosts.Select(x => scrapeTask(x)));
            return scrapedHosts;
        }

        internal class IndividualHostScraper
        {
            internal CoreServer Host { get; set; }
            internal string Username { get; set; }
            internal ulong UserId { get; set; }

            private AutoResetEvent connected = new(false);
            private AutoResetEvent responseReceived = new(false);

            private const int timeout = 4000;

            internal TemporaryClient StartConnection()
            {
                var client = new TemporaryClient(Host.Address, Host.Port, Username, UserId.ToString(), User.ClientTypes.TemporaryConnection);
                client.ConnectedToServer += Client_ConnectedToServer;
                client.FailedToConnectToServer += Client_FailedToConnectToServer;

                Task.Run(client.Start);

                Logger.Warning($"Beginning 1 second wait {Host.Address}");
                var didntTimeOut = connected.WaitOne(timeout); //Note to future Moon: The old client start didn't wait for connections. At all.

                Logger.Error(didntTimeOut ? $"{Host.Address} connected and signaled" : $"{Host.Address} timed out (1s)");
                return client;
            }

            internal async Task<State> ScrapeState(CoreServer self = null)
            {
                var client = StartConnection();
                var state = client.Connected ? client.State : null;

                //Add our self to the server's list of active servers
                if (self != null && client.Connected)
                {
                    await client.Send(new Packet
                    {
                        Event = new Event
                        {
                            host_added_event = new Event.HostAddedEvent
                            {
                                Server = self
                            }
                        }
                    });
                }

                client.Shutdown();
                return state?.ServerSettings != null ? state : null;
            }

            internal async Task SendPacket(Packet requestPacket)
            {
                var client = StartConnection();
                if (client.Connected)
                {
                    await client.Send(requestPacket);
                    client.Shutdown();
                }
            }

            internal async Task<Packet> SendRequest(Packet requestPacket, Packet.packetOneofCase responseType)
            {
                Packet responsePacket = null;
                var client = StartConnection();
                if (client.Connected)
                {
                    client.PacketReceived += (packet) =>
                    {
                        if (packet.packetCase == responseType)
                        {
                            responsePacket = packet;
                            responseReceived.Set();
                        }

                        return Task.CompletedTask;
                    };
                    await client.Send(requestPacket);
                    responseReceived.WaitOne(timeout);
                    client.Shutdown();
                }
                return responsePacket;
            }

            protected Task Client_ConnectedToServer(ConnectResponse response)
            {
                connected.Set();
                return Task.CompletedTask;
            }

            protected Task Client_FailedToConnectToServer(ConnectResponse response)
            {
                connected.Set();
                return Task.CompletedTask;
            }
        }
    }
}