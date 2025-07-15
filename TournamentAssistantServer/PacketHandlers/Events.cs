using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.ASP.Attributes;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.Permissions;
using Packets = TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    [Module(Packet.packetOneofCase.Request, "packet.Request.TypeCase")]
    public class Events : ControllerBase
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }
        public StateManager StateManager { get; set; }

        [AllowFromPlayer]
        [AllowFromWebsocket]
        [PacketHandler((int)Packets.Request.TypeOneofCase.update_user)]
        [HttpPut]
        public async Task UpdateUser([FromBody] Packet packet, [FromUser] User user)
        {
            var updateUser = packet.Request.update_user;

            // Users may only update their own state
            if (user.Guid != updateUser.User.Guid)
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_user = new Response.UpdateUser
                        {
                            Message = "You may only update your own user",
                            User = updateUser.User
                        }
                    }
                });
                return;
            }

            await StateManager.UpdateUser(updateUser.TournamentId, updateUser.User);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Packets.Response.ResponseType.Success,
                    RespondingToPacketId = packet.Id,
                    update_user = new Response.UpdateUser
                    {
                        Message = "Successfully updated user",
                        User = updateUser.User
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.CreateMatch)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.create_match)]
        [HttpPost]
        public async Task CreateMatch([FromBody] Packet packet, [FromUser] User user)
        {
            var createMatch = packet.Request.create_match;

            var match = await StateManager.CreateMatch(createMatch.TournamentId, createMatch.Match);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Packets.Response.ResponseType.Success,
                    RespondingToPacketId = packet.Id,
                    create_match = new Response.CreateMatch
                    {
                        Message = "Successfully created match",
                        Match = match
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.AddUserToMatch)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.add_user_to_match)]
        [HttpPost]
        public async Task AddUserToMatch([FromBody] Packet packet, [FromUser] User user)
        {
            var updateMatch = packet.Request.add_user_to_match;

            var existingMatch = StateManager.GetMatch(updateMatch.TournamentId, updateMatch.MatchId);
            if (existingMatch != null)
            {
                existingMatch.AssociatedUsers.Add(updateMatch.UserId);

                await StateManager.UpdateMatch(updateMatch.TournamentId, existingMatch);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_match = new Response.UpdateMatch
                        {
                            Message = "Successfully updated match",
                            Match = existingMatch
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_match = new Response.UpdateMatch
                        {
                            Message = "Match does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.RemoveUserFromMatch)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.remove_user_from_match)]
        [HttpPut]
        public async Task RemoveUserFromMatch([FromBody] Packet packet, [FromUser] User user)
        {
            var updateMatch = packet.Request.remove_user_from_match;

            var existingMatch = StateManager.GetMatch(updateMatch.TournamentId, updateMatch.MatchId);
            if (existingMatch != null)
            {
                existingMatch.AssociatedUsers.RemoveAll(x => x == updateMatch.UserId);

                await StateManager.UpdateMatch(updateMatch.TournamentId, existingMatch);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_match = new Response.UpdateMatch
                        {
                            Message = "Successfully updated match",
                            Match = existingMatch
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_match = new Response.UpdateMatch
                        {
                            Message = "Match does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetMatchLeader)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_match_leader)]
        [HttpPut]
        public async Task SetMatchLeader([FromBody] Packet packet, [FromUser] User user)
        {
            var updateMatch = packet.Request.set_match_leader;

            var existingMatch = StateManager.GetMatch(updateMatch.TournamentId, updateMatch.MatchId);
            if (existingMatch != null)
            {
                existingMatch.Leader = updateMatch.UserId;

                await StateManager.UpdateMatch(updateMatch.TournamentId, existingMatch);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_match = new Response.UpdateMatch
                        {
                            Message = "Successfully updated match",
                            Match = existingMatch
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_match = new Response.UpdateMatch
                        {
                            Message = "Match does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetMatchMap)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_match_map)]
        [HttpPut]
        public async Task SetMatchMap([FromBody] Packet packet, [FromUser] User user)
        {
            var updateMatch = packet.Request.set_match_map;

            var existingMatch = StateManager.GetMatch(updateMatch.TournamentId, updateMatch.MatchId);
            if (existingMatch != null)
            {
                existingMatch.SelectedMap = updateMatch.Map;

                await StateManager.UpdateMatch(updateMatch.TournamentId, existingMatch);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_match = new Response.UpdateMatch
                        {
                            Message = "Successfully updated match",
                            Match = existingMatch
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_match = new Response.UpdateMatch
                        {
                            Message = "Match does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.DeleteMatch)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.delete_match)]
        [HttpPut]
        public async Task DeleteMatch([FromBody] Packet packet, [FromUser] User user)
        {
            var deleteMatch = packet.Request.delete_match;

            var match = await StateManager.DeleteMatch(deleteMatch.TournamentId, deleteMatch.MatchId);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Packets.Response.ResponseType.Success,
                    RespondingToPacketId = packet.Id,
                    delete_match = new Response.DeleteMatch
                    {
                        Message = "Successfully deleted match",
                        Match = match
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.CreateQualifier)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.create_qualifier_event)]
        [HttpPost]
        public async Task CreateQualifier([FromBody] Packet packet, [FromUser] User user)
        {
            var createQualifier = packet.Request.create_qualifier_event;

            var qualifier = await StateManager.CreateQualifier(createQualifier.TournamentId, createQualifier.Event);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Packets.Response.ResponseType.Success,
                    RespondingToPacketId = packet.Id,
                    create_qualifier_event = new Response.CreateQualifierEvent
                    {
                        Message = "Successfully created qualifier",
                        Qualifier = qualifier
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetQualifierName)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_qualifier_name)]
        [HttpPut]
        public async Task SetQualifierName([FromBody] Packet packet, [FromUser] User user)
        {
            var updateQualifier = packet.Request.set_qualifier_name;

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.Name = updateQualifier.QualifierName;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Successfully updated qualifier",
                            Qualifier = existingQualifier
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Qualifier does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetQualifierImage)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_qualifier_image)]
        [HttpPut]
        public async Task SetQualifierImage([FromBody] Packet packet, [FromUser] User user)
        {
            var updateQualifier = packet.Request.set_qualifier_image;

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.Image = updateQualifier.QualifierImage;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Successfully updated qualifier",
                            Qualifier = existingQualifier
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Qualifier does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetQualifierInfoChannel)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_qualifier_info_channel)]
        [HttpPut]
        public async Task SetQualifierInfoChannel([FromBody] Packet packet, [FromUser] User user)
        {
            var updateQualifier = packet.Request.set_qualifier_info_channel;

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.InfoChannel = updateQualifier.InfoChannel;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Successfully updated qualifier",
                            Qualifier = existingQualifier
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Qualifier does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetQualifierFlags)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_qualifier_flags)]
        [HttpPut]
        public async Task SetQualifierFlags([FromBody] Packet packet, [FromUser] User user)
        {
            var updateQualifier = packet.Request.set_qualifier_flags;

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.Flags = updateQualifier.QualifierFlags;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Successfully updated qualifier",
                            Qualifier = existingQualifier
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Qualifier does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetQualifierLeaderboardSort)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_qualifier_leaderboard_sort)]
        [HttpPut]
        public async Task SetQualifierLeaderboardSort([FromBody] Packet packet, [FromUser] User user)
        {
            var updateQualifier = packet.Request.set_qualifier_leaderboard_sort;

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.Sort = updateQualifier.QualifierLeaderboardSort;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Successfully updated qualifier",
                            Qualifier = existingQualifier
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Qualifier does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.AddQualifierMaps)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.add_qualifier_maps)]
        [HttpPost]
        public async Task AddQualifierMaps([FromBody] Packet packet, [FromUser] User user)
        {
            var updateQualifier = packet.Request.add_qualifier_maps;

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.QualifierMaps.AddRange(updateQualifier.Maps.Select(x => {
                    x.Guid = Guid.NewGuid().ToString();
                    return x;
                }));

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Successfully updated qualifier",
                            Qualifier = existingQualifier
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Qualifier does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.UpdateQualifierMap)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.update_qualifier_map)]
        [HttpPut]
        public async Task UpdateQualifierMap([FromBody] Packet packet, [FromUser] User user)
        {
            var updateQualifier = packet.Request.update_qualifier_map;

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                var replaceIndex = existingQualifier.QualifierMaps.FindIndex(x => x.Guid == updateQualifier.Map.Guid);
                if (replaceIndex == -1)
                {
                    await TAServer.Send(Guid.Parse(user.Guid), new Packet
                    {
                        Response = new Response
                        {
                            Type = Packets.Response.ResponseType.Fail,
                            RespondingToPacketId = packet.Id,
                            update_qualifier_event = new Response.UpdateQualifierEvent
                            {
                                Message = "Could not find qualifier map with that ID",
                                Qualifier = existingQualifier
                            }
                        }
                    });
                    return;
                }

                existingQualifier.QualifierMaps[replaceIndex] = updateQualifier.Map;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Successfully updated qualifier",
                            Qualifier = existingQualifier
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Qualifier does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.RemoveQualifierMap)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.remove_qualifier_map)]
        [HttpPut]
        public async Task RemoveQualifierMap([FromBody] Packet packet, [FromUser] User user)
        {
            var updateQualifier = packet.Request.remove_qualifier_map;

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.QualifierMaps.RemoveAll(x => x.Guid == updateQualifier.MapId);

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Successfully updated qualifier",
                            Qualifier = existingQualifier
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_qualifier_event = new Response.UpdateQualifierEvent
                        {
                            Message = "Qualifier does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.DeleteQualifier)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.delete_qualifier_event)]
        [HttpPut]
        public async Task DeleteQualifier([FromBody] Packet packet, [FromUser] User user)
        {
            var deleteQualifier = packet.Request.delete_qualifier_event;

            var qualifier = await StateManager.DeleteQualifier(deleteQualifier.TournamentId, deleteQualifier.QualifierId);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Packets.Response.ResponseType.Success,
                    RespondingToPacketId = packet.Id,
                    delete_qualifier_event = new Response.DeleteQualifierEvent
                    {
                        Message = "Successfully deleted qualifier",
                        Qualifier = qualifier
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Packets.Request.TypeOneofCase.create_tournament)]
        [HttpPost]
        public async Task CreateTournament([FromBody] Packet packet, [FromUser] User user)
        {
            var createTournament = packet.Request.create_tournament;

            var tournament = await StateManager.CreateTournament(createTournament.Tournament, user);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Packets.Response.ResponseType.Success,
                    RespondingToPacketId = packet.Id,
                    create_tournament = new Response.CreateTournament
                    {
                        Message = "Successfully created tournament",
                        Tournament = tournament
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentName)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_name)]
        [HttpPut]
        public async Task SetTournamentName([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_name;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.TournamentName = updateTournament.TournamentName;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentImage)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_image)]
        [HttpPut]
        public async Task SetTournamentImage([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_image;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.TournamentImage = updateTournament.TournamentImage;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentEnableTeams)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_enable_teams)]
        [HttpPut]
        public async Task SetTournamentEnableTeams([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_enable_teams;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.EnableTeams = updateTournament.EnableTeams;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentEnablePools)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_enable_pools)]
        [HttpPut]
        public async Task SetTournamentEnablePools([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_enable_pools;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.EnablePools = updateTournament.EnablePools;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentShowTournamentButton)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_show_tournament_button)]
        [HttpPut]
        public async Task SetTournamentShowTournamentButton([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_show_tournament_button;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.ShowTournamentButton = updateTournament.ShowTournamentButton;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentShowQualifierButton)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_show_qualifier_button)]
        [HttpPut]
        public async Task SetTournamentShowQualifierButton([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_show_qualifier_button;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.ShowQualifierButton = updateTournament.ShowQualifierButton;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentAllowUnauthorizedView)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_allow_unauthorized_view)]
        [HttpPut]
        public async Task SetTournamentAllowUnauthorizedView([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_allow_unauthorized_view;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.AllowUnauthorizedView = updateTournament.AllowUnauthorizedView;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentScoreUpdateFrequency)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_score_update_frequency)]
        [HttpPut]
        public async Task SetTournamentScoreUpdateFrequency([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_score_update_frequency;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.ScoreUpdateFrequency = updateTournament.ScoreUpdateFrequency;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentBannedMods)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_banned_mods)]
        [HttpPut]
        public async Task SetTournamentBannedMods([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_banned_mods;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.BannedMods.Clear();
                existingTournament.Settings.BannedMods.AddRange(updateTournament.BannedMods);

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.AddTournamentRole)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.add_tournament_role)]
        [HttpPost]
        public async Task AddTournamentRole([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.add_tournament_role;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                if (existingTournament.Settings.Roles.Any(x => x.RoleId == updateTournament.Role.RoleId))
                {
                    await TAServer.Send(Guid.Parse(user.Guid), new Packet
                    {
                        Response = new Response
                        {
                            Type = Packets.Response.ResponseType.Fail,
                            RespondingToPacketId = packet.Id,
                            update_tournament = new Response.UpdateTournament
                            {
                                Message = "Role with that ID already exists",
                                Tournament = existingTournament
                            }
                        }
                    });
                    return;
                }

                updateTournament.Role.Guid = Guid.NewGuid().ToString();
                existingTournament.Settings.Roles.Add(updateTournament.Role);

                await StateManager.AddTournamentRole(existingTournament, updateTournament.Role);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentRoleName)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_role_name)]
        [HttpPut]
        public async Task SetTournamentRoleName([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_role_name;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingRoleIndex = existingTournament.Settings.Roles.FindIndex(x => x.Guid == updateTournament.RoleId || x.RoleId == updateTournament.RoleId);
                if (existingRoleIndex == -1)
                {
                    await TAServer.Send(Guid.Parse(user.Guid), new Packet
                    {
                        Response = new Response
                        {
                            Type = Packets.Response.ResponseType.Fail,
                            RespondingToPacketId = packet.Id,
                            update_tournament = new Response.UpdateTournament
                            {
                                Message = "Could not find role with that ID",
                                Tournament = existingTournament
                            }
                        }
                    });
                    return;
                }

                existingTournament.Settings.Roles[existingRoleIndex].Name = updateTournament.RoleName;

                await StateManager.UpdateTournamentRole(existingTournament, existingTournament.Settings.Roles[existingRoleIndex]);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentRolePermissions)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_role_permissions)]
        [HttpPut]
        public async Task SetTournamentRolePermissions([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_role_permissions;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingRoleIndex = existingTournament.Settings.Roles.FindIndex(x => x.Guid == updateTournament.RoleId || x.RoleId == updateTournament.RoleId);
                if (existingRoleIndex == -1)
                {
                    await TAServer.Send(Guid.Parse(user.Guid), new Packet
                    {
                        Response = new Response
                        {
                            Type = Packets.Response.ResponseType.Fail,
                            RespondingToPacketId = packet.Id,
                            update_tournament = new Response.UpdateTournament
                            {
                                Message = "Could not find role with that ID",
                                Tournament = existingTournament
                            }
                        }
                    });
                    return;
                }

                existingTournament.Settings.Roles[existingRoleIndex].Permissions.Clear();
                existingTournament.Settings.Roles[existingRoleIndex].Permissions.AddRange(updateTournament.Permissions);

                await StateManager.UpdateTournamentRole(existingTournament, existingTournament.Settings.Roles[existingRoleIndex]);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.RemoveTournamentRole)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.remove_tournament_role)]
        [HttpPut]
        public async Task RemoveTournamentRole([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.remove_tournament_role;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingRole = existingTournament.Settings.Roles.FirstOrDefault(x => x.Guid == updateTournament.RoleId || x.RoleId == updateTournament.RoleId);
                if (existingRole == null)
                {
                    await TAServer.Send(Guid.Parse(user.Guid), new Packet
                    {
                        Response = new Response
                        {
                            Type = Packets.Response.ResponseType.Success,
                            RespondingToPacketId = packet.Id,
                            update_tournament = new Response.UpdateTournament
                            {
                                Message = "Could not find role with that ID",
                                Tournament = existingTournament
                            }
                        }
                    });
                    return;
                }

                existingTournament.Settings.Roles.RemoveAll(x => x.Guid == updateTournament.RoleId);

                await StateManager.RemoveTournamentRole(existingTournament, existingRole);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.AddTournamentTeam)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.add_tournament_team)]
        [HttpPost]
        public async Task AddTournamentTeam([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.add_tournament_team;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                updateTournament.Team.Guid = Guid.NewGuid().ToString();
                existingTournament.Settings.Teams.Add(updateTournament.Team);

                await StateManager.AddTournamentTeam(existingTournament, updateTournament.Team);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentTeamName)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_team_name)]
        [HttpPut]
        public async Task SetTournamentTeamName([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_team_name;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingTeamIndex = existingTournament.Settings.Teams.FindIndex(x => x.Guid == updateTournament.TeamId);
                if (existingTeamIndex == -1)
                {
                    await TAServer.Send(Guid.Parse(user.Guid), new Packet
                    {
                        Response = new Response
                        {
                            Type = Packets.Response.ResponseType.Fail,
                            RespondingToPacketId = packet.Id,
                            update_tournament = new Response.UpdateTournament
                            {
                                Message = "Could not find team with that ID",
                                Tournament = existingTournament
                            }
                        }
                    });
                    return;
                }

                existingTournament.Settings.Teams[existingTeamIndex].Name = updateTournament.TeamName;

                await StateManager.UpdateTournamentTeam(existingTournament, existingTournament.Settings.Teams[existingTeamIndex]);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentTeamImage)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_team_image)]
        [HttpPut]
        public async Task SetTournamentTeamImage([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_team_image;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingTeamIndex = existingTournament.Settings.Teams.FindIndex(x => x.Guid == updateTournament.TeamId);
                if (existingTeamIndex == -1)
                {
                    await TAServer.Send(Guid.Parse(user.Guid), new Packet
                    {
                        Response = new Response
                        {
                            Type = Packets.Response.ResponseType.Fail,
                            RespondingToPacketId = packet.Id,
                            update_tournament = new Response.UpdateTournament
                            {
                                Message = "Could not find team with that ID",
                                Tournament = existingTournament
                            }
                        }
                    });
                    return;
                }

                existingTournament.Settings.Teams[existingTeamIndex].Image = updateTournament.TeamImage;

                await StateManager.UpdateTournamentTeam(existingTournament, existingTournament.Settings.Teams[existingTeamIndex]);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.RemoveTournamentTeam)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.remove_tournament_team)]
        [HttpPut]
        public async Task RemoveTournamentTeam([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.remove_tournament_team;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingTeam = existingTournament.Settings.Teams.Find(x => x.Guid == updateTournament.TeamId);
                existingTournament.Settings.Teams.RemoveAll(x => x.Guid == updateTournament.TeamId);

                await StateManager.RemoveTournamentTeam(existingTournament, existingTeam);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.AddTournamentPool)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.add_tournament_pool)]
        [HttpPost]
        public async Task AddTournamentPool([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.add_tournament_pool;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                updateTournament.Pool.Guid = Guid.NewGuid().ToString();
                existingTournament.Settings.Pools.Add(updateTournament.Pool);

                await StateManager.AddTournamentPool(existingTournament, updateTournament.Pool);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.SetTournamentPoolName)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.set_tournament_pool_name)]
        [HttpPut]
        public async Task SetTournamentPoolName([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.set_tournament_pool_name;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingIndex = existingTournament.Settings.Pools.FindIndex(x => x.Guid == updateTournament.PoolId);
                if (existingIndex == -1)
                {
                    await TAServer.Send(Guid.Parse(user.Guid), new Packet
                    {
                        Response = new Response
                        {
                            Type = Packets.Response.ResponseType.Fail,
                            RespondingToPacketId = packet.Id,
                            update_tournament = new Response.UpdateTournament
                            {
                                Message = "Could not find pool with that ID",
                                Tournament = existingTournament
                            }
                        }
                    });
                    return;
                }

                existingTournament.Settings.Pools[existingIndex].Name = updateTournament.PoolName;

                await StateManager.UpdateTournamentPool(existingTournament, existingTournament.Settings.Pools[existingIndex]);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.AddTournamentPoolMaps)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.add_tournament_pool_maps)]
        [HttpPost]
        public async Task AddTournamentPoolMaps([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.add_tournament_pool_maps;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingPool = existingTournament.Settings.Pools.FirstOrDefault(x => x.Guid == updateTournament.PoolId);
                existingPool.Maps.AddRange(updateTournament.Maps);

                foreach (var map in updateTournament.Maps)
                {
                    await StateManager.AddTournamentPoolSong(existingTournament, existingPool, map);
                }

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.UpdateTournamentPoolMaps)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.update_tournament_pool_map)]
        [HttpPut]
        public async Task UpdateTournamentPoolMap([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.update_tournament_pool_map;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingPool = existingTournament.Settings.Pools.FirstOrDefault(x => x.Guid == updateTournament.PoolId);
                var existingMapIndex = existingPool.Maps.FindIndex(x => x.Guid == updateTournament.Map.Guid);
                if (existingMapIndex == -1)
                {
                    await TAServer.Send(Guid.Parse(user.Guid), new Packet
                    {
                        Response = new Response
                        {
                            Type = Packets.Response.ResponseType.Fail,
                            RespondingToPacketId = packet.Id,
                            update_tournament = new Response.UpdateTournament
                            {
                                Message = "Could not find pool map with that ID",
                                Tournament = existingTournament
                            }
                        }
                    });
                    return;
                }

                existingPool.Maps[existingMapIndex] = updateTournament.Map;

                await StateManager.UpdateTournamentPoolSong(existingTournament, existingPool, updateTournament.Map);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.RemoveTournamentPoolMaps)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.remove_tournament_pool_map)]
        [HttpPut]
        public async Task RemoveTournamentPoolMap([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.remove_tournament_pool_map;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingPool = existingTournament.Settings.Pools.FirstOrDefault(x => x.Guid == updateTournament.PoolId);
                var existingMap = existingPool.Maps.First(x => x.Guid == updateTournament.MapId);
                existingPool.Maps.RemoveAll(x => x.Guid == updateTournament.MapId);

                await StateManager.RemoveTournamentPoolSong(existingTournament, existingMap);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.RemoveTournamentPools)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.remove_tournament_pool)]
        [HttpPut]
        public async Task RemoveTournamentPool([FromBody] Packet packet, [FromUser] User user)
        {
            var updateTournament = packet.Request.remove_tournament_pool;

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingPool = existingTournament.Settings.Pools.FirstOrDefault(x => x.Guid == updateTournament.PoolId);
                existingTournament.Settings.Pools.RemoveAll(x => x.Guid == updateTournament.PoolId);

                await StateManager.RemoveTournamentPool(existingTournament, existingPool);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Successfully updated tournament",
                            Tournament = existingTournament
                        }
                    }
                });
            }
            else
            {
                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
                        update_tournament = new Response.UpdateTournament
                        {
                            Message = "Tournament does not exist"
                        }
                    }
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.DeleteTournament)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.delete_tournament)]
        [HttpPut]
        public async Task DeleteTournament([FromBody] Packet packet, [FromUser] User user)
        {
            var deleteTournament = packet.Request.delete_tournament;

            var tournament = await StateManager.DeleteTournament(deleteTournament.TournamentId);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Packets.Response.ResponseType.Success,
                    RespondingToPacketId = packet.Id,
                    delete_tournament = new Response.DeleteTournament
                    {
                        Message = "Successfully deleted tournament",
                        Tournament = tournament
                    }
                }
            });
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Packets.Request.TypeOneofCase.add_server)]
        [NonAction]
        public async Task AddServer([FromBody] Packet packet, [FromUser] User user)
        {
            var addServer = packet.Request.add_server;

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

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Success,
                        RespondingToPacketId = packet.Id,
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

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
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

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Packets.Response.ResponseType.Fail,
                        RespondingToPacketId = packet.Id,
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
