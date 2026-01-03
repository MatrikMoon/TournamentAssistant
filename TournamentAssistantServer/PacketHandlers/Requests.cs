using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantDiscordBot.Discord;
using TournamentAssistantServer.ASP.Attributes;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.Database.Models;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantServer.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utilities;
using static TournamentAssistantShared.Constants;
using static TournamentAssistantShared.Permissions;
using Models = TournamentAssistantShared.Models;
using Packets = TournamentAssistantShared.Models.Packets;
using Tournament = TournamentAssistantShared.Models.Tournament;
using User = TournamentAssistantShared.Models.User;

namespace TournamentAssistantServer.PacketHandlers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    [Module(Packet.packetOneofCase.Request, "packet.Request.TypeCase")]
    public class Requests : ControllerBase
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }
        public StateManager StateManager { get; set; }
        public DatabaseService DatabaseService { get; set; }
        public QualifierBot QualifierBot { get; set; }
        public AuthorizationService AuthorizationService { get; set; }

        [AllowFromPlayer]
        [AllowFromWebsocket]
        [AllowFromReadonly]
        [PacketHandler((int)Packets.Request.TypeOneofCase.connect)]
        [HttpPost]
        public ActionResult<Response.Connect> Connect([FromBody] Request.Connect connect, [FromUser] User user)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            var versionCode = user.ClientType == Models.User.ClientTypes.Player ? PLUGIN_VERSION_CODE : WEBSOCKET_VERSION_CODE;
            var versionName = user.ClientType == Models.User.ClientTypes.Player ? PLUGIN_VERSION : WEBSOCKET_VERSION;

            if (user.ClientType != Models.User.ClientTypes.RESTConnection &&
                connect.ClientVersion != versionCode || (connect.UiVersion != 0 && connect.UiVersion != TAUI_VERSION_CODE))
            {
                return BadRequest(new Response.Connect
                {
                    ServerVersion = versionCode,
                    Message = $"Version mismatch, this server expected version {versionName} (TAUI version: {TAUI_VERSION})",
                    Reason = Packets.Response.Connect.ConnectFailReason.IncorrectVersion
                });
            }
            else
            {
                // Give the newly connected player the sanitized state

                // Don't expose tourney info unless the tourney is joined
                var sanitizedState = new State();
                sanitizedState.Tournaments.AddRange(
                    StateManager
                        .GetTournaments()
                        .Where(x => (user.discord_info != null && tournamentDatabase.IsUserAuthorized(x.Guid, user.discord_info.UserId, Permissions.ViewTournamentInList)) || tournamentDatabase.IsUserAuthorized(x.Guid, user.PlatformId, Permissions.ViewTournamentInList))
                        .Select(x =>
                        {
                            // If the user can join the tournament, they can see settings. *shrug* Again, sue me.
                            var userCanSeeSettings = tournamentDatabase.IsUserAuthorized(x.Guid, user.discord_info.UserId, Permissions.JoinTournament) || tournamentDatabase.IsUserAuthorized(x.Guid, user.PlatformId, Permissions.JoinTournament);
                            var tournamentSettings = userCanSeeSettings ? x.Settings : new Tournament.TournamentSettings
                            {
                                TournamentName = x.Settings.TournamentName,
                                TournamentImage = x.Settings.TournamentImage,
                            };

                            // Moon's note 7/4/2025:
                            // The actual code that checks permissions will check if either the discord id or the platform id
                            // has the required permission, so we end up with this
                            // Also, we should probably provide this in Join() too... But for now I'm good <~>
                            tournamentSettings.MyPermissions.Clear();
                            tournamentSettings.MyPermissions.AddRange(
                                tournamentDatabase.GetUserPermissions(x.Guid, user.discord_info?.UserId)
                                    .Concat(tournamentDatabase.GetUserPermissions(x.Guid, user.PlatformId))
                                    .Distinct()
                            );

                            return new Tournament
                            {
                                Guid = x.Guid,
                                Settings = tournamentSettings,
                                Server = x.Server,
                            };
                        }));
                sanitizedState.KnownServers.AddRange(StateManager.GetServers());

                return new Response.Connect
                {
                    State = sanitizedState,
                    ServerVersion = versionCode
                };
            }
        }

        [AllowFromPlayer]
        [AllowFromWebsocket]
        [AllowFromReadonly]
        [RequirePermission(PermissionValues.JoinTournament)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.join)]
        // [HttpPost] Don't think this should be allowed?
        [NonAction]
        public async Task<ActionResult<Response.Join>> Join([FromBody] Request.Join join, [FromUser] User user)
        {
            var tournament = StateManager.GetTournament(join.TournamentId);

            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            if (tournament == null)
            {
                return BadRequest(new Response.Join
                {
                    Message = $"Tournament does not exist!",
                    Reason = Packets.Response.Join.JoinFailReason.IncorrectPassword
                });
            }
            else if (tournamentDatabase.VerifyHashedPassword(tournament.Guid, join.Password))
            {
                await StateManager.AddUser(tournament.Guid, user, join.ModLists.ToArray());

                // Don't expose other tourney info, unless they're part of that tourney too
                var sanitizedState = new State();
                sanitizedState.Tournaments.AddRange(
                    StateManager.GetTournaments()
                        .Where(x => !x.Users.ContainsUser(user))
                        .Where(x => (user.discord_info != null && tournamentDatabase.IsUserAuthorized(x.Guid, user.discord_info.UserId, Permissions.ViewTournamentInList)) || tournamentDatabase.IsUserAuthorized(x.Guid, user.PlatformId, Permissions.ViewTournamentInList))
                        .Select(x => new Tournament
                        {
                            Guid = x.Guid,
                            Settings = x.Settings
                        }));

                // Re-add new tournament, tournaments the user is part of
                sanitizedState.Tournaments.Add(tournament);
                sanitizedState.Tournaments.AddRange(StateManager.GetTournaments().Where(x => StateManager.GetUsers(x.Guid).ContainsUser(user)));
                sanitizedState.KnownServers.AddRange(StateManager.GetServers());

                return new Response.Join
                {
                    SelfGuid = user.Guid,
                    State = sanitizedState,
                    TournamentId = tournament.Guid,
                    Message = $"Connected to {tournament.Settings.TournamentName}!"
                };
            }
            else
            {
                return BadRequest(new Response.Join
                {
                    Message = $"Incorrect password for {tournament.Settings.TournamentName}!",
                    Reason = Packets.Response.Join.JoinFailReason.IncorrectPassword
                });
            }
        }

        [AllowFromPlayer]
        [AllowFromWebsocket]
        [AllowFromReadonly]
        [RequirePermission(PermissionValues.GetQualifierScores)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.qualifier_scores)]
        [HttpPost]
        public ActionResult<Response.LeaderboardEntries> GetQualifierScores([FromBody] Request.QualifierScores scoreRequest, [FromUser] User user)
        {
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            var @event = qualifierDatabase.Qualifiers.FirstOrDefault(x => !x.Old && x.Guid == scoreRequest.EventId);

            IEnumerable<LeaderboardEntry> scores = Enumerable.Empty<LeaderboardEntry>().AsQueryable();

            Func<string, string> getColor = (string platformId) =>
            {
                switch (platformId)
                {
                    case "76561198063268251":
                        return "#0f6927";
                    case "76561198845827102":
                        return "#ac71d9";
                    case "76561198254999022":
                        return "#ff8400";
                    case "76561199097417465":
                        return "#f542e3";
                    case "76561198377216121":
                        return "#09a106";
                    case "76561198183820433":
                        return "#ff69b4";
                    default:
                        return "#ffffff";
                }
            };

            // If a map was specified, return only scores for that map. Otherwise, return all for the event
            if (!string.IsNullOrEmpty(scoreRequest.MapId))
            {
                var song = qualifierDatabase.Songs.FirstOrDefault(x => x.Guid == scoreRequest.MapId && !x.Old);
                if (song != null)
                {
                    scores = qualifierDatabase.Scores
                        .Where(x => x.MapId == scoreRequest.MapId && !x.IsPlaceholder && !x.Old)
                        .OrderByQualifierSettings((QualifierEvent.LeaderboardSort)@event.Sort, song.Target)
                        .Select(x => new LeaderboardEntry
                        {
                            EventId = x.EventId,
                            MapId = x.MapId,
                            PlatformId = x.PlatformId,
                            Username = x.Username,
                            MultipliedScore = x.MultipliedScore,
                            ModifiedScore = x.ModifiedScore,
                            MaxPossibleScore = x.MaxPossibleScore,
                            Accuracy = x.Accuracy,
                            NotesMissed = x.NotesMissed,
                            BadCuts = x.BadCuts,
                            GoodCuts = x.GoodCuts,
                            MaxCombo = x.MaxCombo,
                            FullCombo = x.FullCombo,
                            Color = getColor(x.PlatformId)
                        });
                }
            }

            // Unused for now, as far as I know. But we could reenable it in the future
            // if we figure a nice way to include target scores in here
            // TODO: Implement target scores
            else
            {
                scores = qualifierDatabase.Scores
                    .Where(x => x.EventId == scoreRequest.EventId && !x.IsPlaceholder && !x.Old)
                    .OrderByQualifierSettings((QualifierEvent.LeaderboardSort)@event.Sort)
                    .Select(x => new LeaderboardEntry
                    {
                        EventId = x.EventId,
                        MapId = x.MapId,
                        PlatformId = x.PlatformId,
                        Username = x.Username,
                        MultipliedScore = x.MultipliedScore,
                        ModifiedScore = x.ModifiedScore,
                        MaxPossibleScore = x.MaxPossibleScore,
                        Accuracy = x.Accuracy,
                        NotesMissed = x.NotesMissed,
                        BadCuts = x.BadCuts,
                        GoodCuts = x.GoodCuts,
                        MaxCombo = x.MaxCombo,
                        FullCombo = x.FullCombo,
                        Color = getColor(x.PlatformId)
                    });
            }

            // If scores are disabled for this event, don't return them
            if (((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings.HideScoresFromPlayers) && 
                !tournamentDatabase.IsUserAuthorized(@event.TournamentId, user.discord_info?.UserId ?? user.PlatformId, Permissions.SeeHiddenQualifierScores))
            {
                return new Response.LeaderboardEntries();
            }
            else
            {
                var scoreRequestResponse = new Response.LeaderboardEntries();
                scoreRequestResponse.Scores.AddRange(scores);

                return scoreRequestResponse;
            }
        }

        [AllowFromPlayer]
        [RequirePermission(PermissionValues.SubmitQualifierScores)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.submit_qualifier_score)]
        // [HttpPost] Also probably shouldn't be allowed
        [NonAction]
        public async Task<ActionResult<Response.LeaderboardEntries>> SubmitQualifierScore([FromBody] Request.SubmitQualifierScore submitScoreRequest)
        {
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();

            var @event = qualifierDatabase.Qualifiers.FirstOrDefault(x => !x.Old && x.Guid == submitScoreRequest.QualifierScore.EventId);
            var tournament = StateManager.GetTournament(submitScoreRequest.TournamentId);

            // Check to see if the song exists in the database
            var song = qualifierDatabase.Songs.FirstOrDefault(x => x.Guid == submitScoreRequest.QualifierScore.MapId && !x.Old);
            if (song != null)
            {
                // Returns list of NOT "OLD" scores (usually just the most recent score)
                var scores = qualifierDatabase.Scores.Where(x => x.MapId == submitScoreRequest.QualifierScore.MapId && x.PlatformId == submitScoreRequest.QualifierScore.PlatformId && !x.Old);
                var oldLowScore = scores.OrderByQualifierSettings((QualifierEvent.LeaderboardSort)@event.Sort, song.Target, true).FirstOrDefault(); // "low" score, because it's the lowest non-old score, which would usually be the high score

                // Add new score to the database
                var newScore = new Score
                {
                    MapId = submitScoreRequest.QualifierScore.MapId,
                    EventId = submitScoreRequest.QualifierScore.EventId,
                    PlatformId = submitScoreRequest.QualifierScore.PlatformId,
                    Username = submitScoreRequest.QualifierScore.Username,
                    LevelId = submitScoreRequest.Map.Beatmap.LevelId,
                    MultipliedScore = submitScoreRequest.QualifierScore.MultipliedScore,
                    ModifiedScore = submitScoreRequest.QualifierScore.ModifiedScore,
                    MaxPossibleScore = submitScoreRequest.QualifierScore.MaxPossibleScore,
                    Accuracy = submitScoreRequest.QualifierScore.Accuracy,
                    NotesMissed = submitScoreRequest.QualifierScore.NotesMissed,
                    BadCuts = submitScoreRequest.QualifierScore.BadCuts,
                    GoodCuts = submitScoreRequest.QualifierScore.GoodCuts,
                    MaxCombo = submitScoreRequest.QualifierScore.MaxCombo,
                    FullCombo = submitScoreRequest.QualifierScore.FullCombo,
                    Characteristic = submitScoreRequest.Map.Beatmap.Characteristic.SerializedName,
                    BeatmapDifficulty = submitScoreRequest.Map.Beatmap.Difficulty,
                    GameOptions = (int)submitScoreRequest.Map.GameplayModifiers.Options,
                    PlayerOptions = (int)submitScoreRequest.Map.PlayerSettings.Options,
                    IsPlaceholder = submitScoreRequest.QualifierScore.IsPlaceholder,
                };

                // If the score isn't a placeholder, but the lowest other score is, then we can replace it with our new attempt's result
                if (!submitScoreRequest.QualifierScore.IsPlaceholder && oldLowScore != null && oldLowScore.IsPlaceholder)
                {
                    newScore.ID = oldLowScore.ID;
                    qualifierDatabase.Entry(oldLowScore).CurrentValues.SetValues(newScore);
                }
                else
                {
                    qualifierDatabase.Scores.Add(newScore);
                }

                qualifierDatabase.SaveChanges();

                // Re-query scores so it includes the new one, but not placeholders
                scores = qualifierDatabase.Scores.Where(x => x.MapId == submitScoreRequest.QualifierScore.MapId && x.PlatformId == submitScoreRequest.QualifierScore.PlatformId && !x.IsPlaceholder && !x.Old);
                var highScore = scores.OrderByQualifierSettings((QualifierEvent.LeaderboardSort)@event.Sort, song.Target).FirstOrDefault();

                // Mark all older scores as old
                foreach (var score in scores)
                {
                    score.Old = true;
                }

                // Mark the newer score as new
                if (highScore != null)
                {
                    highScore.Old = false;
                    qualifierDatabase.SaveChanges();
                }

                // --- SCORE REPORTING (Discord bot, reply packet) --- //

                var newScores = qualifierDatabase.Scores
                    .Where(x => x.MapId == submitScoreRequest.QualifierScore.MapId && !x.IsPlaceholder && !x.Old)
                    .OrderByQualifierSettings((QualifierEvent.LeaderboardSort)@event.Sort, song.Target)
                    .Select(x => new LeaderboardEntry
                    {
                        EventId = x.EventId,
                        MapId = x.MapId,
                        PlatformId = x.PlatformId,
                        Username = x.Username,
                        MultipliedScore = x.MultipliedScore,
                        ModifiedScore = x.ModifiedScore,
                        MaxPossibleScore = x.MaxPossibleScore,
                        Accuracy = x.Accuracy,
                        NotesMissed = x.NotesMissed,
                        BadCuts = x.BadCuts,
                        GoodCuts = x.GoodCuts,
                        MaxCombo = x.MaxCombo,
                        FullCombo = x.FullCombo,
                        Color = x.PlatformId == submitScoreRequest.QualifierScore.PlatformId ? "#00ff00" : "#ffffff"
                    });

                // Return the new scores for the song so the leaderboard will update immediately
                // If scores are disabled for this event, don't return them
                var hideScores = ((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings.HideScoresFromPlayers);
                var enableScoreFeed = ((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings.EnableDiscordScoreFeed);
                var enableLeaderboardMessage = ((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings.EnableDiscordLeaderboard);

                // Send a notification of qualifier score submission to all listening web clients
                var websocketClients = StateManager.GetUsers(submitScoreRequest.TournamentId).Where(x => x.ClientType == Models.User.ClientTypes.WebsocketConnection);
                await TAServer.Send(websocketClients.Select(x => Guid.Parse(x.Guid)).ToArray(), new Packet
                {
                    Push = new Push
                    {
                        QualifierScoreSubmtited = new Push.QualifierScoreSubmitted
                        {
                            TournamentId = submitScoreRequest.TournamentId,
                            Event = tournament.Qualifiers.First(x => x.Guid == @event.Guid),
                            Map = submitScoreRequest.Map,
                            QualifierScore = submitScoreRequest.QualifierScore
                        }
                    }
                });

                // if (@event.InfoChannelId != default && !hideScores && QualifierBot != null)
                if ((oldLowScore == null || oldLowScore.IsNewScoreBetter(submitScoreRequest.QualifierScore, (QualifierEvent.LeaderboardSort)@event.Sort, song.Target)) && @event.InfoChannelId != default && QualifierBot != null)
                {
                    if (enableScoreFeed)
                    {
                        QualifierBot.SendScoreEvent(@event.InfoChannelId, submitScoreRequest.Map.Beatmap.Name, submitScoreRequest.QualifierScore);
                    }

                    if (enableLeaderboardMessage)
                    {
                        var newMessageId = await QualifierBot.SendLeaderboardUpdate(@event.InfoChannelId, song.LeaderboardMessageId, song.Guid, song.Name, newScores.ToArray(), tournament.Settings.TournamentName);

                        // In console apps, await might continue on a different thread, so to be sure `song` isn't detached, let's grab a new reference
                        song = qualifierDatabase.Songs.FirstOrDefault(x => x.Guid == submitScoreRequest.QualifierScore.MapId && !x.Old);
                        if (song.LeaderboardMessageId != newMessageId)
                        {
                            System.IO.File.AppendAllText("leaderboardDebug.txt", $"Saving new messageId: old-{song.LeaderboardMessageId} new-{newMessageId} songName-{song.Name}\n");

                            song.LeaderboardMessageId = newMessageId;
                            qualifierDatabase.SaveChanges();
                        }
                    }
                }

                var submitScoreResponse = new Response.LeaderboardEntries();
                submitScoreResponse.Scores.AddRange(hideScores ? new LeaderboardEntry[] { } : newScores.ToArray());

                return submitScoreResponse;
            }

            return new Response.LeaderboardEntries();
        }

        [AllowFromPlayer]
        [RequirePermission(PermissionValues.GetRemainingAttempts)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.remaining_attempts)]
        [HttpPost]
        public ActionResult<Response.RemainingAttempts> GetReminingAttempts([FromBody] Request.RemainingAttempts remainingAttempts, [FromUser] User user)
        {
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();

            var currentAttempts = qualifierDatabase.Scores.Where(x => x.MapId == remainingAttempts.MapId && x.PlatformId == user.PlatformId).Count();
            var totalAttempts = qualifierDatabase.Songs.First(x => x.Guid == remainingAttempts.MapId).Attempts;

            return new Response.RemainingAttempts
            {
                remaining_attempts = totalAttempts - currentAttempts
            };
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.AddAuthorizedUsers)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.add_authorized_user)]
        [HttpPost]
        public ActionResult<Response.AddAuthorizedUser> AddAuthorizedUser([FromBody] Request.AddAuthorizedUser addAuthorizedUser)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            var tournament = StateManager.GetTournament(addAuthorizedUser.TournamentId);

            tournamentDatabase.AddAuthorizedUser(tournament.Guid, addAuthorizedUser.DiscordId, addAuthorizedUser.RoleIds.ToArray());

            var response = new Response.AddAuthorizedUser
            {
                TournamentId = addAuthorizedUser.TournamentId,
                DiscordId = addAuthorizedUser.DiscordId,
            };
            response.Roles.AddRange(addAuthorizedUser.RoleIds);

            return response;
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.UpdateAuthorizedUserRoles)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.update_authorized_user_roles)]
        [HttpPost]
        public ActionResult<Response.UpdateAuthorizedUser> UpdateAuthorizedUserRoles([FromBody] Request.UpdateAuthorizedUserRoles updateAuthorizedUserRoles)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            var tournament = StateManager.GetTournament(updateAuthorizedUserRoles.TournamentId);

            tournamentDatabase.ChangeAuthorizedUserRoles(tournament.Guid, updateAuthorizedUserRoles.DiscordId, updateAuthorizedUserRoles.RoleIds.ToArray());

            var newPermissionFlags = tournamentDatabase.GetUserRoleIds(tournament.Guid, updateAuthorizedUserRoles.DiscordId);

            var response = new Response.UpdateAuthorizedUser
            {
                TournamentId = updateAuthorizedUserRoles.TournamentId,
                DiscordId = updateAuthorizedUserRoles.DiscordId,
            };
            response.Roles.AddRange(newPermissionFlags);

            return response;
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.RemoveAuthorizedUsers)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.remove_authorized_user)]
        [HttpPut]
        public ActionResult<Response.RemoveAuthorizedUser> RemoveAuthorizedUser([FromBody] Request.RemoveAuthorizedUser removeAuthorizedUser)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            var tournament = StateManager.GetTournament(removeAuthorizedUser.TournamentId);

            tournamentDatabase.RemoveAuthorizedUser(tournament.Guid, removeAuthorizedUser.DiscordId);

            return new Response.RemoveAuthorizedUser
            {
                TournamentId = removeAuthorizedUser.TournamentId,
                DiscordId = removeAuthorizedUser.DiscordId,
            };
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.GetAuthorizedUsers)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.get_authorized_users)]
        [HttpPost]
        public async Task<ActionResult<Response.GetAuthorizedUsers>> GetAuthorizedUsers([FromBody] Request.GetAuthorizedUsers getAuthorizedUsers)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            var tournament = StateManager.GetTournament(getAuthorizedUsers.TournamentId);

            var response = new Response.GetAuthorizedUsers
            {
                TournamentId = getAuthorizedUsers.TournamentId,
            };

            // TOOO: Guess we actually need to use the steam api. Oh well. In the meantime...
            var authorizedUsers = tournamentDatabase.AuthorizedUsers
                .Where(x => !x.Old && x.TournamentId == getAuthorizedUsers.TournamentId)
                .ToList();
            if (authorizedUsers.Count > 10)
            {
                response.AuthorizedUsers.AddRange(
                    authorizedUsers
                    .Select(x =>
                    {
                        var response = new Response.GetAuthorizedUsers.AuthroizedUser
                        {
                            DiscordId = x.DiscordId,
                            DiscordUsername = "Hi Jive, you rate limit causing dummy",
                            DiscordAvatarUrl = "https://cdn.discordapp.com/avatars/708801604719214643/d37a1b93a741284ecd6e57569f6cd598.webp?size=100",
                        };
                        response.Roles.AddRange(x.Roles.Split(","));
                        return response;
                    }
                ));
            }
            else
            {
                // We actually fetch pfp and username from discord (or steam) in realtime for this. Heavy, yes, but
                // Discord.NET takes care of caching and avoiding rate limits for us...
                // for discord. We'll have to handle the rate limiting of other services
                // (lookin at you, steam) on our own
                response.AuthorizedUsers.AddRange(await Task.WhenAll(authorizedUsers
                    .Select(async x =>
                    {
                        var discordUserInfo = await AccountLookup.GetAccountInfo(QualifierBot, DatabaseService, x.DiscordId);
                        var response = new Response.GetAuthorizedUsers.AuthroizedUser
                        {
                            DiscordId = x.DiscordId,
                            DiscordUsername = discordUserInfo.Username,
                            DiscordAvatarUrl = discordUserInfo.AvatarUrl,
                        };
                        response.Roles.AddRange(x.Roles.Split(","));
                        return response;
                    }
                )));
            }

            return response;
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.GetDiscordInfo)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.get_discord_info)]
        [HttpPost]
        public async Task<ActionResult<Response.GetDiscordInfo>> GetDiscordInfo([FromBody] Request.GetDiscordInfo getDiscordInfo)
        {
            var discordUserInfo = await AccountLookup.GetAccountInfo(QualifierBot, DatabaseService, getDiscordInfo.DiscordId);

            return new Response.GetDiscordInfo
            {
                DiscordId = discordUserInfo.UserId,
                DiscordUsername = discordUserInfo.Username,
                DiscordAvatarUrl = discordUserInfo.AvatarUrl
            };
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Packets.Request.TypeOneofCase.get_bot_tokens_for_user)]
        [HttpPost]
        public async Task<ActionResult<Response.GetBotTokensForUser>> GetBotTokensForUser([FromBody] Request.GetBotTokensForUser getBotTokensForUser)
        {
            var botTokens = await QualifierBot.GetAccountInfo(getBotTokensForUser.OwnerDiscordId);

            using var userDatabase = DatabaseService.NewUserDatabaseContext();

            var response = new Response.GetBotTokensForUser();
            response.BotUsers.AddRange(userDatabase.GetTokensByOwner(getBotTokensForUser.OwnerDiscordId).Select(x =>
            {
                return new Response.GetBotTokensForUser.BotUser
                {
                    Guid = x.Guid,
                    Username = x.Name,
                    OwnerDiscordId = x.OwnerDiscordId,
                };
            }));

            return response;
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Packets.Request.TypeOneofCase.generate_bot_token)]
        [HttpPost]
        public ActionResult<Response.GenerateBotToken> GenerateBotToken([FromBody] Request.GenerateBotToken generateBotToken)
        {
            using var userDatabase = DatabaseService.NewUserDatabaseContext();

            var newUserGuid = Guid.NewGuid().ToString();
            var user = new User
            {
                Guid = newUserGuid,
                discord_info = new User.DiscordInfo
                {
                    UserId = newUserGuid,
                    Username = generateBotToken.Username,
                },
            };

            var newToken = AuthorizationService.GenerateWebsocketToken(user, true);

            userDatabase.AddUser(newToken, user.discord_info.Username, newUserGuid, ExecutionContext.User.discord_info.UserId);

            return new Response.GenerateBotToken
            {
                BotToken = newToken,
            };
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Packets.Request.TypeOneofCase.revoke_bot_token)]
        [HttpPost]
        public ActionResult<Response.RevokeBotToken> RevokeBotToken([FromBody] Request.RevokeBotToken revokeBotToken)
        {
            using var userDatabase = DatabaseService.NewUserDatabaseContext();

            var existingToken = userDatabase.GetUser(revokeBotToken.BotTokenGuid);

            if (existingToken.OwnerDiscordId == ExecutionContext.User.discord_info.UserId || ExecutionContext.User.discord_info.UserId == "229408465787944970")
            {
                userDatabase.RevokeUser(revokeBotToken.BotTokenGuid);

                return new Response.RevokeBotToken();
            }
            else
            {
                return BadRequest(new Response.RevokeBotToken
                {
                    Message = "Cannot remove token owned by other user"
                });
            }
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.RefundAttempts)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.refund_attempts)]
        [HttpPost]
        public ActionResult<Response.RefundAttempts> RefundAttempts([FromBody] Request.RefundAttempts refundAttempts)
        {
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();

            var currentAttempts = qualifierDatabase.Scores.Where(x => x.MapId == refundAttempts.MapId && x.PlatformId == refundAttempts.PlatformId).Count();
            var totalAttempts = qualifierDatabase.Songs.First(x => x.Guid == refundAttempts.MapId).Attempts;
            var @event = qualifierDatabase.Qualifiers.FirstOrDefault(x => !x.Old && x.Guid == refundAttempts.EventId);
            var song = qualifierDatabase.Songs.FirstOrDefault(x => (x.Guid == refundAttempts.MapId || x.LevelId == refundAttempts.MapId) && !x.Old);

            if (currentAttempts == 0)
            {
                return BadRequest(new Response.RefundAttempts
                {
                    Message = "The user did not have any attempts on this map"
                });
            }

            if (totalAttempts == 0)
            {
                return BadRequest(new Response.RefundAttempts
                {
                    Message = "This map does not have limited attempts enabled"
                });
            }

            var scores = qualifierDatabase.Scores.Where(x => x.MapId == refundAttempts.MapId && x.PlatformId == refundAttempts.PlatformId);
            var scoresToRemove = scores.OrderByQualifierSettings((QualifierEvent.LeaderboardSort)@event.Sort, song.Target).TakeLast(Math.Min(scores.Count(), refundAttempts.Count));

            // Note: this is the only time scores are ever deleted
            qualifierDatabase.Scores.RemoveRange(scoresToRemove);

            qualifierDatabase.SaveChanges();

            return new Response.RefundAttempts
            {
                Message = $"Successfully refunded {scoresToRemove.Count()} attempts!"
            };
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.PlayWithStreamSync)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.preload_image_for_stream_sync)]
        [HttpPost]
        public async Task PreloadImageForStreamSync([FromBody] Request.PreloadImageForStreamSync preloadImageForStreamSync, [FromUser] User user)
        {
            await TAServer.ForwardTo(preloadImageForStreamSync.ForwardToes.Select(Guid.Parse).ToArray(), Guid.Parse(user.Guid), new Packet
            {
                Id = ExecutionContext.Packet?.Id, // Packet may be null for REST requests. This shouldn't necessarily be the case, but can be fixed in the future
                Request = new Request
                {
                    preload_image_for_stream_sync = preloadImageForStreamSync
                }
            });
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.LoadSong)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.load_song)]
        [HttpPost]
        public async Task LoadSong([FromBody] Request.LoadSong loadSong, [FromUser] User user)
        {
            await TAServer.ForwardTo(loadSong.ForwardToes.Select(Guid.Parse).ToArray(), Guid.Parse(user.Guid), new Packet
            {
                Id = ExecutionContext.Packet?.Id, // Packet may be null for REST requests. This shouldn't necessarily be the case, but can be fixed in the future
                Request = new Request
                {
                    load_song = loadSong
                }
            });
        }

        // This one's just for ASP.NET. Conversion of a websocket token to a REST token
        [AllowUnauthorized]
        [HttpPost]
        public string ConvertWebsocketTokenToRest([FromQuery] string websocketToken)
        {
            var validUser = AuthorizationService.VerifyUser(websocketToken, null, out var user, true);
            if (!validUser || user.ClientType != Models.User.ClientTypes.WebsocketConnection)
            {
                throw new ArgumentException();
            }
            return AuthorizationService.GenerateRestToken(user);
        }
    }
}
