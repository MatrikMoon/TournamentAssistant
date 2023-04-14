using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.SimpleJSON;

/**
 * Rewritten by Moon on 4/4/2023 at 11:35PM to try to
 * replicate the new Typescrpt scraper found in the new TAUI
 */

namespace TournamentAssistantShared
{
    public class Scraper
    {
        public class TournamentWithServerInfo
        {
            public Tournament Tournament { get; set; }
            public CoreServer Server { get; set; }
        }

        public class OnProgressData
        {
            public int TotalServers { get; set; }
            public int SucceededServers { get; set; }
            public int FailedServers { get; set; }
            public List<TournamentWithServerInfo> Tournaments { get; set; }
        }

        public static void GetTournaments(string token, Action<OnProgressData> onProgress, Action<OnProgressData> onComplete)
        {
            var scraper = new ScraperInstance(token);
            scraper.OnProgress += (progress) =>
            {
                onProgress(progress);

                if (progress.FailedServers + progress.SucceededServers == progress.TotalServers)
                {
                    onComplete(progress);
                }
            };

            scraper.GetTournaments();
        }

        private class ScraperInstance
        {
            public event Action<OnProgressData> OnProgress;

            private string token;
            private List<CoreServer> servers = new List<CoreServer>();
            private List<TournamentWithServerInfo> tournaments = new List<TournamentWithServerInfo>();

            private int succeededServers = 0;
            private int failedServers = 0;

            public ScraperInstance(string token)
            {
                this.token = token;
            }

            public void GetTournaments()
            {
                var masterClient = new TemporaryClient(Constants.MASTER_SERVER, 2052);
                masterClient.SetAuthToken(token);

                masterClient.ConnectedToServer += (response) =>
                {
                    servers = response.State.KnownServers;
                    tournaments = response.State.Tournaments.Select(x =>
                        new TournamentWithServerInfo
                        {
                            Tournament = x,
                            Server = new CoreServer
                            {
                                Address = Constants.MASTER_SERVER,
                                Name = "Default Server",
                                Port = 2052,
                                WebsocketPort = 2053
                            }
                        }).ToList();

                    masterClient.Shutdown();

                    //We successfully got tournaments from the master server
                    succeededServers++;

                    if (OnProgress != null)
                    {
                        OnProgress.Invoke(new OnProgressData
                        {
                            TotalServers = servers.Count,
                            SucceededServers = succeededServers,
                            FailedServers = failedServers,
                            Tournaments = tournaments
                        });
                    }

                    //Kick off all the individual requests to the found servers that aren't the master server
                    servers
                        .Where(x => $"{x.Address}:{x.Port}" != $"{Constants.MASTER_SERVER}:2052")
                        .ForEach(x => GetTournamentsFromServer(x));

                    return Task.CompletedTask;
                };

                masterClient.FailedToConnectToServer += (response) =>
                {
                    //We failed to get tournaments from the master server
                    failedServers++;

                    if (OnProgress != null)
                    {
                        OnProgress.Invoke(new OnProgressData
                        {
                            TotalServers = 1,
                            SucceededServers = 0,
                            FailedServers = 1,
                            Tournaments = tournaments
                        });
                    }

                    //Don't bother retrying, as there isn't a retry limit. May as well let the user know there's connection issues
                    masterClient.Shutdown();

                    return Task.CompletedTask;
                };

                Task.Run(masterClient.Start);
            }

            private void GetTournamentsFromServer(CoreServer server)
            {
                var client = new TemporaryClient(server.Address, server.Port);
                client.SetAuthToken(token);

                client.ConnectedToServer += (response) =>
                {
                    tournaments.AddRange(response.State.Tournaments.Select(x => new TournamentWithServerInfo
                    {
                        Tournament = x,
                        Server = server
                    }));

                    client.Shutdown();

                    //We successfully got tournaments from the server
                    succeededServers++;

                    if (OnProgress != null)
                    {
                        OnProgress.Invoke(new OnProgressData
                        {
                            TotalServers = servers.Count,
                            SucceededServers = succeededServers,
                            FailedServers = failedServers,
                            Tournaments = tournaments
                        });
                    }

                    return Task.CompletedTask;
                };

                client.FailedToConnectToServer += (response) =>
                {
                    //We failed to get tournaments from the master server
                    failedServers++;

                    if (OnProgress != null)
                    {
                        OnProgress.Invoke(new OnProgressData
                        {
                            TotalServers = servers.Count,
                            SucceededServers = succeededServers,
                            FailedServers = failedServers,
                            Tournaments = tournaments
                        });
                    }

                    //Don't bother trying to reconnect if a connection fails
                    client.Shutdown();

                    return Task.CompletedTask;
                };

                Task.Run(client.Start);
            }
        }
    }
}