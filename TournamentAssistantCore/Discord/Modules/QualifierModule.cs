#pragma warning disable 1998
using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
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
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantCore.Discord.Modules
{
    public class QualifierModule : ModuleBase<SocketCommandContext>
    {
        private static Random random = new Random();

        public DatabaseService DatabaseService { get; set; }
        public ScoresaberService ScoresaberService { get; set; }
        public SystemServerService ServerService { get; set; }

        private bool IsAdmin()
        {
            return ((IGuildUser)Context.User).GuildPermissions.Has(GuildPermission.Administrator) ||
                ((IGuildUser)Context.User).GuildPermissions.Has(GuildPermission.ManageChannels);
        }

        private GameplayParameters FindSong(List<GameplayParameters> songPool, string levelId, string characteristic, int beatmapDifficulty, int gameOptions, int playerOptions)
        {
            return songPool.FirstOrDefault(x => x.Beatmap.LevelId == levelId && x.Beatmap.Characteristic.SerializedName == characteristic && x.Beatmap.Difficulty == (BeatmapDifficulty)beatmapDifficulty && x.GameplayModifiers.Options == (GameOptions)gameOptions && x.PlayerSettings.Options == (PlayerOptions)playerOptions);
        }

        private List<GameplayParameters> RemoveSong(List<GameplayParameters> songPool, string levelId, string characteristic, int beatmapDifficulty, int gameOptions, int playerOptions)
        {
            songPool.RemoveAll(x => x.Beatmap.LevelId == levelId && x.Beatmap.Characteristic.SerializedName == characteristic && x.Beatmap.Difficulty == (BeatmapDifficulty)beatmapDifficulty && x.GameplayModifiers.Options == (GameOptions)gameOptions && x.PlayerSettings.Options == (PlayerOptions)playerOptions);
            return songPool;
        }

        private bool SongExists(List<GameplayParameters> songPool, string levelId, string characteristic, int beatmapDifficulty, int gameOptions, int playerOptions)
        {
            return FindSong(songPool, levelId, characteristic, beatmapDifficulty, gameOptions, playerOptions) != null;
        }

        [Command("createEvent")]
        [Summary("Create a Qualifier event for your guild")]
        [RequireContext(ContextType.Guild)]
        public async Task CreateEventAsync([Remainder] string paramString)
        {
            if (IsAdmin())
            {
                var name = paramString.ParseArgs("name");
                var hostServer = paramString.ParseArgs("host");
                var notificationChannelId = paramString.ParseArgs("infoChannelId");
                
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(hostServer))
                {
                    await ReplyAsync(embed: "Usage: `createEvent -name \"Event Name\" -host \"[host address]:[port]\"`\nTo find available hosts, please run `listHosts`\nYou can also set your desired event settings here. For example, add `-hidescorefromplayers` to the command!".ErrorEmbed());
                }
                else
                {
                    var server = ServerService.GetServer();
                    if (server == null)
                    {
                        await ReplyAsync(embed: "The Server is not running, so we can't can't add events to it".ErrorEmbed());
                    }
                    else
                    {
                        var host = server.State.KnownHosts.FirstOrDefault(x => $"{x.Address}:{x.Port}" == hostServer);

                        QualifierEvent.EventSettings settings = QualifierEvent.EventSettings.None;

                        //Parse any desired options
                        foreach (QualifierEvent.EventSettings o in Enum.GetValues(typeof(QualifierEvent.EventSettings)))
                        {
                            if (paramString.ParseArgs(o.ToString()) == "true") settings = (settings | o);
                        }

                        var response = await server.SendCreateQualifierEvent(host, DatabaseService.DatabaseContext.ConvertDatabaseToModel(null, new Database.Event
                        {
                            EventId = Guid.NewGuid().ToString(),
                            GuildId = Context.Guild.Id,
                            GuildName = Context.Guild.Name,
                            Name = name,
                            InfoChannelId = ulong.Parse(notificationChannelId ?? "0"),
                            Flags = (int)settings
                        }));
                        if (response.Type == Response.ResponseType.Success)
                        {
                            await ReplyAsync(embed: response.Message.SuccessEmbed());
                        }
                        else if (response.Type == Response.ResponseType.Fail)
                        {
                            await ReplyAsync(embed: response.Message.ErrorEmbed());
                        }
                    }
                }
            }
            else await ReplyAsync(embed: "You do not have sufficient permissions to use this command".ErrorEmbed());
        }

        [Command("setScoreChannel")]
        [Summary("Sets a score channel for the ongoing event")]
        [RequireContext(ContextType.Guild)]
        public async Task SetScoreChannelAsync(IGuildChannel channel, [Remainder] string paramString)
        {
            if (IsAdmin())
            {
                var eventId = paramString.ParseArgs("eventId");

                if (string.IsNullOrEmpty(eventId))
                {
                    await ReplyAsync(embed: "Usage: `setScoreChannel #channel -eventId \"[event id]\"`\nTo find event ids, please run `listEvents`".ErrorEmbed());
                }
                else
                {
                    var server = ServerService.GetServer();
                    if (server == null)
                    {
                        await ReplyAsync(embed: "The Server is not running, so we can't can't add events to it".ErrorEmbed());
                    }
                    else
                    {
                        var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts, $"{server.CoreServer.Address}:{server.CoreServer.Port}", 0);
                        var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.EventId.ToString() == eventId));

                        if (targetPair.Key != null)
                        {
                            var targetEvent = targetPair.Value.Events.First(x => x.EventId.ToString() == eventId);
                            targetEvent.InfoChannel = new TournamentAssistantShared.Models.Discord.Channel
                            {
                                Id = channel?.Id ?? 0,
                                Name = channel?.Name ?? ""
                            };

                            var response = await server.SendUpdateQualifierEvent(targetPair.Key, targetEvent);
                            if (response.Type == Response.ResponseType.Success)
                            {
                                await ReplyAsync(embed: response.Message.SuccessEmbed());
                            }
                            else if (response.Type == Response.ResponseType.Fail)
                            {
                                await ReplyAsync(embed: response.Message.ErrorEmbed());
                            }
                        }
                        else await ReplyAsync(embed: "Could not find an event with that ID".ErrorEmbed());
                    }
                }
            }
            else await ReplyAsync(embed: "You do not have sufficient permissions to use this command".ErrorEmbed());
        }

        [Command("addSong")]
        [Summary("Add a song to the currently running event")]
        [RequireContext(ContextType.Guild)]
        public async Task AddSongAsync([Remainder] string paramString = null)
        {
            if (IsAdmin())
            {
                var eventId = paramString.ParseArgs("eventId");
                var songId = paramString.ParseArgs("song");

                if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(songId))
                {
                    await ReplyAsync(embed: ("Usage: `addSong -eventId \"[event id]\" -song [song link]`\n" +
                        "To find event ids, please run `listEvents`\n" +
                        "Optional parameters: `-difficulty [difficulty]`, `-characteristic [characteristic]` (example: `-characteristic onesaber`), `-[modifier]` (example: `-nofail`)").ErrorEmbed());
                    return;
                }

                //Parse the difficulty input, either as an int or a string
                BeatmapDifficulty difficulty = BeatmapDifficulty.ExpertPlus;

                string difficultyArg = paramString.ParseArgs("difficulty");
                if (difficultyArg != null)
                {
                    //If the enum conversion doesn't succeed, try it as an int
                    if (!Enum.TryParse(difficultyArg, true, out difficulty))
                    {
                        await ReplyAsync(embed: "Could not parse difficulty parameter".ErrorEmbed());
                        return;
                    }
                }

                string characteristic = paramString.ParseArgs("characteristic");
                characteristic = characteristic ?? "Standard";

                GameOptions gameOptions = GameOptions.None;
                PlayerOptions playerOptions = PlayerOptions.None;

                //Load up the GameOptions and PlayerOptions
                foreach (GameOptions o in Enum.GetValues(typeof(GameOptions)))
                {
                    if (paramString.ParseArgs(o.ToString()) == "true") gameOptions = (gameOptions | o);
                }

                foreach (PlayerOptions o in Enum.GetValues(typeof(PlayerOptions)))
                {
                    if (paramString.ParseArgs(o.ToString()) == "true") playerOptions = (playerOptions | o);
                }

                //Sanitize input
                if (songId.StartsWith("https://beatsaver.com/") || songId.StartsWith("https://bsaber.com/"))
                {
                    //Strip off the trailing slash if there is one
                    if (songId.EndsWith("/")) songId = songId.Substring(0, songId.Length - 1);

                    //Strip off the beginning of the url to leave the id
                    songId = songId.Substring(songId.LastIndexOf("/") + 1);
                }

                if (songId.Contains("&"))
                {
                    songId = songId.Substring(0, songId.IndexOf("&"));
                }

                var server = ServerService.GetServer();
                if (server == null)
                {
                    await ReplyAsync(embed: "The Server is not running, so we can't can't add songs to it".ErrorEmbed());
                }
                else
                {
                    //Get the hash for the song
                    var hash = TournamentAssistantShared.BeatSaver.BeatSaverDownloader.GetHashFromID(songId);
                    var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts, $"{server.CoreServer.Address}:{server.CoreServer.Port}", 0);
                    var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.EventId.ToString() == eventId));
                    var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.EventId.ToString() == eventId);
                    var songPool = targetEvent.QualifierMaps.ToList();

                    if (OstHelper.IsOst(hash))
                    {
                        if (!SongExists(songPool, hash, characteristic, (int)difficulty, (int)gameOptions, (int)playerOptions))
                        {
                            GameplayParameters parameters = new GameplayParameters
                            {
                                Beatmap = new Beatmap
                                {
                                    Name = OstHelper.GetOstSongNameFromLevelId(hash),
                                    LevelId = hash,
                                    Characteristic = new Characteristic
                                    {
                                        SerializedName = characteristic
                                    },
                                    Difficulty = difficulty
                                },
                                GameplayModifiers = new GameplayModifiers
                                {
                                    Options = gameOptions
                                },
                                PlayerSettings = new PlayerSpecificSettings
                                {
                                    Options = playerOptions
                                }
                            };

                            songPool.Add(parameters);
                            targetEvent.QualifierMaps = songPool.ToArray();

                            var response = await server.SendUpdateQualifierEvent(targetPair.Key, targetEvent);
                            if (response.Type == Response.ResponseType.Success)
                            {
                                await ReplyAsync(embed: ($"Added: {parameters.Beatmap.Name} ({difficulty}) ({characteristic})" +
                                    $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                    $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}").SuccessEmbed());
                            }
                            else if (response.Type == Response.ResponseType.Fail)
                            {
                                await ReplyAsync(embed: response.Message.ErrorEmbed());
                            }
                        }
                        else await ReplyAsync(embed: "Song is already active in the database".ErrorEmbed());
                    }
                    else
                    {
                        var songInfo = await TournamentAssistantShared.BeatSaver.BeatSaverDownloader.GetSongInfo(songId);
                        string songName = songInfo.name;

                        if (!songInfo.HasDifficulty(characteristic, difficulty))
                        {
                            BeatmapDifficulty nextBestDifficulty = songInfo.GetClosestDifficultyPreferLower(characteristic, difficulty);

                            if (SongExists(songPool, hash, characteristic, (int)nextBestDifficulty, (int)gameOptions, (int)playerOptions))
                            {
                                await ReplyAsync(embed: $"{songName} doesn't have {difficulty}, and {nextBestDifficulty} is already in the event".ErrorEmbed());
                            }
                            else
                            {
                                GameplayParameters parameters = new GameplayParameters
                                {
                                    Beatmap = new Beatmap
                                    {
                                        Name = songName,
                                        LevelId = $"custom_level_{hash.ToUpper()}",
                                        Characteristic = new Characteristic
                                        {
                                            SerializedName = characteristic
                                        },
                                        Difficulty = nextBestDifficulty
                                    },
                                    GameplayModifiers = new GameplayModifiers
                                    {
                                        Options = gameOptions
                                    },
                                    PlayerSettings = new PlayerSpecificSettings
                                    {
                                        Options = playerOptions
                                    }
                                };

                                songPool.Add(parameters);
                                targetEvent.QualifierMaps = songPool.ToArray();

                                var response = await server.SendUpdateQualifierEvent(targetPair.Key, targetEvent);
                                if (response.Type == Response.ResponseType.Success)
                                {
                                    await ReplyAsync(embed: ($"{songName} doesn't have {difficulty}, using {nextBestDifficulty} instead.\n" +
                                        $"Added to the song list" +
                                        $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                        $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}").SuccessEmbed());
                                }
                                else if (response.Type == Response.ResponseType.Fail)
                                {
                                    await ReplyAsync(embed: response.Message.ErrorEmbed());
                                }
                            }
                        }
                        else
                        {
                            GameplayParameters parameters = new GameplayParameters
                            {
                                Beatmap = new Beatmap
                                {
                                    Name = songName,
                                    LevelId = $"custom_level_{hash.ToUpper()}",
                                    Characteristic = new Characteristic
                                    {
                                        SerializedName = characteristic
                                    },
                                    Difficulty = difficulty
                                },
                                GameplayModifiers = new GameplayModifiers
                                {
                                    Options = gameOptions
                                },
                                PlayerSettings = new PlayerSpecificSettings
                                {
                                    Options = playerOptions
                                }
                            };

                            songPool.Add(parameters);
                            targetEvent.QualifierMaps = songPool.ToArray();

                            var response = await server.SendUpdateQualifierEvent(targetPair.Key, targetEvent);
                            if (response.Type == Response.ResponseType.Success)
                            {
                                await ReplyAsync(embed: ($"{songName} ({difficulty}) ({characteristic}) downloaded and added to song list" +
                                    $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                    $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}").SuccessEmbed());
                            }
                            else if (response.Type == Response.ResponseType.Fail)
                            {
                                await ReplyAsync(embed: response.Message.ErrorEmbed());
                            }
                        }
                    }
                }
            }
            else await ReplyAsync(embed: "You do not have sufficient permissions to use this command".ErrorEmbed());
        }

        [Command("listSongs")]
        [Summary("List the currently active songs for the current event")]
        [RequireContext(ContextType.Guild)]
        public async Task ListSongsAsync([Remainder] string paramString = null)
        {
            var server = ServerService.GetServer();
            if (server == null)
            {
                await ReplyAsync(embed: "The Server is not running, so we can't can't get any event info".ErrorEmbed());
            }
            else
            {
                var eventId = paramString.ParseArgs("eventId");

                if (string.IsNullOrEmpty(eventId))
                {
                    await ReplyAsync(embed: ("Usage: `listSongs -eventId \"[event id]\"`\n" +
                        "To find event ids, please run `listEvents`\n").ErrorEmbed());
                    return;
                }

                var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts, $"{server.CoreServer.Address}:{server.CoreServer.Port}", 0);
                var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.EventId.ToString() == eventId));
                var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.EventId.ToString() == eventId);
                var songPool = targetEvent.QualifierMaps.ToList();

                var builder = new EmbedBuilder();
                builder.Title = "<:page_with_curl:735592941338361897> Song List";
                builder.Color = new Color(random.Next(255), random.Next(255), random.Next(255));

                var titleField = new EmbedFieldBuilder();
                titleField.Name = "Title";
                titleField.Value = "```";
                titleField.IsInline = true;

                var difficultyField = new EmbedFieldBuilder();
                difficultyField.Name = "Difficulty";
                difficultyField.Value = "```";
                difficultyField.IsInline = true;

                var modifierField = new EmbedFieldBuilder();
                modifierField.Name = "Modifiers";
                modifierField.Value = "```";
                modifierField.IsInline = true;

                foreach (var song in songPool)
                {
                    titleField.Value += $"\n{song.Beatmap.Name}";
                    difficultyField.Value += $"\n{song.Beatmap.Difficulty}";
                    modifierField.Value += $"\n{song.GameplayModifiers.Options}";
                }

                titleField.Value += "```";
                difficultyField.Value += "```";
                modifierField.Value += "```";

                builder.AddField(titleField);
                builder.AddField(difficultyField);
                builder.AddField(modifierField);

                await ReplyAsync(embed: builder.Build());
            }
        }

        [Command("removeSong")]
        [Summary("Remove a song from the currently running event")]
        [RequireContext(ContextType.Guild)]
        public async Task RemoveSongAsync([Remainder] string paramString = null)
        {
            if (IsAdmin())
            {
                var server = ServerService.GetServer();
                if (server == null)
                {
                    await ReplyAsync(embed: "The Server is not running, so we can't can't get any event info".ErrorEmbed());
                }
                else
                {
                    var eventId = paramString.ParseArgs("eventId");
                    var songId = paramString.ParseArgs("song");

                    if (string.IsNullOrEmpty(eventId))
                    {
                        await ReplyAsync(embed: ("Usage: `removeSong -eventId \"[event id]\" -song [song link]`\n" +
                            "To find event ids, please run `listEvents`\n" +
                            "Note: You may also need to include difficulty and modifier info to be sure you remove the right song").ErrorEmbed());
                        return;
                    }

                    var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts, $"{server.CoreServer.Address}:{server.CoreServer.Port}", 0);
                    var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.EventId.ToString() == eventId));
                    var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.EventId.ToString() == eventId);
                    var songPool = targetEvent.QualifierMaps.ToList();

                    //Parse the difficulty input, either as an int or a string
                    BeatmapDifficulty difficulty = BeatmapDifficulty.ExpertPlus;

                    string difficultyArg = paramString.ParseArgs("difficulty");
                    if (difficultyArg != null)
                    {
                        //If the enum conversion doesn't succeed, try it as an int
                        if (!Enum.TryParse(difficultyArg, true, out difficulty))
                        {
                            await ReplyAsync(embed: ("Could not parse difficulty parameter.\n" +
                            "Usage: `removeSong [songId] [difficulty]`").ErrorEmbed());
                            return;
                        }
                    }

                    string characteristic = paramString.ParseArgs("characteristic");
                    characteristic = characteristic ?? "Standard";

                    GameOptions gameOptions = GameOptions.None;
                    PlayerOptions playerOptions = PlayerOptions.None;

                    //Load up the GameOptions and PlayerOptions
                    foreach (GameOptions o in Enum.GetValues(typeof(GameOptions)))
                    {
                        if (paramString.ParseArgs(o.ToString()) == "true") gameOptions = (gameOptions | o);
                    }

                    foreach (PlayerOptions o in Enum.GetValues(typeof(PlayerOptions)))
                    {
                        if (paramString.ParseArgs(o.ToString()) == "true") playerOptions = (playerOptions | o);
                    }

                    //Sanitize input
                    if (songId.StartsWith("https://beatsaver.com/") || songId.StartsWith("https://bsaber.com/"))
                    {
                        //Strip off the trailing slash if there is one
                        if (songId.EndsWith("/")) songId = songId.Substring(0, songId.Length - 1);

                        //Strip off the beginning of the url to leave the id
                        songId = songId.Substring(songId.LastIndexOf("/") + 1);
                    }

                    if (songId.Contains("&"))
                    {
                        songId = songId.Substring(0, songId.IndexOf("&"));
                    }

                    //Get the hash for the song
                    var hash = TournamentAssistantShared.BeatSaver.BeatSaverDownloader.GetHashFromID(songId);

                    var song = FindSong(songPool, $"custom_level_{hash.ToUpper()}", characteristic, (int)difficulty, (int)gameOptions, (int)playerOptions);
                    if (song != null)
                    {
                        targetEvent.QualifierMaps = RemoveSong(songPool, $"custom_level_{hash.ToUpper()}", characteristic, (int)difficulty, (int)gameOptions, (int)playerOptions).ToArray();

                        var response = await server.SendUpdateQualifierEvent(targetPair.Key, targetEvent);
                        if (response.Type == Response.ResponseType.Success)
                        {
                            await ReplyAsync(embed: ($"Removed {song.Beatmap.Name} ({difficulty}) ({characteristic}) from the song list" +
                                $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}").SuccessEmbed());
                        }
                        else if (response.Type == Response.ResponseType.Fail)
                        {
                            await ReplyAsync(embed: response.Message.ErrorEmbed());
                        }
                    }
                    else await ReplyAsync(embed: $"Specified song does not exist with that difficulty / characteristic / gameOptions / playerOptions ({difficulty} {characteristic} {gameOptions} {playerOptions})".ErrorEmbed());
                }                
            }
        }

        [Command("endEvent")]
        [Summary("End the current event")]
        [RequireContext(ContextType.Guild)]
        public async Task EndEventAsync([Remainder] string paramString = null)
        {
            if (IsAdmin())
            {
                //Make server backup
                /*Logger.Warning($"BACKING UP DATABASE...");
                File.Copy("BotDatabase.db", $"EventDatabase_bak_{DateTime.Now.Day}_{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}.db");
                Logger.Success("Database backed up succsessfully.");*/

                var server = ServerService.GetServer();
                if (server == null)
                {
                    await ReplyAsync(embed: "The Server is not running, so we can't can't get any event info".ErrorEmbed());
                }
                else
                {
                    var eventId = paramString.ParseArgs("eventId");

                    if (string.IsNullOrEmpty(eventId))
                    {
                        await ReplyAsync(embed: ("Usage: `endEvent -eventId \"[event id]\"`\n" +
                            "To find event ids, please run `listEvents`").ErrorEmbed());
                        return;
                    }

                    var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts, $"{server.CoreServer.Address}:{server.CoreServer.Port}", 0);
                    var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.EventId.ToString() == eventId));
                    var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.EventId.ToString() == eventId);

                    var response = await server.SendDeleteQualifierEvent(targetPair.Key, targetEvent);
                    if (response.Type == Response.ResponseType.Success)
                    {
                        await ReplyAsync(embed: response.Message.SuccessEmbed());
                    }
                    else if (response.Type == Response.ResponseType.Fail)
                    {
                        await ReplyAsync(embed: response.Message.ErrorEmbed());
                    }
                }
            }
            else await ReplyAsync(embed: "You do not have sufficient permissions to use this command".ErrorEmbed());
        }

        [Command("listEvents")]
        [Summary("Show all events we can find info about")]
        [RequireContext(ContextType.Guild)]
        public async Task ListEventsAsync()
        {
            if (IsAdmin())
            {
                var server = ServerService.GetServer();
                if (server == null)
                {
                    await ReplyAsync(embed: "The Server is not running, so we can't can't get any event info".ErrorEmbed());
                }
                else
                {
                    var knownEvents = (await HostScraper.ScrapeHosts(server.State.KnownHosts, $"{server.CoreServer.Address}:{server.CoreServer.Port}", 0)).Select(x => x.Value).Where(x => x.Events != null).SelectMany(x => x.Events);

                    var builder = new EmbedBuilder();
                    builder.Title = "<:page_with_curl:735592941338361897> Events";
                    builder.Color = new Color(random.Next(255), random.Next(255), random.Next(255));

                    foreach (var @event in knownEvents)
                    {
                        builder.AddField(@event.Name, $"```fix\n{@event.EventId}```\n" +
                            $"```css\n({@event.Guild.Name})```", true);
                    }

                    await ReplyAsync(embed: builder.Build());
                }
            }
            else await ReplyAsync(embed: "You do not have sufficient permissions to use this command".ErrorEmbed());
        }

        [Command("listHosts")]
        [Summary("Show all hosts we can find info about")]
        [RequireContext(ContextType.Guild)]
        public async Task ListHostsAsync()
        {
            if (IsAdmin())
            {
                var server = ServerService.GetServer();
                if (server == null)
                {
                    await ReplyAsync(embed: "The Server is not running, so we can't can't get any host info".ErrorEmbed());
                }
                else
                {
                    var builder = new EmbedBuilder();
                    builder.Title = "<:page_with_curl:735592941338361897> Hosts";
                    builder.Color = new Color(random.Next(255), random.Next(255), random.Next(255));

                    foreach (var host in server.State.KnownHosts)
                    {
                        builder.AddField(host.Name, $"```\n{host.Address}:{host.Port}```", true);
                    }

                    await ReplyAsync(embed: builder.Build());
                }
            }
            else await ReplyAsync(embed: "You do not have sufficient permissions to use this command".ErrorEmbed());
        }

        [Command("leaderboards")]
        [Alias("leaderboard")]
        [Summary("Show leaderboards from the currently running event")]
        [RequireContext(ContextType.Guild)]
        public async Task LeaderboardsAsync([Remainder] string paramString)
        {
            if (IsAdmin())
            {
                var server = ServerService.GetServer();
                if (server == null)
                {
                    await ReplyAsync(embed: "The Server is not running, so we can't can't get any host info".ErrorEmbed());
                }
                else
                {
                    var eventId = paramString.ParseArgs("eventId");
                    var knownPairs = await HostScraper.ScrapeHosts(server.State.KnownHosts, $"{server.CoreServer.Address}:{server.CoreServer.Port}", 0);
                    var targetPair = knownPairs.FirstOrDefault(x => x.Value.Events.Any(y => y.EventId.ToString() == eventId));
                    var targetEvent = targetPair.Value.Events.FirstOrDefault(x => x.EventId.ToString() == eventId);

                    var builder = new EmbedBuilder();
                    builder.Title = "<:page_with_curl:735592941338361897> Leaderboards";
                    builder.Color = new Color(random.Next(255), random.Next(255), random.Next(255));

                    var playerNames = new List<string>();
                    var playerScores = new List<string>();

                    foreach (var map in targetEvent.QualifierMaps)
                    {
                        var scores = (await HostScraper.RequestResponse(targetPair.Key, new Packet(new ScoreRequest
                        {
                            EventId = Guid.Parse(eventId),
                            Parameters = map
                        }), typeof(ScoreRequestResponse), $"{server.CoreServer.Address}:{server.CoreServer.Port}", 0)).SpecificPacket as ScoreRequestResponse;

                        builder.AddField(map.Beatmap.Name, $"```\n{string.Join("\n", scores.Scores.Select(x => $"{x.Username} {x._Score} {(x.FullCombo ? "FC" : "")}\n"))}```", true);
                    }

                    await ReplyAsync(embed: builder.Build());
                }
            }
            else await ReplyAsync(embed: "You do not have sufficient permissions to use this command".ErrorEmbed());
        }
    }
}
