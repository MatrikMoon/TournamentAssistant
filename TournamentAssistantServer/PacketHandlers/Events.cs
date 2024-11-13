using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    [Module(Packet.packetOneofCase.Request, "packet.Request.TypeCase")]
    class Events
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }
        public StateManager StateManager { get; set; }

        [AllowFromPlayer]
        [AllowFromWebsocket]
        [RequirePermission(Permissions.View)]
        [PacketHandler((int)Request.TypeOneofCase.update_user)]
        public async Task UpdateUser(Packet packet, User user)
        {
            var updateUser = packet.Request.update_user;

            //TODO: Do permission checks

            await StateManager.UpdateUser(updateUser.TournamentId, updateUser.User);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.create_match)]
        public async Task CreateMatch(Packet packet, User user)
        {
            var createMatch = packet.Request.create_match;

            //TODO: Do permission checks

            var match = await StateManager.CreateMatch(createMatch.TournamentId, createMatch.Match);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.add_user_to_match)]
        public async Task AddUserToMatch(Packet packet, User user)
        {
            var updateMatch = packet.Request.add_user_to_match;

            //TODO: Do permission checks

            var existingMatch = StateManager.GetMatch(updateMatch.TournamentId, updateMatch.MatchId);
            if (existingMatch != null)
            {
                existingMatch.AssociatedUsers.Add(updateMatch.UserId);

                await StateManager.UpdateMatch(updateMatch.TournamentId, existingMatch);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.remove_user_from_match)]
        public async Task RemoveUserFromMatch(Packet packet, User user)
        {
            var updateMatch = packet.Request.remove_user_from_match;

            //TODO: Do permission checks

            var existingMatch = StateManager.GetMatch(updateMatch.TournamentId, updateMatch.MatchId);
            if (existingMatch != null)
            {
                existingMatch.AssociatedUsers.RemoveAll(x => x == updateMatch.UserId);

                await StateManager.UpdateMatch(updateMatch.TournamentId, existingMatch);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_match_leader)]
        public async Task SetMatchLeader(Packet packet, User user)
        {
            var updateMatch = packet.Request.set_match_leader;

            //TODO: Do permission checks

            var existingMatch = StateManager.GetMatch(updateMatch.TournamentId, updateMatch.MatchId);
            if (existingMatch != null)
            {
                existingMatch.Leader = updateMatch.UserId;

                await StateManager.UpdateMatch(updateMatch.TournamentId, existingMatch);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_match_map)]
        public async Task SetMatchMap(Packet packet, User user)
        {
            var updateMatch = packet.Request.set_match_map;

            //TODO: Do permission checks

            var existingMatch = StateManager.GetMatch(updateMatch.TournamentId, updateMatch.MatchId);
            if (existingMatch != null)
            {
                existingMatch.SelectedMap = updateMatch.Map;

                await StateManager.UpdateMatch(updateMatch.TournamentId, existingMatch);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.delete_match)]
        public async Task DeleteMatch(Packet packet, User user)
        {
            var deleteMatch = packet.Request.delete_match;

            //TODO: Do permission checks

            var match = await StateManager.DeleteMatch(deleteMatch.TournamentId, deleteMatch.MatchId);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.create_qualifier_event)]
        public async Task CreateQualifier(Packet packet, User user)
        {
            var createQualifier = packet.Request.create_qualifier_event;

            //TODO: Do permission checks

            var qualifier = await StateManager.CreateQualifier(createQualifier.TournamentId, createQualifier.Event);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_qualifier_name)]
        public async Task SetQualifierName(Packet packet, User user)
        {
            var updateQualifier = packet.Request.set_qualifier_name;

            //TODO: Do permission checks

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.Name = updateQualifier.QualifierName;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_qualifier_image)]
        public async Task SetQualifierImage(Packet packet, User user)
        {
            var updateQualifier = packet.Request.set_qualifier_image;

            //TODO: Do permission checks

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.Image = updateQualifier.QualifierImage;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_qualifier_info_channel)]
        public async Task SetQualifierInfoChannel(Packet packet, User user)
        {
            var updateQualifier = packet.Request.set_qualifier_info_channel;

            //TODO: Do permission checks

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.InfoChannel = updateQualifier.InfoChannel;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_qualifier_flags)]
        public async Task SetQualifierFlags(Packet packet, User user)
        {
            var updateQualifier = packet.Request.set_qualifier_flags;

            //TODO: Do permission checks

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.Flags = updateQualifier.QualifierFlags;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_qualifier_leaderboard_sort)]
        public async Task SetQualifierLeaderboardSort(Packet packet, User user)
        {
            var updateQualifier = packet.Request.set_qualifier_leaderboard_sort;

            //TODO: Do permission checks

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.Sort = updateQualifier.QualifierLeaderboardSort;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.add_qualifier_maps)]
        public async Task AddQualifierMaps(Packet packet, User user)
        {
            var updateQualifier = packet.Request.add_qualifier_maps;

            //TODO: Do permission checks

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.QualifierMaps.AddRange(updateQualifier.Maps);

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.update_qualifier_map)]
        public async Task UpdateQualifierMap(Packet packet, User user)
        {
            var updateQualifier = packet.Request.update_qualifier_map;

            //TODO: Do permission checks

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                var replaceIndex = existingQualifier.QualifierMaps.FindIndex(x => x.Guid == updateQualifier.Map.Guid);
                existingQualifier.QualifierMaps[replaceIndex] = updateQualifier.Map;

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.remove_qualifier_map)]
        public async Task RemoveQualifierMap(Packet packet, User user)
        {
            var updateQualifier = packet.Request.remove_qualifier_map;

            //TODO: Do permission checks

            var existingQualifier = StateManager.GetQualifier(updateQualifier.TournamentId, updateQualifier.QualifierId);
            if (existingQualifier != null)
            {
                existingQualifier.QualifierMaps.RemoveAll(x => x.Guid == updateQualifier.MapId);

                await StateManager.UpdateQualifier(updateQualifier.TournamentId, existingQualifier);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.delete_qualifier_event)]
        public async Task DeleteQualifier(Packet packet, User user)
        {
            var deleteQualifier = packet.Request.delete_qualifier_event;

            //TODO: Do permission checks

            var qualifier = await StateManager.DeleteQualifier(deleteQualifier.TournamentId, deleteQualifier.QualifierId);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
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
        [PacketHandler((int)Request.TypeOneofCase.create_tournament)]
        public async Task CreateTournament(Packet packet, User user)
        {
            var createTournament = packet.Request.create_tournament;

            //TODO: Do permission checks

            var tournament = await StateManager.CreateTournament(createTournament.Tournament, user);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_name)]
        public async Task SetTournamentName(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_name;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.TournamentName = updateTournament.TournamentName;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_image)]
        public async Task SetTournamentImage(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_image;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.TournamentImage = updateTournament.TournamentImage;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_enable_teams)]
        public async Task SetTournamentEnableTeams(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_enable_teams;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.EnableTeams = updateTournament.EnableTeams;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_enable_pools)]
        public async Task SetTournamentEnablePools(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_enable_pools;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.EnablePools = updateTournament.EnablePools;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_show_tournament_button)]
        public async Task SetTournamentShowTournamentButton(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_show_tournament_button;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.ShowTournamentButton = updateTournament.ShowTournamentButton;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_show_qualifier_button)]
        public async Task SetTournamentShowQualifierButton(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_show_qualifier_button;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.ShowQualifierButton = updateTournament.ShowQualifierButton;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_allow_unauthorized_view)]
        public async Task SetTournamentAllowUnauthorizedView(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_allow_unauthorized_view;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.AllowUnauthorizedView = updateTournament.AllowUnauthorizedView;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_score_update_frequency)]
        public async Task SetTournamentScoreUpdateFrequency(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_score_update_frequency;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.ScoreUpdateFrequency = updateTournament.ScoreUpdateFrequency;

                await StateManager.UpdateTournamentSettings(existingTournament);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_banned_mods)]
        public async Task SetTournamentBannedMods(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_banned_mods;

            //TODO: Do permission checks

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
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.add_tournament_team)]
        public async Task AddTournamentTeam(Packet packet, User user)
        {
            var updateTournament = packet.Request.add_tournament_team;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.Teams.Add(updateTournament.Team);

                await StateManager.AddTournamentTeam(existingTournament, updateTournament.Team);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_team_name)]
        public async Task SetTournamentTeamName(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_team_name;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingTeamIndex = existingTournament.Settings.Teams.FindIndex(x => x.Guid == updateTournament.TeamId);
                existingTournament.Settings.Teams[existingTeamIndex].Name = updateTournament.TeamName;

                await StateManager.UpdateTournamentTeam(existingTournament, existingTournament.Settings.Teams[existingTeamIndex]);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_team_image)]
        public async Task SetTournamentTeamImage(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_team_image;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingTeamIndex = existingTournament.Settings.Teams.FindIndex(x => x.Guid == updateTournament.TeamId);
                existingTournament.Settings.Teams[existingTeamIndex].Image = updateTournament.TeamImage;

                await StateManager.UpdateTournamentTeam(existingTournament, existingTournament.Settings.Teams[existingTeamIndex]);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.remove_tournament_team)]
        public async Task RemoveTournamentTeam(Packet packet, User user)
        {
            var updateTournament = packet.Request.remove_tournament_team;

            //TODO: Do permission checks

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
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.add_tournament_pool)]
        public async Task AddTournamentPool(Packet packet, User user)
        {
            var updateTournament = packet.Request.add_tournament_pool;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                existingTournament.Settings.Pools.Add(updateTournament.Pool);

                await StateManager.AddTournamentPool(existingTournament, updateTournament.Pool);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.set_tournament_pool_name)]
        public async Task SetTournamentPoolName(Packet packet, User user)
        {
            var updateTournament = packet.Request.set_tournament_pool_name;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingIndex = existingTournament.Settings.Pools.FindIndex(x => x.Guid == updateTournament.PoolId);
                existingTournament.Settings.Pools[existingIndex].Name = updateTournament.PoolName;

                await StateManager.UpdateTournamentPool(existingTournament, existingTournament.Settings.Pools[existingIndex]);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.add_tournament_pool_maps)]
        public async Task AddTournamentPoolMaps(Packet packet, User user)
        {
            var updateTournament = packet.Request.add_tournament_pool_maps;

            //TODO: Do permission checks

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
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.update_tournament_pool_map)]
        public async Task UpdateTournamentPoolMap(Packet packet, User user)
        {
            var updateTournament = packet.Request.update_tournament_pool_map;

            //TODO: Do permission checks

            var existingTournament = StateManager.GetTournament(updateTournament.TournamentId);
            if (existingTournament != null)
            {
                var existingPool = existingTournament.Settings.Pools.FirstOrDefault(x => x.Guid == updateTournament.PoolId);
                var existingMapIndex = existingPool.Maps.FindIndex(x => x.Guid == updateTournament.Map.Guid);
                existingPool.Maps[existingMapIndex] = updateTournament.Map;

                await StateManager.UpdateTournamentPoolSong(existingTournament, existingPool, updateTournament.Map);

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.remove_tournament_pool_map)]
        public async Task RemoveTournamentPoolMap(Packet packet, User user)
        {
            var updateTournament = packet.Request.remove_tournament_pool_map;

            //TODO: Do permission checks

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
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.remove_tournament_pool)]
        public async Task RemoveTournamentPool(Packet packet, User user)
        {
            var updateTournament = packet.Request.remove_tournament_pool;

            //TODO: Do permission checks

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
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
        [RequirePermission(Permissions.Admin)]
        [PacketHandler((int)Request.TypeOneofCase.delete_tournament)]
        public async Task DeleteTournament(Packet packet, User user)
        {
            var deleteTournament = packet.Request.delete_tournament;

            //TODO: Do permission checks

            var tournament = await StateManager.DeleteTournament(deleteTournament.TournamentId);

            await TAServer.Send(Guid.Parse(user.Guid), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Success,
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
        [PacketHandler((int)Request.TypeOneofCase.add_server)]
        public async Task AddServer(Packet packet, User user)
        {
            var addServer = packet.Request.add_server;

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

                await TAServer.Send(Guid.Parse(user.Guid), new Packet
                {
                    Response = new Response
                    {
                        Type = Response.ResponseType.Success,
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
                        Type = Response.ResponseType.Fail,
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
                        Type = Response.ResponseType.Fail,
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
