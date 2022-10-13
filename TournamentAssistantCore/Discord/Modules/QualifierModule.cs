#pragma warning disable 1998
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore.Internal;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantCore.Discord.Helpers;
using TournamentAssistantCore.Discord.Services;
using TournamentAssistantShared;
using TournamentAssistantShared.BeatSaver;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.Models.GameplayModifiers;
using static TournamentAssistantShared.Models.PlayerSpecificSettings;
using static TournamentAssistantShared.Constants;

namespace TournamentAssistantCore.Discord.Modules
{
    public class QualifierModule : InteractionModuleBase
    {
        private static Random random = new();

        public DatabaseService DatabaseService { get; set; }
        public ScoresaberService ScoresaberService { get; set; }
        public SystemServerService ServerService { get; set; }

        private GameplayParameters FindSong(List<GameplayParameters> songPool, string levelId, string characteristic, int beatmapDifficulty, int gameOptions, int playerOptions)
        {
            return songPool.FirstOrDefault(x => x.Beatmap.LevelId == levelId && x.Beatmap.Characteristic.SerializedName == characteristic && x.Beatmap.Difficulty == beatmapDifficulty && x.GameplayModifiers.Options == (GameOptions)gameOptions && x.PlayerSettings.Options == (PlayerOptions)playerOptions);
        }

        private List<GameplayParameters> RemoveSong(List<GameplayParameters> songPool, string levelId, string characteristic, int beatmapDifficulty, int gameOptions, int playerOptions)
        {
            songPool.RemoveAll(x => x.Beatmap.LevelId == levelId && x.Beatmap.Characteristic.SerializedName == characteristic && x.Beatmap.Difficulty == beatmapDifficulty && x.GameplayModifiers.Options == (GameOptions)gameOptions && x.PlayerSettings.Options == (PlayerOptions)playerOptions);
            return songPool;
        }

        private string SanitizeSongId(string songId)
        {
            if (songId.StartsWith("https://beatsaver.com/") || songId.StartsWith("https://bsaber.com/"))
            {
                //Strip off the trailing slash if there is one
                if (songId.EndsWith("/")) songId = songId[..^1];

                //Strip off the beginning of the url to leave the id
                songId = songId[(songId.LastIndexOf("/", StringComparison.Ordinal) + 1)..];
            }

            if (songId.Contains('&'))
            {
                songId = songId[..songId.IndexOf("&", StringComparison.Ordinal)];
            }

            return songId;
        }

        private bool SongExists(List<GameplayParameters> songPool, string levelId, string characteristic, int beatmapDifficulty, int gameOptions, int playerOptions)
        {
            return FindSong(songPool, levelId, characteristic, beatmapDifficulty, gameOptions, playerOptions) != null;
        }

        [SlashCommand("create-event", "Create a Qualifier event for your guild (use /list-hosts to see available hosts)")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task CreateEventAsync(string name, string hostAddress, ITextChannel infoChannel = null, string settings = null)
        {
            var server = ServerService.GetServer();
            if (server == null)
            {
                await RespondAsync(embed: "The Server is not running, so we can't can't add events to it".ErrorEmbed());
            }
            else
            {
                var eventSettings = Enum.GetValues(typeof(QualifierEvent.EventSettings)).Cast<QualifierEvent.EventSettings>()
                    .Where(o => !string.IsNullOrWhiteSpace(settings.ParseArgs(o.ToString())))
                    .Aggregate(QualifierEvent.EventSettings.None, (current, o) => current | o);

                var host = server.State.KnownHosts.FirstOrDefault(x => $"{x.Address}:{x.Port}" == hostAddress);

                var response = await server.SendCreateQualifierEvent(host, DatabaseService.DatabaseContext.ConvertDatabaseToModel(null, new Database.Event
                {
                    EventId = Guid.NewGuid().ToString(),
                    GuildId = Context.Guild.Id,
                    GuildName = Context.Guild.Name,
                    Name = name,
                    InfoChannelId = ulong.Parse(infoChannel?.Id.ToString() ?? "0"),
                    Flags = (int)eventSettings
                }));

                switch (response.Type)
                {
                    case Response.ResponseType.Success:
                        await RespondAsync(embed: response.modify_qualifier.Message.SuccessEmbed());
                        break;
                    case Response.ResponseType.Fail:
                        await RespondAsync(embed: response.modify_qualifier.Message.ErrorEmbed());
                        break;
                    default:
                        await RespondAsync(embed: "An unknown error occurred".ErrorEmbed());
                        break;
                }
            }
        }

        [SlashCommand("set-score-channel", "Sets a score channel for the ongoing event")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetScoreChannelAsync(ITextChannel channel, string eventId)
        {
            var server = ServerService.GetServer();
            if (server == null)
            {
                await RespondAsync(embed: "The Server is not running, so we can't can't add events to it".ErrorEmbed());
            }
            else
            {
                var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts.ToArray(), $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0);
                var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.Guid.ToString() == eventId));
                if (targetPair.Key == null)
                {
                    await RespondAsync(embed: "Could not find an event with that ID".ErrorEmbed());
                    return;
                }

                var targetEvent = targetPair.Value.Events.First(x => x.Guid.ToString() == eventId);
                targetEvent.InfoChannel = new TournamentAssistantShared.Models.Discord.Channel
                {
                    Id = (int)(channel?.Id ?? 0),
                    Name = channel?.Name ?? ""
                };

                var response = await server.SendUpdateQualifierEvent(targetPair.Key, targetEvent);
                switch (response.Type)
                {
                    case Response.ResponseType.Success:
                        await RespondAsync(embed: response.modify_qualifier.Message.SuccessEmbed());
                        break;
                    case Response.ResponseType.Fail:
                        await RespondAsync(embed: response.modify_qualifier.Message.ErrorEmbed());
                        break;
                    default:
                        await RespondAsync(embed: "An unknown error occurred".ErrorEmbed());
                        break;
                }
            }
        }

        [SlashCommand("list-options", "Lists all available options for adding a song")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task ListOptionsAsync()
        {
            var gameOptions = Enum.GetValues(typeof(GameOptions)).Cast<object>().Select(option => $"`{option}`").ToArray();
            var playerOptions = Enum.GetValues(typeof(PlayerOptions)).Cast<object>().Select(option => $"`{option}`").ToArray();
            var eventOptions = Enum.GetValues(typeof(QualifierEvent.EventSettings)).Cast<object>().Select(option => $"`{option}`").ToArray();

            await RespondAsync(embed: $"Available game options: {string.Join(", ", gameOptions)}\n\nAvailable player options: {string.Join(", ", playerOptions)}\n\nAvailable event settings: {string.Join(", ", eventOptions)}".InfoEmbed());
        }

        [SlashCommand("add-song", "Add a song to the currently running event (use /list-options to see available options)")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task AddSongAsync(string eventId, string songId, BeatmapDifficulty difficulty, string characteristic = "Standard", string gameOptionsString = null, string playerOptionsString = null)
        {
            //Load up the GameOptions and PlayerOptions
            var gameOptions = Enum.GetValues(typeof(GameOptions)).Cast<GameOptions>()
                .Where(o => !string.IsNullOrWhiteSpace(gameOptionsString.ParseArgs(o.ToString())))
                .Aggregate(GameOptions.None, (current, o) => current | o);

            var playerOptions = Enum.GetValues(typeof(PlayerOptions)).Cast<PlayerOptions>()
                .Where(o => !string.IsNullOrWhiteSpace(playerOptionsString.ParseArgs(o.ToString())))
                .Aggregate(PlayerOptions.None, (current, o) => current | o);

            //Sanitize input
            songId = SanitizeSongId(songId);

            var server = ServerService.GetServer();
            if (server == null)
            {
                await RespondAsync(embed: "The Server is not running, so we can't can't add songs to it".ErrorEmbed());
            }
            else
            {
                //Get the hash for the song
                var hash = BeatSaverDownloader.GetHashFromID(songId);
                if (hash == null)
                {
                    await RespondAsync(embed: "Could not find a BeatSaver map with that ID".ErrorEmbed());
                    return;
                }

                var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts.ToArray(), $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0);
                var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.Guid.ToString() == eventId));
                if (targetPair.Key == null)
                {
                    await RespondAsync(embed: "Could not find an event with that ID".ErrorEmbed());
                    return;
                }

                var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.Guid.ToString() == eventId);

                var songPool = targetEvent.QualifierMaps.ToList();

                GameplayParameters parameters = new()
                {
                    GameplayModifiers = new GameplayModifiers
                    {
                        Options = gameOptions
                    },
                    PlayerSettings = new PlayerSpecificSettings
                    {
                        Options = playerOptions
                    }
                };

                int responseType;
                bool exists;
                string songName;

                if (OstHelper.IsOst(hash))
                {
                    exists = SongExists(songPool, hash, characteristic, (int)difficulty, (int)gameOptions, (int)playerOptions);
                    songName = OstHelper.GetOstSongNameFromLevelId(hash);
                    parameters.Beatmap = new Beatmap
                    {
                        Name = songName,
                        LevelId = hash,
                        Characteristic = new Characteristic
                        {
                            SerializedName = characteristic
                        },
                        Difficulty = (int)difficulty
                    };
                    responseType = 0;
                }
                else
                {
                    var songInfo = await BeatSaverDownloader.GetSongInfo(songId);
                    songName = songInfo.name;

                    if (!songInfo.HasDifficulty(characteristic, difficulty))
                    {
                        difficulty = songInfo.GetClosestDifficultyPreferLower(characteristic, difficulty);
                        responseType = 1;
                    }
                    else
                    {
                        responseType = 2;
                    }

                    exists = SongExists(songPool, $"custom_level_{hash.ToUpper()}", characteristic, (int)difficulty, (int)gameOptions, (int)playerOptions);

                    parameters.Beatmap = new Beatmap
                    {
                        Name = songName,
                        LevelId = $"custom_level_{hash.ToUpper()}",
                        Characteristic = new Characteristic
                        {
                            SerializedName = characteristic
                        },
                        Difficulty = (int)difficulty
                    };
                }

                if (exists)
                {
                    if (responseType == 1)
                        await RespondAsync(embed: $"{songName} doesn't have that difficulty, and {difficulty} is already in the event".ErrorEmbed());
                    else
                        await RespondAsync(embed: "Song has already been added to the list".ErrorEmbed());
                    return;
                }

                songPool.Add(parameters);
                targetEvent.QualifierMaps.Clear();
                targetEvent.QualifierMaps.AddRange(songPool);

                var response = await server.SendUpdateQualifierEvent(targetPair.Key, targetEvent);
                switch (response.Type)
                {
                    case Response.ResponseType.Success:
                        var replyString = responseType switch
                        {
                            0 => $"Added: {parameters.Beatmap.Name} ({difficulty}) ({characteristic})",
                            1 => $"{songName} doesn't have that difficulty, using {difficulty} instead.\nAdded to the song list",
                            2 => $"{songName} ({difficulty}) ({characteristic}) downloaded and added to song list",
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        await RespondAsync(embed: (replyString +
                                                 $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                                 $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}").SuccessEmbed());
                        break;
                    case Response.ResponseType.Fail:
                        await RespondAsync(embed: response.modify_qualifier.Message.ErrorEmbed());
                        break;
                    default:
                        await RespondAsync(embed: "An unknown error occurred".ErrorEmbed());
                        break;
                }
            }
        }

        [SlashCommand("list-songs", "List the currently active songs for the current event")]
        [RequireContext(ContextType.Guild)]
        public async Task ListSongsAsync(string eventId)
        {
            var server = ServerService.GetServer();
            if (server == null)
            {
                await RespondAsync(embed: "The Server is not running, so we can't can't get any event info".ErrorEmbed());
            }
            else
            {
                var builder = new EmbedBuilder
                {
                    Title = "<:page_with_curl:735592941338361897> Song List",
                    Color = new Color(random.Next(255), random.Next(255), random.Next(255)),
                    Description = "Loading songs..."
                };
                await RespondAsync(embed: builder.Build(), ephemeral: true);
                builder.Description = null;

                var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts.ToArray(), $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0);
                var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.Guid.ToString() == eventId));
                if (targetPair.Key == null)
                {
                    await RespondAsync(embed: "Could not find an event with that ID".ErrorEmbed());
                    return;
                }

                var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.Guid.ToString() == eventId);
                var songPool = targetEvent.QualifierMaps.ToList();

                var titleField = new EmbedFieldBuilder
                {
                    Name = "Title",
                    Value = "```",
                    IsInline = true
                };

                var difficultyField = new EmbedFieldBuilder
                {
                    Name = "Difficulty",
                    Value = "```",
                    IsInline = true
                };

                var modifierField = new EmbedFieldBuilder
                {
                    Name = "Modifiers",
                    Value = "```",
                    IsInline = true
                };

                foreach (var song in songPool)
                {
                    titleField.Value += $"\n{song.Beatmap.Name}";
                    difficultyField.Value += $"\n{(BeatmapDifficulty)song.Beatmap.Difficulty}";
                    modifierField.Value += $"\n{song.GameplayModifiers.Options}";
                }

                titleField.Value += "```";
                difficultyField.Value += "```";
                modifierField.Value += "```";

                builder.AddField(titleField);
                builder.AddField(difficultyField);
                builder.AddField(modifierField);

                await ModifyOriginalResponseAsync(x => x.Embed = builder.Build());
            }
        }

        [SlashCommand("remove-song", "Remove a song from the currently running event")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task RemoveSongAsync(string eventId, string songId, BeatmapDifficulty difficulty, string characteristic = "Standard", string gameOptionsString = null, string playerOptionsString = null)
        {
            var server = ServerService.GetServer();
            if (server == null)
            {
                await RespondAsync(embed: "The Server is not running, so we can't can't get any event info".ErrorEmbed());
            }
            else
            {
                var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts.ToArray(), $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0);
                var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.Guid.ToString() == eventId));
                if (targetPair.Key == null)
                {
                    await RespondAsync(embed: "Could not find an event with that ID".ErrorEmbed());
                    return;
                }

                var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.Guid.ToString() == eventId);
                var songPool = targetEvent.QualifierMaps.ToList();

                var gameOptions = Enum.GetValues(typeof(GameOptions)).Cast<GameOptions>()
                    .Where(o => !string.IsNullOrWhiteSpace(gameOptionsString.ParseArgs(o.ToString())))
                    .Aggregate(GameOptions.None, (current, o) => current | o);

                var playerOptions = Enum.GetValues(typeof(PlayerOptions)).Cast<PlayerOptions>()
                    .Where(o => !string.IsNullOrWhiteSpace(playerOptionsString.ParseArgs(o.ToString())))
                    .Aggregate(PlayerOptions.None, (current, o) => current | o);

                //Sanitize input
                songId = SanitizeSongId(songId);

                //Get the hash for the song
                var hash = BeatSaverDownloader.GetHashFromID(songId);
                if (hash == null)
                {
                    await RespondAsync(embed: "Could not find a BeatSaver map with that ID".ErrorEmbed());
                    return;
                }

                var levelId = OstHelper.IsOst(hash) ? hash : $"custom_level_{hash.ToUpper()}";

                var song = FindSong(songPool, levelId, characteristic, (int)difficulty, (int)gameOptions, (int)playerOptions);
                if (song != null)
                {
                    targetEvent.QualifierMaps.Clear();
                    targetEvent.QualifierMaps.AddRange(RemoveSong(songPool, levelId, characteristic, (int)difficulty, (int)gameOptions, (int)playerOptions).ToArray());

                    var response = await server.SendUpdateQualifierEvent(targetPair.Key, targetEvent);
                    switch (response.Type)
                    {
                        case Response.ResponseType.Success:
                            await RespondAsync(embed: ($"Removed {song.Beatmap.Name} ({difficulty}) ({characteristic}) from the song list" +
                                                     $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                                     $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}").SuccessEmbed());
                            break;
                        case Response.ResponseType.Fail:
                            await RespondAsync(embed: response.modify_qualifier.Message.ErrorEmbed());
                            break;
                        default:
                            await RespondAsync(embed: "An unknown error occurred".ErrorEmbed());
                            break;
                    }
                }
                else await RespondAsync(embed: $"Specified song does not exist with that difficulty / characteristic / gameOptions / playerOptions ({difficulty} {characteristic} {gameOptions} {playerOptions})".ErrorEmbed());
            }

        }

        [SlashCommand("end-event", "End the current event")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task EndEventAsync(string eventId)
        {
            //Make server backup
            /*Logger.Warning($"BACKING UP DATABASE...");
            File.Copy("BotDatabase.db", $"EventDatabase_bak_{DateTime.Now.Day}_{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}.db");
            Logger.Success("Database backed up succsessfully.");*/

            var server = ServerService.GetServer();
            if (server == null)
            {
                await RespondAsync(embed: "The Server is not running, so we can't can't get any event info".ErrorEmbed());
            }
            else
            {
                var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts.ToArray(), $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0);
                var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.Guid.ToString() == eventId));
                if (targetPair.Key == null)
                {
                    await RespondAsync(embed: "Could not find an event with that ID".ErrorEmbed());
                    return;
                }

                var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.Guid.ToString() == eventId);

                var response = await server.SendDeleteQualifierEvent(targetPair.Key, targetEvent);
                switch (response.Type)
                {
                    case Response.ResponseType.Success:
                        await RespondAsync(embed: response.modify_qualifier.Message.SuccessEmbed());
                        break;
                    case Response.ResponseType.Fail:
                        await RespondAsync(embed: response.modify_qualifier.Message.ErrorEmbed());
                        break;
                    default:
                        await RespondAsync(embed: "An unknown error occurred".ErrorEmbed());
                        break;
                }
            }
        }

        [SlashCommand("list-events", "Show all events we can find info about")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task ListEventsAsync()
        {
            var server = ServerService.GetServer();
            if (server == null)
            {
                await RespondAsync(embed: "The Server is not running, so we can't can't get any event info".ErrorEmbed());
            }
            else
            {
                var builder = new EmbedBuilder
                {
                    Title = "<:page_with_curl:735592941338361897> Events",
                    Color = new Color(random.Next(255), random.Next(255), random.Next(255)),
                    Description = "Fetching events..."
                };
                await RespondAsync(embed: builder.Build(), ephemeral: true);
                builder.Description = null;

                var knownEvents = (await HostScraper.ScrapeHosts(server.State.KnownHosts.ToArray(), $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0)).Select(x => x.Value).Where(x => x.Events != null).SelectMany(x => x.Events);
                foreach (var @event in knownEvents)
                {
                    builder.AddField(@event.Name, $"```fix\n{@event.Guid}```\n" +
                        $"```css\n({@event.Guild.Name})```", true);
                }

                await ModifyOriginalResponseAsync(x => x.Embed = builder.Build());
            }
        }

        [SlashCommand("list-hosts", "Show all hosts we can find info about")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task ListHostsAsync()
        {
            var server = ServerService.GetServer();
            if (server == null)
            {
                await RespondAsync(embed: "The Server is not running, so we can't can't get any host info".ErrorEmbed());
            }
            else
            {
                var builder = new EmbedBuilder
                {
                    Title = "<:page_with_curl:735592941338361897> Hosts",
                    Color = new Color(random.Next(255), random.Next(255), random.Next(255))
                };

                foreach (var host in server.State.KnownHosts)
                {
                    builder.AddField(host.Name, $"```\n{host.Address}:{host.Port}```", true);
                }

                await RespondAsync(embed: builder.Build());
            }
        }

        [SlashCommand("dumb-leaderboards", "Show leaderboards from the currently running event, unformatted to allow for larger messages")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task DumbLeaderboardsAsync(string eventId)
        {
            var server = ServerService.GetServer();
            if (server == null)
            {
                await RespondAsync(embed: "The Server is not running, so we can't can't get any host info".ErrorEmbed());
            }
            else
            {
                var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts.ToArray(), $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0);
                var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.Guid.ToString() == eventId));
                if (targetPair.Key == null)
                {
                    await RespondAsync(embed: "Could not find an event with that ID".ErrorEmbed());
                    return;
                }

                var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.Guid.ToString() == eventId);

                var playerNames = new List<string>();
                var playerScores = new List<string>();

                var leaderboardText = string.Empty;
                foreach (var map in targetEvent.QualifierMaps)
                {
                    var scores = (await HostScraper.RequestResponse(targetPair.Key, new Packet
                    {
                        Request = new Request
                        {
                            leaderboard_score = new Request.LeaderboardScore
                            {
                                EventId = eventId,
                                Parameters = map
                            }
                        }
                    },
                    $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0)).Response.leaderboard_scores;

                    leaderboardText += $"{map.Beatmap.Name}:\n```{string.Join("\n", scores.Scores.Select(x => $"{x.Username} {x.Score} {(x.FullCombo ? "FC" : "")}\n"))}```";
                }

                await RespondAsync(string.IsNullOrWhiteSpace(leaderboardText) ? "No scores yet" : leaderboardText);
            }
        }

        [SlashCommand("excel-leaderboards", "Show leaderboards from the currently running event, exported to excel")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task ExcelLeaderboardsAsync(string eventId)
        {
            var server = ServerService.GetServer();
            if (server == null)
            {
                await RespondAsync(embed: "The Server is not running, so we can't can't get any host info".ErrorEmbed());
            }
            else
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                var excel = new ExcelPackage();

                var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts.ToArray(), $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0);
                var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.Guid.ToString() == eventId));
                if (targetPair.Key == null)
                {
                    await RespondAsync(embed: "Could not find an event with that ID".ErrorEmbed());
                    return;
                }

                var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.Guid.ToString() == eventId);

                foreach (var map in targetEvent.QualifierMaps)
                {
                    var workSheet = excel.Workbook.Worksheets.Add(map.Beatmap.Name);
                    var scores = (await HostScraper.RequestResponse(targetPair.Key, new Packet
                    {
                        Request = new Request
                        {
                            leaderboard_score = new Request.LeaderboardScore
                            {
                                EventId = eventId,
                                Parameters = map
                            }
                        }
                    },
                    $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0)).Response.leaderboard_scores;

                    var row = 0;
                    foreach (var score in scores.Scores)
                    {
                        row++;
                        workSheet.SetValue(row, 1, score.Score);
                        workSheet.SetValue(row, 2, score.Username);
                        workSheet.SetValue(row, 3, score.FullCombo ? "FC" : "");
                    }
                }

                await Context.Channel.SendFileAsync(new MemoryStream(excel.GetAsByteArray()), "Leaderboards.xlsx");
            }
        }

        [SlashCommand("leaderboards", "Show leaderboards from the currently running event")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task LeaderboardsAsync(string eventId)
        {
            var server = ServerService.GetServer();
            if (server == null)
            {
                await RespondAsync(embed: "The Server is not running, so we can't can't get any host info".ErrorEmbed());
            }
            else
            {
                var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts.ToArray(), $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0);
                var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.Guid.ToString() == eventId));
                if (targetPair.Key == null)
                {
                    await RespondAsync(embed: "Could not find an event with that ID".ErrorEmbed());
                    return;
                }

                var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.Guid.ToString() == eventId);

                var builder = new EmbedBuilder
                {
                    Title = "<:page_with_curl:735592941338361897> Leaderboards",
                    Color = new Color(random.Next(255), random.Next(255), random.Next(255))
                };

                var playerNames = new List<string>();
                var playerScores = new List<string>();

                foreach (var map in targetEvent.QualifierMaps)
                {
                    var scores = (await HostScraper.RequestResponse(targetPair.Key, new Packet
                    {
                        Request = new Request
                        {
                            leaderboard_score = new Request.LeaderboardScore
                            {
                                EventId = eventId,
                                Parameters = map
                            }
                        }
                    },
                    $"{server.ServerSelf.Address}:{server.ServerSelf.Port}", 0)).Response.leaderboard_scores;

                    builder.AddField(map.Beatmap.Name, $"```\n{string.Join("\n", scores.Scores.Select(x => $"{x.Username} {x.Score} {(x.FullCombo ? "FC" : "")}\n"))}```", true);
                }

                await RespondAsync(embed: builder.Build());
            }
        }
    }
}
