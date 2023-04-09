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
            public string Address { get; set; }
            public string Port { get; set; }
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
                            Address = Constants.MASTER_SERVER,
                            Port = "2052"
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
                        .ForEach(x => GetTournamentsFromServer(x.Address, x.Port));

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
                            TotalServers = servers.Count,
                            SucceededServers = succeededServers,
                            FailedServers = failedServers,
                            Tournaments = tournaments
                        });
                    }

                    return Task.CompletedTask;
                };

                Task.Run(masterClient.Start);
            }

            private void GetTournamentsFromServer(string address, int port)
            {
                var client = new TemporaryClient(address, port);
                client.SetAuthToken(token);

                client.ConnectedToServer += (response) =>
                {
                    tournaments.AddRange(response.State.Tournaments.Select(x => new TournamentWithServerInfo
                    {
                        Tournament = x,
                        Address = address,
                        Port = $"{port}"
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

                    return Task.CompletedTask;
                };

                Task.Run(client.Start);
            }
        }
    }
}