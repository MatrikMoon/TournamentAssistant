#pragma warning disable 1998
using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TournamentAssistantShared.BeatSaver;
using TournamentAssistantShared.Discord.Database;
using TournamentAssistantShared.Discord.Helpers;
using TournamentAssistantShared.Discord.Services;
using static TournamentAssistantShared.Models.GameplayModifiers;
using static TournamentAssistantShared.Models.PlayerSpecificSettings;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantShared.Discord.Modules
{
    public class QualifierModule : ModuleBase<SocketCommandContext>
    {
        private static Random random = new Random();

        public DatabaseService DatabaseService { get; set; }
        public ScoresaberService ScoresaberService { get; set; }

        //Pull parameters out of an argument list string
        //Note: argument specifiers are required to start with "-"
        private static string ParseArgs(string argString, string argToGet)
        {
            //Return nothing if the parameter arg string is empty
            if (string.IsNullOrWhiteSpace(argString) || string.IsNullOrWhiteSpace(argToGet)) return null;

            List<string> argsWithQuotedStrings = new List<string>();
            string[] argArray = argString.Split(' ');

            for (int x = 0; x < argArray.Length; x++)
            {
                if (argArray[x].StartsWith("\""))
                {
                    string assembledString = string.Empty; //argArray[x].Substring(1) + " ";
                    for (int y = x; y < argArray.Length; y++)
                    {
                        if (argArray[y].StartsWith("\"")) argArray[y] = argArray[y].Substring(1); //Strip quotes off the front of the currently tested word.
                                                                                                  //This is necessary since this part of the code also handles the string right after the open quote
                        if (argArray[y].EndsWith("\""))
                        {
                            assembledString += argArray[y].Substring(0, argArray[y].Length - 1);
                            x = y;
                            break;
                        }
                        else assembledString += argArray[y] + " ";
                    }
                    argsWithQuotedStrings.Add(assembledString);
                }
                else argsWithQuotedStrings.Add(argArray[x]);
            }

            argArray = argsWithQuotedStrings.ToArray();

            for (int i = 0; i < argArray.Length; i++)
            {
                if (argArray[i].ToLower() == $"-{argToGet}".ToLower())
                {
                    if (((i + 1) < (argArray.Length)) && !argArray[i + 1].StartsWith("-"))
                    {
                        return argArray[i + 1];
                    }
                    else return "true";
                }
            }

            return null;
        }

        private bool IsAdmin()
        {
            return ((IGuildUser)Context.User).GuildPermissions.Has(GuildPermission.Administrator);
        }

        private Song FindSong(ulong eventId, string levelId, string characteristic, int beatmapDifficulty, int gameOptions, int playerOptions)
        {
            return DatabaseService.DatabaseContext.Songs.FirstOrDefault(x => x.EventId == eventId && x.LevelId == levelId && x.Characteristic == characteristic && x.BeatmapDifficulty == beatmapDifficulty && x.GameOptions == gameOptions && x.PlayerOptions == playerOptions && !x.Old);
        }

        private bool SongExists(ulong eventId, string levelId, string characteristic, int beatmapDifficulty, int gameOptions, int playerOptions)
        {
            return FindSong(eventId, levelId, characteristic, beatmapDifficulty, gameOptions, playerOptions) != null;
        }

        [Command("createEvent")]
        [Summary("Create a Qualifier event for your guild")]
        [RequireContext(ContextType.Guild)]
        public async Task CreateEventAsync([Remainder] string name)
        {
            if (IsAdmin())
            {
                if (DatabaseService.DatabaseContext.Events.Any(x => !x.Old && x.EventId == Context.Guild.Id)) await ReplyAsync(embed: "There is already an event running for your guild".ErrorEmbed());
                else
                {
                    await DatabaseService.DatabaseContext.Events.AddAsync(new Event
                    {
                        Name = name,
                        EventId = Context.Guild.Id,
                        GuildId = Context.Guild.Id,
                    });
                    await DatabaseService.DatabaseContext.SaveChangesAsync();

                    await ReplyAsync(embed: $"Successfully created event: {name}".SuccessEmbed());
                }
            }
        }

        [Command("register")]
        [RequireContext(ContextType.Guild)]
        public async Task RegisterAsync(string userId)
        {
            //Sanitize input
            if (userId.StartsWith("https://scoresaber.com/u/")) userId = userId.Substring("https://scoresaber.com/u/".Length);
            if (userId.Contains("&")) userId = userId.Substring(0, userId.IndexOf("&"));
            userId = Regex.Replace(userId, "[^0-9]", "");

            //Check to see if there's an event going on for this server
            if (!DatabaseService.DatabaseContext.Events.Any(x => !x.Old && x.EventId == Context.Guild.Id))
            {
                await ReplyAsync(embed: "There are no events running in your guild".ErrorEmbed());
                return;
            }

            var user = (IGuildUser)Context.User;
            var player = DatabaseService.DatabaseContext.Players.FirstOrDefault(x => x.GuildId == Context.Guild.Id && x.DiscordId == user.Id);
            if (player == null)
            {
                //Escape apostrophes in player's names
                //TODO: Proper escaping, if it's even necessary
                //Yes, this is the worst type of todo, one that's a potential
                //major security flaw. Sue me. Not literally though.
                string username = Regex.Replace(user.Username, "[\'\";]", "");

                player = new Player
                {
                    GuildId = Context.Guild.Id,
                    DiscordId = user.Id,
                    ScoresaberId = Convert.ToUInt64(userId),
                    DiscordName = username,
                    DiscordExtension = user.Discriminator,
                    DiscordMention = user.Mention
                };

                //Get country and rank data
                var basicData = await ScoresaberService.GetBasicPlayerData(userId);
                player.Country = ScoresaberService.GetPlayerCountry(basicData);
                player.Rank = Convert.ToInt32(ScoresaberService.GetPlayerRank(basicData));

                //Add player to database
                DatabaseService.DatabaseContext.Players.Add(player);
                await DatabaseService.DatabaseContext.SaveChangesAsync();

                //Send success message
                await ReplyAsync(embed: $"User `{player.DiscordName}` successfully linked to `{player.DiscordId}`".SuccessEmbed());
            }
            else if (player.DiscordId != user.Id)
            {
                await ReplyAsync(embed: $"That steam account is already linked to `{player.DiscordName}`, message an admin if you *really* need to relink it.".WarningEmbed());
            }
            else await ReplyAsync(embed: "You are already registered!".InfoEmbed());
        }

        [Command("addSong")]
        [RequireContext(ContextType.Guild)]
        public async Task AddSongAsync(string songId, [Remainder] string paramString = null)
        {
            if (IsAdmin())
            {
                //Parse the difficulty input, either as an int or a string
                BeatmapDifficulty difficulty = BeatmapDifficulty.ExpertPlus;

                string difficultyArg = ParseArgs(paramString, "difficulty");
                if (difficultyArg != null)
                {
                    //If the enum conversion doesn't succeed, try it as an int
                    if (!Enum.TryParse(difficultyArg, true, out difficulty))
                    {
                        await ReplyAsync(embed: ("Could not parse difficulty parameter.\n" +
                        "Usage: addSong [songId] [difficulty]").ErrorEmbed());

                        return;
                    }
                }

                string characteristic = ParseArgs(paramString, "characteristic");
                characteristic = characteristic ?? "Standard";

                GameOptions gameOptions = GameOptions.None;
                PlayerOptions playerOptions = PlayerOptions.None;

                //Load up the GameOptions and PlayerOptions
                foreach (GameOptions o in Enum.GetValues(typeof(GameOptions)))
                {
                    if (ParseArgs(paramString, o.ToString()) == "true") gameOptions = (gameOptions | o);
                }

                foreach (PlayerOptions o in Enum.GetValues(typeof(PlayerOptions)))
                {
                    if (ParseArgs(paramString, o.ToString()) == "true") playerOptions = (playerOptions | o);
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
                var hash = BeatSaverDownloader.GetHashFromID(songId);

                if (OstHelper.IsOst(hash))
                {
                    //if (!Song.Exists(hash, parsedDifficulty, characteristicArg, true))
                    if (!SongExists(Context.Guild.Id, hash, characteristic, (int)difficulty, (int)gameOptions, (int)playerOptions))
                    {
                        Song song = new Song
                        {
                            Name = OstHelper.GetOstSongNameFromLevelId(hash),
                            EventId = Context.Guild.Id,
                            LevelId = hash,
                            Characteristic = characteristic,
                            BeatmapDifficulty = (int)difficulty,
                            GameOptions = (int)gameOptions,
                            PlayerOptions = (int)playerOptions
                        };

                        await DatabaseService.DatabaseContext.Songs.AddAsync(song);
                        await DatabaseService.DatabaseContext.SaveChangesAsync();

                        await ReplyAsync(embed: ($"Added: {song.Name} ({difficulty}) ({characteristic})" +
                                $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}").SuccessEmbed());
                    }
                    else await ReplyAsync(embed: "Song is already active in the database".ErrorEmbed());
                }
                else
                {
                    BeatSaverDownloader.DownloadSong(hash, async (songPath) =>
                    {
                        if (songPath != null)
                        {
                            DownloadedSong song = new DownloadedSong(hash);
                            string songName = song.Name;

                            if (!song.GetBeatmapDifficulties(characteristic).Contains(difficulty))
                            {
                                BeatmapDifficulty nextBestDifficulty = song.GetClosestDifficultyPreferLower(difficulty);

                                if (SongExists(Context.Guild.Id, hash, characteristic, (int)nextBestDifficulty, (int)gameOptions, (int)playerOptions))
                                {
                                    await ReplyAsync(embed: $"{songName} doesn't have {difficulty}, and {nextBestDifficulty} is already in the database".ErrorEmbed());
                                }

                                else
                                {
                                    Song databaseSong = new Song
                                    {
                                        Name = songName,
                                        EventId = Context.Guild.Id,
                                        LevelId = hash,
                                        Characteristic = characteristic,
                                        BeatmapDifficulty = (int)nextBestDifficulty,
                                        GameOptions = (int)gameOptions,
                                        PlayerOptions = (int)playerOptions
                                    };

                                    await DatabaseService.DatabaseContext.Songs.AddAsync(databaseSong);
                                    await DatabaseService.DatabaseContext.SaveChangesAsync();

                                    await ReplyAsync(embed: ($"{songName} doesn't have {difficulty}, using {nextBestDifficulty} instead.\n" +
                                        $"Added to the song list" +
                                        $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                        $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}").SuccessEmbed());
                                }
                            }
                            else
                            {
                                Song databaseSong = new Song
                                {
                                    Name = songName,
                                    EventId = Context.Guild.Id,
                                    LevelId = hash,
                                    Characteristic = characteristic,
                                    BeatmapDifficulty = (int)difficulty,
                                    GameOptions = (int)gameOptions,
                                    PlayerOptions = (int)playerOptions
                                };

                                await DatabaseService.DatabaseContext.Songs.AddAsync(databaseSong);
                                await DatabaseService.DatabaseContext.SaveChangesAsync();

                                await ReplyAsync(embed: ($"{songName} ({difficulty}) ({characteristic}) downloaded and added to song list" +
                                    $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                    $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}").ErrorEmbed());
                            }
                        }
                        else await ReplyAsync(embed: "Could not download song.".ErrorEmbed());
                    });
                }
            }
        }

        [Command("listSongs")]
        [RequireContext(ContextType.Guild)]
        public async Task ListSongsAsync()
        {
            if (!DatabaseService.DatabaseContext.Events.Any(x => !x.Old && x.EventId == Context.Guild.Id))
            {
                await ReplyAsync(embed: "There are no events running in your guild".ErrorEmbed());
                return;
            }

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

            foreach (var song in DatabaseService.DatabaseContext.Songs.Where(x => !x.Old && x.EventId == Context.Guild.Id))
            {
                titleField.Value += $"\n{song.Name}";
                difficultyField.Value += $"\n{(BeatmapDifficulty)song.BeatmapDifficulty}";
                modifierField.Value += $"\n{(GameOptions)song.GameOptions}";
            }

            titleField.Value += "```";
            difficultyField.Value += "```";
            modifierField.Value += "```";

            builder.AddField(titleField);
            builder.AddField(difficultyField);
            builder.AddField(modifierField);

            await ReplyAsync(embed: builder.Build());
        }

        [Command("removeSong")]
        [RequireContext(ContextType.Guild)]
        public async Task RemoveSongAsync(string songId, [Remainder] string paramString = null)
        {
            if (IsAdmin())
            {
                //Parse the difficulty input, either as an int or a string
                BeatmapDifficulty difficulty = BeatmapDifficulty.ExpertPlus;

                string difficultyArg = ParseArgs(paramString, "difficulty");
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

                string characteristic = ParseArgs(paramString, "characteristic");
                characteristic = characteristic ?? "Standard";

                GameOptions gameOptions = GameOptions.None;
                PlayerOptions playerOptions = PlayerOptions.None;

                //Load up the GameOptions and PlayerOptions
                foreach (GameOptions o in Enum.GetValues(typeof(GameOptions)))
                {
                    if (ParseArgs(paramString, o.ToString()) == "true") gameOptions = (gameOptions | o);
                }

                foreach (PlayerOptions o in Enum.GetValues(typeof(PlayerOptions)))
                {
                    if (ParseArgs(paramString, o.ToString()) == "true") playerOptions = (playerOptions | o);
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
                var hash = BeatSaverDownloader.GetHashFromID(songId);

                var song = FindSong(Context.Guild.Id, hash, characteristic, (int)difficulty, (int)gameOptions, (int)playerOptions);
                if (song != null)
                {
                    song.Old = true;

                    await DatabaseService.DatabaseContext.SaveChangesAsync();

                    await ReplyAsync(embed: ($"Removed {song.Name} ({difficulty}) ({characteristic}) from the song list" +
                                    $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                    $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}").SuccessEmbed());
                }
                else await ReplyAsync(embed: $"Specified song does not exist with that difficulty / characteristic / gameOptions / playerOptions ({difficulty} {characteristic} {gameOptions} {playerOptions})".ErrorEmbed());
            }
        }

        [Command("endEvent")]
        [RequireContext(ContextType.Guild)]
        public async Task EndEventAsync()
        {
            if (IsAdmin())
            {
                //Make server backup
                Logger.Warning($"BACKING UP DATABASE...");
                File.Copy("BotDatabase.db", $"EventDatabase_bak_{DateTime.Now.Day}_{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}.db");
                Logger.Success("Database backed up succsessfully.");

                var currentEvent = DatabaseService.DatabaseContext.Events.FirstOrDefault(x => !x.Old && x.EventId == Context.Guild.Id);
                if (currentEvent == null)
                {
                    await ReplyAsync(embed: "There are no events running in your guild".ErrorEmbed());
                    return;
                }

                //Mark all songs and scores as old
                currentEvent.Old = true;
                await DatabaseService.DatabaseContext.Songs.Where(x => x.EventId == currentEvent.EventId).ForEachAsync(x => x.Old = true);
                await DatabaseService.DatabaseContext.Scores.Where(x => x.EventId == currentEvent.EventId).ForEachAsync(x => x.Old = true);
                await DatabaseService.DatabaseContext.SaveChangesAsync();

                await ReplyAsync(embed: "All songs and scores are marked as old. You may now add new songs.".SuccessEmbed());
            }
        }

        [Command("leaderboards")]
        [RequireContext(ContextType.Guild)]
        public async Task LeaderboardsAsync()
        {
            
        }
    }
}
