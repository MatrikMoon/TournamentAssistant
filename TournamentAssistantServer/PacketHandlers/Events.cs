using System;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.Discord;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    [Module(Packet.packetOneofCase.Request, "packet.Request.TypeCase")]
    class Events
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }
        public StateManager StateManager { get; set; }
        public DatabaseService DatabaseService { get; set; }
        public QualifierBot QualifierBot { get; set; }

        [AllowFromPlayer]
        [AllowFromWebsocket]
        [PacketHandler((int)Request.TypeOneofCase.update_user)]
        public async Task UpdateUser()
        {
            var updateUser = ExecutionContext.Packet.Request.update_user;

            //TODO: Do permission checks

            await StateManager.UpdateUser(updateUser.tournamentId, updateUser.User);

            await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
                    RespondingToPacketId = ExecutionContext.Packet.Id,
                    update_user = new Response.UpdateUser
                    {
                        Message = "Successfully updated user"
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Request.TypeOneofCase.create_match)]
        public async Task CreateMatch()
        {
            var createMatch = ExecutionContext.Packet.Request.create_match;

            //TODO: Do permission checks

            await StateManager.CreateMatch(createMatch.tournamentId, createMatch.Match);

            await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
                    RespondingToPacketId = ExecutionContext.Packet.Id,
                    create_match = new Response.CreateMatch
                    {
                        Message = "Successfully created match"
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Request.TypeOneofCase.update_match)]
        public async Task UpdateMatch()
        {
            var updateMatch = ExecutionContext.Packet.Request.update_match;

            //TODO: Do permission checks

            await StateManager.UpdateMatch(updateMatch.tournamentId, updateMatch.Match);

            await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
                    RespondingToPacketId = ExecutionContext.Packet.Id,
                    update_match = new Response.UpdateMatch
                    {
                        Message = "Successfully updated match"
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Request.TypeOneofCase.delete_match)]
        public async Task DeleteMatch()
        {
            var deleteMatch = ExecutionContext.Packet.Request.delete_match;

            //TODO: Do permission checks

            await StateManager.DeleteMatch(deleteMatch.tournamentId, deleteMatch.Match);

            await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
                    RespondingToPacketId = ExecutionContext.Packet.Id,
                    delete_match = new Response.DeleteMatch
                    {
                        Message = "Successfully deleted match"
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Request.TypeOneofCase.create_qualifier_event)]
        public async Task CreateQualifier()
        {
            var createQualifier = ExecutionContext.Packet.Request.create_qualifier_event;

            //TODO: Do permission checks

            await StateManager.CreateQualifier(createQualifier.tournamentId, createQualifier.Event);

            await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
                    RespondingToPacketId = ExecutionContext.Packet.Id,
                    create_qualifier_event = new Response.CreateQualifierEvent
                    {
                        Message = "Successfully created qualifier"
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Request.TypeOneofCase.update_qualifier_event)]
        public async Task UpdateQualifier()
        {
            var updateQualifier = ExecutionContext.Packet.Request.update_qualifier_event;

            //TODO: Do permission checks

            await StateManager.UpdateQualifier(updateQualifier.tournamentId, updateQualifier.Event);

            await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
                    RespondingToPacketId = ExecutionContext.Packet.Id,
                    update_qualifier_event = new Response.UpdateQualifierEvent
                    {
                        Message = "Successfully updated qualifier"
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Request.TypeOneofCase.delete_qualifier_event)]
        public async Task DeleteQualifier()
        {
            var deleteQualifier = ExecutionContext.Packet.Request.delete_qualifier_event;

            //TODO: Do permission checks

            await StateManager.DeleteQualifier(deleteQualifier.tournamentId, deleteQualifier.Event);

            await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
                    RespondingToPacketId = ExecutionContext.Packet.Id,
                    delete_qualifier_event = new Response.DeleteQualifierEvent
                    {
                        Message = "Successfully deleted qualifier"
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Request.TypeOneofCase.create_tournament)]
        public async Task CreateTournament()
        {
            var createTournament = ExecutionContext.Packet.Request.create_tournament;

            //TODO: Do permission checks

            var tournament = await StateManager.CreateTournament(createTournament.Tournament);

            await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
                    RespondingToPacketId = ExecutionContext.Packet.Id,
                    create_tournament = new Response.CreateTournament
                    {
                        Message = "Successfully created tournament",
                        Tournament = tournament
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Request.TypeOneofCase.update_tournament)]
        public async Task UpdateTournament()
        {
            var updateTournament = ExecutionContext.Packet.Request.update_tournament;

            //TODO: Do permission checks

            await StateManager.UpdateTournament(updateTournament.Tournament);

            await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
                    RespondingToPacketId = ExecutionContext.Packet.Id,
                    update_tournament = new Response.UpdateTournament
                    {
                        Message = "Successfully updated tournament"
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Request.TypeOneofCase.delete_tournament)]
        public async Task DeleteTournament()
        {
            var deleteTournament = ExecutionContext.Packet.Request.delete_tournament;

            //TODO: Do permission checks

            await StateManager.DeleteTournament(deleteTournament.Tournament);

            await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
                    RespondingToPacketId = ExecutionContext.Packet.Id,
                    delete_tournament = new Response.DeleteTournament
                    {
                        Message = "Successfully deleted tournament"
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Request.TypeOneofCase.add_server)]
        public async Task AddServer()
        {
            var addServer = ExecutionContext.Packet.Request.add_server;

            //TODO: Do permission checks

            //To add a server to the master list, we'll need to be sure we can connect to it first. If not, we'll tell the requester why.
            var newConnection = new TAClient(addServer.Server.Address, addServer.Server.Port);

            //If we've been provided with a token to use, use it
            if (!string.IsNullOrWhiteSpace(addServer.AuthToken))
            {
                newConnection.SetAuthToken(addServer.AuthToken);
            }

            newConnection.ConnectedToServer += async (response) =>
            {
                TAServer.AddServerConnection(newConnection);

                await StateManager.AddServer(addServer.Server);

                await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
                        RespondingToPacketId = ExecutionContext.Packet.Id,
                        add_server = new Response.AddServer
                        {
                            Message = $"Server added to the master list!",
                        },
                    }
                });
            };

            newConnection.AuthorizationRequestedFromServer += async (authRequest) =>
            {
                newConnection.Shutdown();

                await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Fail,
                        RespondingToPacketId = ExecutionContext.Packet.Id,
                        add_server = new Response.AddServer
                        {
                            Message = $"Could not connect to your server due to an authorization error. Try adding an auth token in your AddServerToList request",
                        },
                    }
                });
            };

            newConnection.FailedToConnectToServer += async (response) =>
            {
                newConnection.Shutdown();

                await TAServer.Send(Guid.Parse(ExecutionContext.User.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Fail,
                        RespondingToPacketId = ExecutionContext.Packet.Id,
                        add_server = new Response.AddServer
                        {
                            Message = $"Could not connect to your server. Try connecting directly to your server from TAUI to see if it's accessible from a regular/external setup",
                        },
                    }
                });
            };

            await newConnection.Connect();
        }
    }
}
