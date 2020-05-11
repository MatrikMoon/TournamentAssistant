#pragma warning disable 1998
using BattleSaberShared.BeatSaver;
using BattleSaberShared.Discord.Database;
using BattleSaberShared.Discord.Services;
using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static BattleSaberShared.Models.GameplayModifiers;
using static BattleSaberShared.Models.PlayerSpecificSettings;
using static BattleSaberShared.SharedConstructs;

namespace BattleSaberShared.Discord.Modules
{
    public class QualifierModule : ModuleBase<SocketCommandContext>
    {
        public DatabaseService DatabaseService { get; set; }

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

        private Song FindSong(ulong guildId, string levelId, string characteristic, int beatmapDifficulty, int gameOptions, int playerOptions)
        {
            return DatabaseService.DatabaseContext.Songs.FirstOrDefault(x => x.LevelId == levelId && x.Characteristic == characteristic && x.BeatmapDifficulty == beatmapDifficulty && x.GameOptions == gameOptions && x.PlayerOptions == playerOptions && !x.Old);
        }

        private bool SongExists(ulong guildId, string levelId, string characteristic, int beatmapDifficulty, int gameOptions, int playerOptions)
        {
            return FindSong(guildId, levelId, characteristic, beatmapDifficulty, gameOptions, playerOptions) != null;
        }

        [Command("register")]
        [RequireContext(ContextType.Guild)]
        public async Task RegisterAsync(string userId)
        {
            //Sanitize input
            if (userId.StartsWith("https://scoresaber.com/u/")) userId = userId.Substring("https://scoresaber.com/u/".Length);
            if (userId.Contains("&")) userId = userId.Substring(0, userId.IndexOf("&"));
            userId = Regex.Replace(userId, "[^0-9]", "");

            var user = (IGuildUser)Context.User;
            var player = DatabaseService.DatabaseContext.Players.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
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
                    UserId = user.Id,
                    DiscordName = username,
                    DiscordExtension = user.Discriminator,
                    DiscordMention = user.Mention
                };
                
                string reply = $"User `{player.DiscordName}` successfully linked to `{player.UserId}`";
                await ReplyAsync(reply);
            }
            else if (player.DiscordMention != user.Mention)
            {
                await ReplyAsync($"That steam account is already linked to `{player.DiscordName}`, message an admin if you *really* need to relink it.");
            }
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
                        await ReplyAsync("Could not parse difficulty parameter.\n" +
                        "Usage: addSong [songId] [difficulty]");

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
                            GuildId = Context.Guild.Id,
                            LevelId = hash,
                            Characteristic = characteristic,
                            BeatmapDifficulty = (int)difficulty,
                            GameOptions = (int)gameOptions,
                            PlayerOptions = (int)playerOptions
                        };
                        await ReplyAsync($"Added: {song.Name} ({difficulty}) ({characteristic})" +
                                $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions.ToString()})" : "")}" +
                                $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions.ToString()})" : "!")}");
                    }
                    else await ReplyAsync("Song is already active in the database");
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
                                    await ReplyAsync($"{songName} doesn't have {difficulty}, and {nextBestDifficulty} is already in the database.\n" +
                                        $"Song not added.");
                                }

                                else
                                {
                                    Song databaseSong = new Song
                                    {
                                        Name = OstHelper.GetOstSongNameFromLevelId(hash),
                                        GuildId = Context.Guild.Id,
                                        LevelId = hash,
                                        Characteristic = characteristic,
                                        BeatmapDifficulty = (int)nextBestDifficulty,
                                        GameOptions = (int)gameOptions,
                                        PlayerOptions = (int)playerOptions
                                    };
                                    await ReplyAsync($"{songName} doesn't have {difficulty}, using {nextBestDifficulty} instead.\n" +
                                        $"Added to the song list" +
                                        $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                        $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}");
                                }
                            }
                            else
                            {
                                Song databaseSong = new Song
                                {
                                    Name = OstHelper.GetOstSongNameFromLevelId(hash),
                                    GuildId = Context.Guild.Id,
                                    LevelId = hash,
                                    Characteristic = characteristic,
                                    BeatmapDifficulty = (int)difficulty,
                                    GameOptions = (int)gameOptions,
                                    PlayerOptions = (int)playerOptions
                                };
                                await ReplyAsync($"{songName} ({difficulty}) ({characteristic}) downloaded and added to song list" +
                                    $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                    $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}");
                            }
                        }
                        else await ReplyAsync("Could not download song.");
                    });
                }
            }
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
                        await ReplyAsync("Could not parse difficulty parameter.\n" +
                        "Usage: removeSong [songId] [difficulty]");

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
                    await ReplyAsync($"Removed {song.Name} ({difficulty}) ({characteristic}) from the song list" +
                                    $"{(gameOptions != GameOptions.None ? $" with game options: ({gameOptions})" : "")}" +
                                    $"{(playerOptions != PlayerOptions.None ? $" with player options: ({playerOptions})" : "!")}");
                }
                else await ReplyAsync("Specified song does not exist with that difficulty / characteristic / gameOptions / playerOptions");
            }
        }

        [Command("endEvent")]
        [RequireContext(ContextType.Guild)]
        public async Task EndEventAsync()
        {
            /*if (IsAdmin())
            {
                //Make server backup
                Logger.Warning($"BACKING UP DATABASE...");
                File.Copy("EventDatabase.db", $"EventDatabase_bak_{DateTime.Now.Day}_{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}.db");
                Logger.Success("Database backed up succsessfully.");

                //Mark all songs and scores as old
                MarkAllOld();
                await ReplyAsync($"All songs and scores are marked as Old. You may now add new songs.");
            }*/
        }

        [Command("leaderboards")]
        [RequireContext(ContextType.Guild)]
        public async Task LeaderboardsAsync()
        {
            /*if (!IsAdmin()) return;

            string finalMessage = "Leaderboard:\n\n";

            List<SongConstruct> songs = GetActiveSongs(true);

            songs.ForEach(x =>
            {
                string hash = x.SongHash;

                if (x.Scores.Count > 0) //Don't print if no one submitted scores
                {
                    var song = new Song(hash, x.Difficulty, x.Characteristic);
                    finalMessage += song.SongName + ":\n";

                    int place = 1;
                    foreach (ScoreConstruct item in x.Scores)
                    {
                        //Incredibly inefficient to open a song info file every time, but only the score structure is guaranteed to hold the real difficutly,
                        //seeing as auto difficulty is what would be represented in the songconstruct
                        string percentage = "???%";
                        if (!OstHelper.IsOst(hash))
                        {
                            var maxScore = new BeatSaver.Song(hash).GetMaxScore(item.Characteristic, item.Difficulty);
                            percentage = ((double)item.Score / maxScore).ToString("P", CultureInfo.InvariantCulture);
                        }

                        finalMessage += place + ": " + new Player(item.UserId).DiscordName + " - " + item.Score + $" ({percentage})" + (item.FullCombo ? " (Full Combo)" : "");
                        finalMessage += "\n";
                        place++;
                    }
                    finalMessage += "\n";
                }
            });

            //Deal with long messages
            if (finalMessage.Length > 2000)
            {
                for (int i = 0; finalMessage.Length > 2000; i++)
                {
                    await ReplyAsync(finalMessage.Substring(0, finalMessage.Length > 2000 ? 2000 : finalMessage.Length));
                    finalMessage = finalMessage.Substring(2000);
                }
            }
            await ReplyAsync(finalMessage);*/
        }

        [Command("help")]
        [RequireContext(ContextType.Guild)]
        public async Task HelpAsync()
        {
            string ret = "```Key:\n\n" +
                "[] -> parameter\n" +
                "<> -> optional parameter / admin-only parameter\n" +
                "() -> Extra notes about command```\n\n" +
                "```Commands:\n\n" +
                "register [scoresaber link] <extras> <@User>\n" +
                "addSong [beatsaver url] <-difficulty> (<difficulty> can be either a number or whole-word, such as 4 or ExpertPlus)\n" +
                "removeSong [beatsaver url] <-difficulty> (<difficulty> can be either a number or whole-word, such as 4 or ExpertPlus)\n" +
                "endEvent\n" +
                "leaderboards (Shows leaderboards... Duh)\n" +
                "help (This message!)```";
            await ReplyAsync(ret);
        }
    }
}
