using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;
using PoolSongDatabaseModel = TournamentAssistantServer.Database.Models.PoolSong;
using PoolSongProtobufModel = TournamentAssistantShared.Models.Map;
using PoolDatabaseModel = TournamentAssistantServer.Database.Models.Pool;
using PoolProtobufModel = TournamentAssistantShared.Models.Tournament.TournamentSettings.Pool;
using TeamDatabaseModel = TournamentAssistantServer.Database.Models.Team;
using TeamProtobufModel = TournamentAssistantShared.Models.Tournament.TournamentSettings.Team;
using TournamentDatabaseModel = TournamentAssistantServer.Database.Models.Tournament;
using TournamentProtobufModel = TournamentAssistantShared.Models.Tournament;

namespace TournamentAssistantServer.Database.Contexts
{
    public class TournamentDatabaseContext : DatabaseContext
    {
        public TournamentDatabaseContext(string location) : base(location) { }

        public DbSet<TournamentDatabaseModel> Tournaments { get; set; }
        public DbSet<TeamDatabaseModel> Teams { get; set; }
        public DbSet<PoolDatabaseModel> Pools { get; set; }
        public DbSet<PoolSongDatabaseModel> PoolSongs { get; set; }

        public void SaveModelToDatabase(TournamentProtobufModel tournament)
        {
            var databaseModel = new TournamentDatabaseModel
            {
                Guid = tournament.Guid,
                Name = tournament.Settings.TournamentName,
                Image = Convert.ToBase64String(tournament.Settings.TournamentImage),
                EnableTeams = tournament.Settings.EnableTeams,
                ScoreUpdateFrequency = tournament.Settings.ScoreUpdateFrequency,
                BannedMods = string.Join(",", tournament.Settings.BannedMods),
                ServerAddress = tournament.Server.Address,
                ServerName = tournament.Server.Name,
                ServerPort = tournament.Server.Port.ToString(),
                ServerWebsocketPort = tournament.Server.WebsocketPort.ToString(),
            };

            var existingTournament = Tournaments.FirstOrDefault(x => !x.Old && x.Guid == tournament.Guid);
            if (existingTournament != null)
            {
                databaseModel.ID = existingTournament.ID;
                Entry(existingTournament).CurrentValues.SetValues(databaseModel);
            }
            else
            {
                Tournaments.Add(databaseModel);
            }

            //-- This assumes the teams list is complete each time --//

            // Add teams to the database if they don't already exist
            var nonExistentTeams = tournament.Settings.Teams.Where(x => !Teams.Any(y => !y.Old && y.Guid == x.Guid));
            foreach (var team in nonExistentTeams)
            {
                Teams.Add(new TeamDatabaseModel
                {
                    Guid = team.Guid,
                    TournamentId = tournament.Guid,
                    Name = team.Name,
                    Image = Convert.ToBase64String(team.Image),
                });
            }

            // Mark all teams for this Tournament as old if they're no longer in the model
            foreach (var x in Teams.AsEnumerable().Where(x => x.TournamentId == tournament.Guid && !tournament.Settings.Teams.Any(y => y.Guid == x.Guid)))
            {
                x.Old = true;
            }

            // -- Handle Map Pool changes -- //

            // Check for removed Pools
            foreach (var pool in Pools.AsEnumerable().Where(x => x.TournamentId == tournament.Guid && !tournament.Settings.Pools.Any(y => y.Guid == x.Guid)))
            {
                pool.Old = true;

                // Also mark each pool's songs as old
                foreach (var song in PoolSongs.AsEnumerable().Where(x => !x.Old && x.PoolId == pool.Guid))
                {
                    song.Old = true;
                }
            }

            // Check for added or changed pools
            foreach (var modelPool in tournament.Settings.Pools)
            {
                var poolDatabaseModel = new PoolDatabaseModel
                {
                    Guid = modelPool.Guid,
                    TournamentId = tournament.Guid,
                    Name = modelPool.Name,
                };

                var existingPool = Pools.FirstOrDefault(x => !x.Old && x.Guid == modelPool.Guid);
                if (existingPool != null)
                {
                    poolDatabaseModel.ID = existingPool.ID;
                    Entry(existingPool).CurrentValues.SetValues(poolDatabaseModel);
                }
                else
                {
                    Pools.Add(poolDatabaseModel);
                }

                //Check for removed songs
                foreach (var databaseSong in PoolSongs.AsQueryable().Where(x => !x.Old && x.PoolId == modelPool.Guid))
                {
                    if (!modelPool.Maps.Any(x => databaseSong.Guid == x.Guid))
                    {
                        databaseSong.Old = true;
                    }
                }

                //Check for newly added or updated songs
                foreach (var modelSong in modelPool.Maps)
                {
                    var poolSongDatabaseModel = new PoolSongDatabaseModel
                    {
                        Guid = modelSong.Guid,
                        PoolId = modelPool.Guid,
                        LevelId = modelSong.GameplayParameters.Beatmap.LevelId,
                        Name = modelSong.GameplayParameters.Beatmap.Name,
                        Characteristic = modelSong.GameplayParameters.Beatmap.Characteristic.SerializedName,
                        BeatmapDifficulty = modelSong.GameplayParameters.Beatmap.Difficulty,
                        GameOptions = (int)modelSong.GameplayParameters.GameplayModifiers.Options,
                        PlayerOptions = (int)modelSong.GameplayParameters.PlayerSettings.Options,
                        ShowScoreboard = modelSong.GameplayParameters.ShowScoreboard,
                        Attempts = modelSong.GameplayParameters.Attempts,
                        DisablePause = modelSong.GameplayParameters.DisablePause,
                        DisableFail = modelSong.GameplayParameters.DisableFail,
                        DisableScoresaberSubmission = modelSong.GameplayParameters.DisableScoresaberSubmission,
                        DisableCustomNotesOnStream = modelSong.GameplayParameters.DisableCustomNotesOnStream,
                    };

                    var existingPoolSong = PoolSongs.FirstOrDefault(x => !x.Old && x.Guid == modelSong.Guid);
                    if (existingPoolSong != null)
                    {
                        poolSongDatabaseModel.ID = existingPoolSong.ID;
                        Entry(existingPoolSong).CurrentValues.SetValues(poolSongDatabaseModel);
                    }
                    else
                    {
                        PoolSongs.Add(poolSongDatabaseModel);
                    }
                }
            }

            SaveChanges();
        }

        public async Task<TournamentProtobufModel> LoadModelFromDatabase(TournamentDatabaseModel tournamentDatabaseModel)
        {
            var tournamentProtobufModel = new TournamentProtobufModel
            {
                Guid = tournamentDatabaseModel.Guid,
                Settings = new TournamentProtobufModel.TournamentSettings
                {
                    TournamentName = tournamentDatabaseModel.Name,
                    TournamentImage = Convert.FromBase64String(tournamentDatabaseModel.Image),
                    EnableTeams = tournamentDatabaseModel.EnableTeams,
                    ScoreUpdateFrequency = tournamentDatabaseModel.ScoreUpdateFrequency,
                },
                Server = new CoreServer
                {
                    Address = tournamentDatabaseModel.ServerAddress,
                    Name = tournamentDatabaseModel.ServerName,
                    Port = int.Parse(tournamentDatabaseModel.ServerPort),
                    WebsocketPort = int.Parse(tournamentDatabaseModel.ServerWebsocketPort)
                }
            };

            tournamentProtobufModel.Settings.Teams.AddRange(
                await Teams.AsAsyncEnumerable()
                    .Where(x => !x.Old && x.TournamentId == tournamentDatabaseModel.Guid)
                    .Select(x =>
                        new TeamProtobufModel
                        {
                            Guid = x.Guid,
                            Name = x.Name,
                            Image = Convert.FromBase64String(x.Image)
                        })
                    .ToListAsync()
            );

            tournamentProtobufModel.Settings.Pools.AddRange(
                await Pools.AsAsyncEnumerable()
                    .Where(x => !x.Old && x.TournamentId == tournamentDatabaseModel.Guid)
                    .Select(x => 
                        new PoolProtobufModel
                        {
                            Guid = x.Guid,
                            Name = x.Name,
                        })
                    .ToListAsync()
            );

            foreach (var pool in tournamentProtobufModel.Settings.Pools)
            {
                pool.Maps.AddRange(
                    await PoolSongs.AsAsyncEnumerable()
                        .Where(x => !x.Old && x.PoolId == pool.Guid)
                        .Select(x =>
                            new PoolSongProtobufModel
                            {
                                Guid = x.Guid,
                                GameplayParameters = new GameplayParameters
                                {
                                    Beatmap = new Beatmap
                                    {
                                        LevelId = x.LevelId,
                                        Characteristic = new Characteristic
                                        {
                                            SerializedName = x.Characteristic
                                        },
                                        Difficulty = x.BeatmapDifficulty,
                                        Name = x.Name
                                    },
                                    GameplayModifiers = new GameplayModifiers
                                    {
                                        Options = (GameplayModifiers.GameOptions)x.GameOptions
                                    },
                                    PlayerSettings = new PlayerSpecificSettings
                                    {
                                        Options = (PlayerSpecificSettings.PlayerOptions)x.PlayerOptions
                                    },
                                    ShowScoreboard = x.ShowScoreboard,
                                    Attempts = x.Attempts,
                                    DisablePause = x.DisablePause,
                                    DisableFail = x.DisableFail,
                                    DisableScoresaberSubmission = x.DisableScoresaberSubmission,
                                    DisableCustomNotesOnStream = x.DisableCustomNotesOnStream,
                                },
                            }).ToArrayAsync() ?? new PoolSongProtobufModel[] { }
                );
            }

            tournamentProtobufModel.Settings.BannedMods.AddRange(tournamentDatabaseModel.BannedMods.Split(",").ToList());
            return tournamentProtobufModel;
        }

        public void DeleteFromDatabase(TournamentProtobufModel tournament)
        {
            foreach (var x in Tournaments.AsEnumerable().Where(x => x.Guid == tournament.Guid.ToString())) x.Old = true;
            foreach (var x in Teams.AsEnumerable().Where(x => x.TournamentId == tournament.Guid.ToString())) x.Old = true;
            foreach (var x in Pools.AsEnumerable().Where(x => x.TournamentId == tournament.Guid.ToString()))
            {
                x.Old = true;

                foreach (var y in PoolSongs.AsEnumerable().Where(y => y.PoolId == x.Guid))
                {
                    y.Old = true;
                }
            }
            SaveChanges();
        }

        public async Task<bool> VerifyHashedPassword(string tournamentId, string hashedPassword)
        {
            var tournament = await Tournaments.AsAsyncEnumerable().FirstOrDefaultAsync(x => x.Guid == tournamentId);

            if (tournament == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(hashedPassword) && string.IsNullOrWhiteSpace(tournament.HashedPassword))
            {
                return true;
            }

            //TODO: Actual hashing please, this is testing-only
            return tournament.HashedPassword == hashedPassword;
        }
    }
}