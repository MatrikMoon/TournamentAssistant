using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Sockets;
using Timer = System.Timers.Timer;

namespace TournamentAssistantShared
{
    public class TAClient
    {
        public class ResponseFromUser
        {
            public string userId;
            public Response response;
        }

        public TAClient(string endpoint, int port)
        {
            Endpoint = endpoint;
            Port = port;
        }

        public event Func<Response.Connect, Task> ConnectedToServer;
        public event Func<Response.Connect, Task> FailedToConnectToServer;
        public event Func<Task> ServerDisconnected;
        public event Func<string, Task> AuthorizationRequestedFromServer;

        public event Func<Response.Join, Task> JoinedTournament;
        public event Func<Response.Join, Task> FailedToJoinTournament;

        public event Func<Request.ShowModal, Task> ShowModal;
        public event Func<Push.SongFinished, Task> PlayerFinishedSong;
        public event Func<RealtimeScore, Task> RealtimeScoreReceived;

        public StateManager StateManager { get; set; } = new StateManager();

        public bool Connected => client?.Connected ?? false;

        protected Client client;
        private string _authToken;

        private Timer _heartbeatTimer = new();
        private bool _shouldHeartbeat;

        public string Endpoint { get; private set; }
        public int Port { get; private set; }

        public void SetAuthToken(string authToken)
        {
            _authToken = authToken;
        }

        //Blocks until connected (or failed), then returns response
        public async Task<Response> Connect()
        {
            //Don't heartbeat while connecting
            _heartbeatTimer.Stop();

            var promise = new TaskCompletionSource<Response>();
            Func<Task> onConnectedToServer = null;
            Func<Task> onFailedToConnectToServer = null;

            try
            {
                client = new Client(Endpoint, Port);
                client.PacketReceived += Client_PacketWrapperReceived;
                client.ServerConnected += Client_ServerConnected;
                client.ServerFailedToConnect += Client_ServerFailedToConnect;
                client.ServerDisconnected += Client_ServerDisconnected;

                _heartbeatTimer = new Timer();
                _heartbeatTimer.Interval = 10000;
                _heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;

                //TODO: I don't think Register awaits async callbacks 
                var cancellationTokenSource = new CancellationTokenSource();
                var registration = cancellationTokenSource.Token.Register(() =>
                {
                    client.ServerConnected -= onConnectedToServer;
                    client.ServerFailedToConnect -= onFailedToConnectToServer;

                    cancellationTokenSource.Dispose();

                    client.Shutdown();
                    promise.SetException(new Exception("Server timed out"));
                });

                onConnectedToServer = async () =>
                {
                    registration.Dispose();
                    cancellationTokenSource.Dispose();

                    var response = await SendRequest(new Request
                    {
                        connect = new Request.Connect
                        {
                            ClientVersion = Constants.VERSION_CODE
                        }
                    });

                    client.ServerConnected -= onConnectedToServer;
                    client.ServerFailedToConnect -= onFailedToConnectToServer;

                    if (response.Length <= 0)
                    {
                        client.Shutdown();
                        promise.SetException(new Exception("Server timed out"));
                    }
                    else
                    {
                        promise.SetResult(response[0].response);
                    }
                };

                onFailedToConnectToServer = () =>
                {
                    client.ServerConnected -= onConnectedToServer;
                    client.ServerFailedToConnect -= onFailedToConnectToServer;

                    registration.Dispose();
                    cancellationTokenSource.Dispose();

                    client.Shutdown();
                    promise.SetException(new Exception("Failed to connect to server"));

                    return Task.CompletedTask;
                };

                client.ServerConnected += onConnectedToServer;
                client.ServerFailedToConnect += onFailedToConnectToServer;
                cancellationTokenSource.CancelAfter(5000);

                await client.Start();
            }
            catch (Exception e)
            {
                promise.SetException(new Exception("Failed to connect to server"));
                Logger.Debug("Failed to connect to server. NOT retrying...");
                Logger.Debug(e.ToString());
            }

            return await promise.Task;
        }

        private void HeartbeatTimer_Elapsed(object _, ElapsedEventArgs __)
        {
            //Send needs to be awaited so it will properly catch exceptions, but we can't make this timer callback async. So, we do this.
            async Task timerAction()
            {
                try
                {
                    await SendToServer(new Packet
                    {
                        Command = new Command
                        {
                            Heartbeat = true
                        }
                    });
                }
                catch (Exception e)
                {
                    Logger.Debug("HEARTBEAT FAILED");
                    Logger.Debug(e.ToString());

                    await Connect();
                }
            }
            Task.Run(timerAction);
        }

        public void Shutdown()
        {
            client?.Shutdown();
            _heartbeatTimer.Stop();

            //If the client was connecting when we shut it down, the FailedToConnect event might resurrect the heartbeat without this
            _shouldHeartbeat = false;
        }

        // -- Actions -- //

        public async Task<Response> JoinTournament(string tournamentId, string password = "")
        {
            var response = await SendRequest(new Request
            {
                join = new Request.Join
                {
                    TournamentId = tournamentId,
                    Password = password
                }
            });

            if (response.Length <= 0)
            {
                throw new Exception("Server timed out");
            }

            return response[0].response;
        }

        public Task RespondToModal(string[] recipients, string modalId, ModalOption response)
        {
            return Send(recipients, new Packet
            {
                Response = new Response
                {
                    show_modal = new Response.ShowModal
                    {
                        ModalId = modalId.ToString(),
                        Value = response.Value
                    }
                }
            });
        }

        public Task SendLoadSong(string[] recipients, string levelId)
        {
            return Send(recipients, new Packet
            {
                Request = new Request
                {
                    load_song = new Request.LoadSong
                    {
                        LevelId = levelId
                    }
                }
            });
        }

        public Task SendPlaySong(string[] recipients, string levelId, Characteristic characteristic, int difficulty)
        {
            return Send(recipients, new Packet
            {
                Command = new Command
                {
                    play_song = new Command.PlaySong
                    {
                        GameplayParameters = new GameplayParameters
                        {
                            Beatmap = new Beatmap
                            {
                                Characteristic = characteristic,
                                Difficulty = difficulty,
                                LevelId = levelId
                            },
                            GameplayModifiers = new GameplayModifiers(),
                            PlayerSettings = new PlayerSpecificSettings()
                        },
                        FloatingScoreboard = true
                    }
                }
            });
        }

        public Task SendSongFinished(User player, string levelId, int difficulty, Characteristic characteristic, Push.SongFinished.CompletionType type, int score)
        {
            return SendToServer(new Packet
            {
                Push = new Push
                {
                    song_finished = new Push.SongFinished
                    {
                        Player = player,
                        Beatmap = new Beatmap
                        {
                            LevelId = levelId,
                            Difficulty = difficulty,
                            Characteristic = characteristic
                        },
                        Score = score,
                        Type = type
                    }
                }
            });
        }

        public async Task<Response> SendQualifierScore(string qualifierId, QualifierEvent.QualifierMap map, string platformId, string username, int multipliedScore, int modifiedScore, int maxPossibleScore, float accuracy, int notesMissed, int badCuts, int goodCuts, int maxCombo, bool fullCombo, bool isPlaceholder)
        {
            var response = await SendRequest(new Request
            {
                submit_qualifier_score = new Request.SubmitQualifierScore
                {
                    Map = map.GameplayParameters,
                    QualifierScore = new LeaderboardEntry
                    {
                        EventId = qualifierId,
                        MapId = map.Guid,
                        PlatformId = platformId,
                        Username = username,
                        MultipliedScore = multipliedScore,
                        ModifiedScore = modifiedScore,
                        MaxPossibleScore = maxPossibleScore,
                        Accuracy = accuracy,
                        NotesMissed = notesMissed,
                        BadCuts = badCuts,
                        GoodCuts = goodCuts,
                        MaxCombo = maxCombo,
                        FullCombo = fullCombo,
                        IsPlaceholder = isPlaceholder,
                        Color = "#ffffff"
                    }

                }
            });

            if (response.Length <= 0)
            {
                throw new Exception("Server timed out");
            }

            return response[0].response;
        }

        public async Task<Response> RequestLeaderboard(string qualifierId, string mapId)
        {
            var response = await SendRequest(new Request
            {
                qualifier_scores = new Request.QualifierScores
                {
                    EventId = qualifierId,
                    MapId = mapId,
                }
            });

            if (response.Length <= 0)
            {
                throw new Exception("Server timed out");
            }

            return response[0].response;
        }

        public async Task<Response> RequestAttempts(string qualifierId, string mapId)
        {
            var response = await SendRequest(new Request
            {
                remaining_attempts = new Request.RemainingAttempts
                {
                    EventId = qualifierId,
                    MapId = mapId,
                }
            });

            if (response.Length <= 0)
            {
                throw new Exception("Server timed out");
            }

            return response[0].response;
        }

        //TODO: To align with what I'm doing above, these parameters should probably be primitives... But it's almost midnight and I'm lazy.
        //Come back to this one.
        public Task SendRealtimeScore(string[] recipients, RealtimeScore score)
        {
            return Send(recipients, new Packet
            {
                Push = new Push
                {
                    RealtimeScore = score
                }
            });
        }

        // -- Various send methods -- //

        protected async Task<ResponseFromUser[]> SendRequest(Request requestPacket, string[] recipients = null, int timeout = 5000)
        {
            var packet = new Packet
            {
                Request = requestPacket
            };

            packet.Id = Guid.NewGuid().ToString();
            packet.From = StateManager.GetSelfGuid();
            packet.Token = _authToken;

            var promise = new TaskCompletionSource<ResponseFromUser[]>();
            var responses = new List<ResponseFromUser>();
            var expectedUsers = recipients != null && recipients.Length > 0 ? recipients : new[] { "00000000-0000-0000-0000-000000000000" };
            Func<PacketWrapper, Task> onPacketReceived = null;

            //TODO: I don't think Register awaits async callbacks 
            var cancellationTokenSource = new CancellationTokenSource();
            var registration = cancellationTokenSource.Token.Register(() =>
            {
                client.PacketReceived -= onPacketReceived;
                promise.SetResult(responses.ToArray());

                cancellationTokenSource.Dispose();
            });

            onPacketReceived = (responsePacket) =>
            {
                if (responsePacket.Payload.packetCase == Packet.packetOneofCase.Response &&
                    responsePacket.Payload.Response?.RespondingToPacketId == packet.Id &&
                    expectedUsers.Contains(responsePacket.Payload.From))
                {
                    responses.Add(new ResponseFromUser
                    {
                        userId = responsePacket.Payload.From,
                        response = responsePacket.Payload.Response
                    });

                    if (expectedUsers.SequenceEqual(responses.Select(x => x.userId)))
                    {
                        client.PacketReceived -= onPacketReceived;

                        registration.Dispose();
                        cancellationTokenSource.Dispose();

                        promise.SetResult(responses.ToArray());
                    }
                }
                return Task.CompletedTask;
            };

            cancellationTokenSource.CancelAfter(timeout);
            client.PacketReceived += onPacketReceived;

            if (recipients != null && recipients.Length > 0)
            {
                await Send(recipients, packet);
            }
            else
            {
                await SendToServer(packet);
            }

            return await promise.Task;
        }

        protected Task SendResponse(string[] recipients, Response response)
        {
            return Send(recipients, new Packet
            {
                Response = response
            });
        }

        protected Task SendPush(string[] recipients, Push push)
        {
            return Send(recipients, new Packet
            {
                Push = push
            });
        }

        protected Task SendCommand(Command command, string[] recipients = null)
        {
            return Send(recipients, new Packet
            {
                Command = command
            });
        }

        private Task Send(string id, Packet packet) => Send(new[] { id }, packet);

        private Task Send(string[] ids, Packet packet)
        {
            var forwardedPacket = new ForwardingPacket
            {
                Packet = packet
            };
            forwardedPacket.ForwardToes.AddRange(ids);

            return ForwardToUser(forwardedPacket);
        }

        private Task ForwardToUser(ForwardingPacket forwardingPacket)
        {
            var innerPacket = forwardingPacket.Packet;
            Logger.Debug($"Forwarding data: {LogPacket(innerPacket)}");

            if (string.IsNullOrEmpty(innerPacket.Id))
            {
                innerPacket.Id = Guid.NewGuid().ToString();
            }

            innerPacket.From = StateManager.GetSelfGuid();
            innerPacket.Token = _authToken;

            return SendToServer(new Packet
            {
                ForwardingPacket = forwardingPacket
            });
        }

        private Task SendToServer(Packet packet)
        {
            Logger.Debug($"Sending data: {LogPacket(packet)}");

            if (string.IsNullOrEmpty(packet.Id))
            {
                packet.Id = Guid.NewGuid().ToString();
            }

            packet.From = StateManager.GetSelfGuid();
            packet.Token = _authToken;

            return client.Send(new PacketWrapper(packet));
        }

        static string LogPacket(Packet packet)
        {
            string secondaryInfo = string.Empty;
            if (packet.packetCase == Packet.packetOneofCase.Command)
            {
                var command = packet.Command;
                if (command.TypeCase == Command.TypeOneofCase.play_song)
                {
                    var playSong = command.play_song;
                    secondaryInfo = playSong.GameplayParameters.Beatmap.LevelId + " : " +
                                    playSong.GameplayParameters.Beatmap.Difficulty;
                }
                else
                {
                    secondaryInfo = command.TypeCase.ToString();
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Request)
            {
                var request = packet.Request;
                if (request.TypeCase == Request.TypeOneofCase.load_song)
                {
                    var loadSong = request.load_song;
                    secondaryInfo = loadSong.LevelId;
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Event)
            {
                var @event = packet.Event;

                secondaryInfo = @event.ChangedObjectCase.ToString();
                if (@event.ChangedObjectCase == Event.ChangedObjectOneofCase.user_updated)
                {
                    var user = @event.user_updated.User;
                    secondaryInfo =
                        $"{secondaryInfo} from ({user.Name} : {user.DownloadState}) : ({user.PlayState} : {user.StreamDelayMs})";
                }
                else if (@event.ChangedObjectCase == Event.ChangedObjectOneofCase.match_updated)
                {
                    var match = @event.match_updated.Match;
                    secondaryInfo = $"{secondaryInfo} ({match.SelectedDifficulty})";
                }
            }

            return $"({packet.packetCase}) ({secondaryInfo})";
        }

        #region State Actions
        public async Task UpdateUser(string tournamentId, User user)
        {
            var request = new Request
            {
                update_user = new Request.UpdateUser
                {
                    tournamentId = tournamentId,
                    User = user
                }
            };

            await SendToServer(new Packet
            {
                Request = request
            });
        }

        public async Task CreateMatch(string tournamentId, Match match)
        {
            var request = new Request
            {
                create_match = new Request.CreateMatch
                {
                    tournamentId = tournamentId,
                    Match = match
                }
            };

            await SendToServer(new Packet
            {
                Request = request
            });
        }

        public async Task UpdateMatch(string tournamentId, Match match)
        {
            var request = new Request
            {
                update_match = new Request.UpdateMatch
                {
                    tournamentId = tournamentId,
                    Match = match
                }
            };

            await SendToServer(new Packet
            {
                Request = request
            });
        }

        public async Task DeleteMatch(string tournamentId, Match match)
        {
            var request = new Request
            {
                delete_match = new Request.DeleteMatch
                {
                    tournamentId = tournamentId,
                    Match = match
                }
            };

            await SendToServer(new Packet
            {
                Request = request
            });
        }

        public async Task CreateQualifierEvent(string tournamentId, QualifierEvent qualifierEvent)
        {
            var request = new Request
            {
                create_qualifier_event = new Request.CreateQualifierEvent
                {
                    tournamentId = tournamentId,
                    Event = qualifierEvent
                }
            };

            await SendToServer(new Packet
            {
                Request = request
            });
        }

        public async Task UpdateQualifierEvent(string tournamentId, QualifierEvent qualifierEvent)
        {
            var request = new Request
            {
                update_qualifier_event = new Request.UpdateQualifierEvent
                {
                    tournamentId = tournamentId,
                    Event = qualifierEvent
                }
            };

            await SendToServer(new Packet
            {
                Request = request
            });
        }

        public async Task DeleteQualifierEvent(string tournamentId, QualifierEvent qualifierEvent)
        {
            var request = new Request
            {
                delete_qualifier_event = new Request.DeleteQualifierEvent
                {
                    tournamentId = tournamentId,
                    Event = qualifierEvent
                }
            };

            await SendToServer(new Packet
            {
                Request = request
            });
        }

        public async Task CreateTournament(Tournament tournament)
        {
            var request = new Request
            {
                create_tournament = new Request.CreateTournament
                {
                    Tournament = tournament
                }
            };

            await SendToServer(new Packet
            {
                Request = request
            });
        }

        public async Task UpdateTournament(Tournament tournament)
        {
            var request = new Request
            {
                update_tournament = new Request.UpdateTournament
                {
                    Tournament = tournament
                }
            };

            await SendToServer(new Packet
            {
                Request = request
            });
        }

        public async Task DeleteTournament(Tournament tournament)
        {
            var request = new Request
            {
                delete_tournament = new Request.DeleteTournament
                {
                    Tournament = tournament
                }
            };

            await SendToServer(new Packet
            {
                Request = request
            });
        }
        #endregion State Actions

        private Task Client_ServerConnected()
        {
            _heartbeatTimer.Start();

            /*Self = new User
            {
                Name = username,
                ClientType = clientType,
                UserId = userId
            };
            Self.ModLists.AddRange(modList);*/

            return Task.CompletedTask;
        }

        private async Task Client_ServerFailedToConnect()
        {
            if (FailedToConnectToServer != null) await FailedToConnectToServer.Invoke(null);
        }

        private async Task Client_ServerDisconnected()
        {
            Logger.Debug("SystemClient: Server disconnected!");
            if (ServerDisconnected != null) await ServerDisconnected.Invoke();
        }

        protected virtual async Task Client_PacketWrapperReceived(PacketWrapper packet)
        {
            await Client_PacketReceived(packet.Payload);
        }

        protected virtual async Task Client_PacketReceived(Packet packet)
        {
            await StateManager.HandlePacket(packet);

            Logger.Debug($"Received data: {LogPacket(packet)}");

            //Ready to go, only disabled since it is currently unusued
            /*if (packet.Type != PacketType.Acknowledgement)
            {
                Send(packet.From, new Packet(new Acknowledgement()
                {
                    PacketId = packet.Id
                }));
            }*/

            /*if (packet.packetCase == Packet.packetOneofCase.Acknowledgement)
            {
                var acknowledgement = packet.Acknowledgement;
            }*/

            if (packet.packetCase == Packet.packetOneofCase.Command)
            {
                var command = packet.Command;
                if (command.TypeCase == Command.TypeOneofCase.DiscordAuthorize)
                {
                    if (AuthorizationRequestedFromServer != null) await AuthorizationRequestedFromServer.Invoke(command.DiscordAuthorize);
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Push)
            {
                var push = packet.Push;
                if (push.DataCase == Push.DataOneofCase.song_finished)
                {
                    if (PlayerFinishedSong != null) await PlayerFinishedSong.Invoke(push.song_finished);
                }
                else if (push.DataCase == Push.DataOneofCase.RealtimeScore)
                {
                    if (RealtimeScoreReceived != null) await RealtimeScoreReceived.Invoke(push.RealtimeScore);
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Request)
            {
                var request = packet.Request;
                if (request.TypeCase == Request.TypeOneofCase.show_modal)
                {
                    if (ShowModal != null) await ShowModal.Invoke(request.show_modal);
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Response)
            {
                var response = packet.Response;
                if (response.DetailsCase == Response.DetailsOneofCase.connect)
                {
                    var connectResponse = response.connect;
                    if (response.Type == Response.ResponseType.Success)
                    {
                        if (ConnectedToServer != null) await ConnectedToServer.Invoke(connectResponse);
                    }
                    else if (response.Type == Response.ResponseType.Fail)
                    {
                        if (FailedToConnectToServer != null) await FailedToConnectToServer.Invoke(connectResponse);
                    }
                }
                else if (response.DetailsCase == Response.DetailsOneofCase.join)
                {
                    var joinResponse = response.join;
                    if (response.Type == Response.ResponseType.Success)
                    {
                        if (JoinedTournament != null) await JoinedTournament.Invoke(joinResponse);
                    }
                    else if (response.Type == Response.ResponseType.Fail)
                    {
                        if (FailedToJoinTournament != null) await FailedToJoinTournament.Invoke(joinResponse);
                    }
                }
            }
        }
    }
}