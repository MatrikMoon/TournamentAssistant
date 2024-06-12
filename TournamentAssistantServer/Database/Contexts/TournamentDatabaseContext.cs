using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;
using AuthorizedUsersDatabaseModel = TournamentAssistantServer.Database.Models.AuthorizedUser;
using PoolDatabaseModel = TournamentAssistantServer.Database.Models.Pool;
using PoolProtobufModel = TournamentAssistantShared.Models.Tournament.TournamentSettings.Pool;
using PoolSongDatabaseModel = TournamentAssistantServer.Database.Models.PoolSong;
using PoolSongProtobufModel = TournamentAssistantShared.Models.Map;
using TeamDatabaseModel = TournamentAssistantServer.Database.Models.Team;
using TeamProtobufModel = TournamentAssistantShared.Models.Tournament.TournamentSettings.Team;
using TournamentDatabaseModel = TournamentAssistantServer.Database.Models.Tournament;
using TournamentProtobufModel = TournamentAssistantShared.Models.Tournament;

namespace TournamentAssistantServer.Database.Contexts
{
    public class TournamentDatabaseContext : DatabaseContext
    {
        public TournamentDatabaseContext() : base("files/TournamentDatabase.db") { }

        public DbSet<TournamentDatabaseModel> Tournaments { get; set; }
        public DbSet<AuthorizedUsersDatabaseModel> AuthorizedUsers { get; set; }
        public DbSet<TeamDatabaseModel> Teams { get; set; }
        public DbSet<PoolDatabaseModel> Pools { get; set; }
        public DbSet<PoolSongDatabaseModel> PoolSongs { get; set; }

        public void SaveNewModelToDatabase(TournamentProtobufModel tournament)
        {
            var databaseModel = new TournamentDatabaseModel
            {
                Guid = tournament.Guid,
                Name = tournament.Settings.TournamentName,
                Image = Convert.ToBase64String(tournament.Settings.TournamentImage),
                EnableTeams = tournament.Settings.EnableTeams,
                EnablePools = tournament.Settings.EnablePools,
                ShowTournamentButton = tournament.Settings.ShowTournamentButton,
                ShowQualifierButton = tournament.Settings.ShowQualifierButton,
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

            // -- This assumes the teams list is complete each time -- //

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
                    Image = Convert.ToBase64String(modelPool.Image),
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

                // Check for removed songs
                foreach (var databaseSong in PoolSongs.AsQueryable().Where(x => !x.Old && x.PoolId == modelPool.Guid))
                {
                    if (!modelPool.Maps.Any(x => databaseSong.Guid == x.Guid))
                    {
                        databaseSong.Old = true;
                    }
                }

                // Check for newly added or updated songs
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

        public void AddAuthorizedUser(TournamentProtobufModel tournament, string discordId, Permissions permission)
        {
            // Remove existing user if applicable
            var existingAuthorizedUser = AuthorizedUsers.FirstOrDefault(x => !x.Old && x.TournamentId == tournament.Guid && x.DiscordId == discordId);
            if (existingAuthorizedUser != null)
            {
                existingAuthorizedUser.Old = true;
            }

            AuthorizedUsers.Add(new AuthorizedUsersDatabaseModel
            {
                Guid = Guid.NewGuid().ToString(),
                TournamentId = tournament.Guid,
                DiscordId = discordId,
                PermissionFlags = (int)permission,
            });

            SaveChanges();
        }

        public void AddAuthorizedUserPermission(TournamentProtobufModel tournament, string discordId, Permissions permission)
        {
            var existingAuthorizedUser = AuthorizedUsers.First(x => !x.Old && x.TournamentId == tournament.Guid && x.DiscordId == discordId);

            var newPermissions = (Permissions)existingAuthorizedUser.PermissionFlags;
            newPermissions |= permission;

            Entry(existingAuthorizedUser).CurrentValues.SetValues(new AuthorizedUsersDatabaseModel
            {
                ID = existingAuthorizedUser.ID,
                Guid = existingAuthorizedUser.Guid,
                TournamentId = existingAuthorizedUser.TournamentId,
                DiscordId = existingAuthorizedUser.DiscordId,
                PermissionFlags = (int)permission,
            });

            SaveChanges();
        }

        public void RemoveAuthorizedUserPermission(TournamentProtobufModel tournament, string discordId, Permissions permission)
        {
            var existingAuthorizedUser = AuthorizedUsers.First(x => !x.Old && x.TournamentId == tournament.Guid && x.DiscordId == discordId);
            
            var newPermissions = (Permissions)existingAuthorizedUser.PermissionFlags;
            newPermissions &= ~permission;

            Entry(existingAuthorizedUser).CurrentValues.SetValues(new AuthorizedUsersDatabaseModel
            {
                ID = existingAuthorizedUser.ID,
                Guid = existingAuthorizedUser.Guid,
                TournamentId = existingAuthorizedUser.TournamentId,
                DiscordId = existingAuthorizedUser.DiscordId,
                PermissionFlags = (int)permission,
            });

            SaveChanges();
        }

        public void RemoveAuthorizedUser(TournamentProtobufModel tournament, string discordId)
        {
            var existingAuthorizedUser = AuthorizedUsers.FirstOrDefault(x => !x.Old && x.TournamentId == tournament.Guid && x.DiscordId == discordId);
            existingAuthorizedUser.Old = true;

            SaveChanges();
        }

        public Permissions GetUserPermission(string tournamentId, string discordId)
        {
            var authorization = AuthorizedUsers.FirstOrDefault(x => !x.Old && x.TournamentId == tournamentId && x.DiscordId == discordId);
            if (authorization == null)
            {
                return Permissions.None;
            }

            return (Permissions)authorization.PermissionFlags;
        }

        public bool IsUserAuthorized(string tournamentId, string discordId, Permissions permission)
        {
            return GetUserPermission(tournamentId, discordId).HasFlag(permission);
        }

        public void UpdateTournamentSettings(TournamentProtobufModel tournament)
        {
            var existingTournament = Tournaments.First(x => !x.Old && x.Guid == tournament.Guid);
            Entry(existingTournament).CurrentValues.SetValues(new TournamentDatabaseModel
            {
                ID = existingTournament.ID,
                Guid = tournament.Guid,
                Name = tournament.Settings.TournamentName,
                Image = Convert.ToBase64String(tournament.Settings.TournamentImage),
                EnableTeams = tournament.Settings.EnableTeams,
                EnablePools = tournament.Settings.EnablePools,
                ShowTournamentButton = tournament.Settings.ShowTournamentButton,
                ShowQualifierButton = tournament.Settings.ShowQualifierButton,
                ScoreUpdateFrequency = tournament.Settings.ScoreUpdateFrequency,
                BannedMods = string.Join(",", tournament.Settings.BannedMods),
                ServerAddress = tournament.Server.Address,
                ServerName = tournament.Server.Name,
                ServerPort = tournament.Server.Port.ToString(),
                ServerWebsocketPort = tournament.Server.WebsocketPort.ToString(),
            });

            SaveChanges();
        }

        public void AddTeam(TournamentProtobufModel tournament, TeamProtobufModel team)
        {
            Teams.Add(new TeamDatabaseModel
            {
                Guid = team.Guid,
                TournamentId = tournament.Guid,
                Name = team.Name,
                Image = Convert.ToBase64String(team.Image),
            });

            SaveChanges();
        }

        public void UpdateTeam(TournamentProtobufModel tournament, TeamProtobufModel team)
        {
            var existingTeam = Teams.First(x => !x.Old && x.Guid == team.Guid);
            Entry(existingTeam).CurrentValues.SetValues(new TeamDatabaseModel
            {
                ID = existingTeam.ID,
                Guid = tournament.Guid,
                TournamentId = tournament.Guid,
                Name = tournament.Settings.TournamentName,
                Image = Convert.ToBase64String(tournament.Settings.TournamentImage),
            });

            SaveChanges();
        }

        public void RemoveTeam(TournamentProtobufModel tournament, TeamProtobufModel team)
        {
            var existingTeam = Teams.FirstOrDefault(x => x.TournamentId == tournament.Guid && x.Guid == team.Guid);
            existingTeam.Old = true;

            SaveChanges();
        }

        public void AddPool(TournamentProtobufModel tournament, PoolProtobufModel pool)
        {
            Pools.Add(new PoolDatabaseModel
            {
                Guid = pool.Guid,
                TournamentId = tournament.Guid,
                Name = pool.Name,
                Image = Convert.ToBase64String(pool.Image),
            });

            SaveChanges();
        }

        public void UpdatePool(TournamentProtobufModel tournament, PoolProtobufModel pool)
        {
            var existingPool = Pools.First(x => !x.Old && x.Guid == pool.Guid);
            Entry(existingPool).CurrentValues.SetValues(new PoolDatabaseModel
            {
                ID = existingPool.ID,
                Guid = pool.Guid,
                TournamentId = tournament.Guid,
                Name = pool.Name,
                Image = Convert.ToBase64String(tournament.Settings.TournamentImage),
            });

            SaveChanges();
        }

        public void RemovePool(TournamentProtobufModel tournament, PoolProtobufModel pool)
        {
            var existingPool = Pools.FirstOrDefault(x => x.TournamentId == tournament.Guid && x.Guid == pool.Guid);
            existingPool.Old = true;

            // Mark all the pool's songs as old too
            foreach (var x in PoolSongs.AsEnumerable().Where(x => x.PoolId == pool.Guid))
            {
                x.Old = true;
            }

            SaveChanges();
        }

        public void AddPoolSong(PoolProtobufModel pool, PoolSongProtobufModel poolSong)
        {
            PoolSongs.Add(new PoolSongDatabaseModel
            {
                Guid = poolSong.Guid,
                PoolId = pool.Guid,
                LevelId = poolSong.GameplayParameters.Beatmap.LevelId,
                Name = poolSong.GameplayParameters.Beatmap.Name,
                Characteristic = poolSong.GameplayParameters.Beatmap.Characteristic.SerializedName,
                BeatmapDifficulty = poolSong.GameplayParameters.Beatmap.Difficulty,
                GameOptions = (int)poolSong.GameplayParameters.GameplayModifiers.Options,
                PlayerOptions = (int)poolSong.GameplayParameters.PlayerSettings.Options,
                ShowScoreboard = poolSong.GameplayParameters.ShowScoreboard,
                Attempts = poolSong.GameplayParameters.Attempts,
                DisablePause = poolSong.GameplayParameters.DisablePause,
                DisableFail = poolSong.GameplayParameters.DisableFail,
                DisableScoresaberSubmission = poolSong.GameplayParameters.DisableScoresaberSubmission,
                DisableCustomNotesOnStream = poolSong.GameplayParameters.DisableCustomNotesOnStream,
            });

            SaveChanges();
        }

        public void UpdatePoolSong(PoolProtobufModel pool, PoolSongProtobufModel poolSong)
        {
            var existingPoolSong = PoolSongs.FirstOrDefault(x => !x.Old && x.Guid == poolSong.Guid);
            Entry(existingPoolSong).CurrentValues.SetValues(new PoolSongDatabaseModel
            {
                ID = existingPoolSong.ID,
                Guid = poolSong.Guid,
                PoolId = pool.Guid,
                LevelId = poolSong.GameplayParameters.Beatmap.LevelId,
                Name = poolSong.GameplayParameters.Beatmap.Name,
                Characteristic = poolSong.GameplayParameters.Beatmap.Characteristic.SerializedName,
                BeatmapDifficulty = poolSong.GameplayParameters.Beatmap.Difficulty,
                GameOptions = (int)poolSong.GameplayParameters.GameplayModifiers.Options,
                PlayerOptions = (int)poolSong.GameplayParameters.PlayerSettings.Options,
                ShowScoreboard = poolSong.GameplayParameters.ShowScoreboard,
                Attempts = poolSong.GameplayParameters.Attempts,
                DisablePause = poolSong.GameplayParameters.DisablePause,
                DisableFail = poolSong.GameplayParameters.DisableFail,
                DisableScoresaberSubmission = poolSong.GameplayParameters.DisableScoresaberSubmission,
                DisableCustomNotesOnStream = poolSong.GameplayParameters.DisableCustomNotesOnStream,
            });

            SaveChanges();
        }

        public void RemovePoolSong(PoolSongProtobufModel poolSong)
        {
            var existingPoolSong = PoolSongs.FirstOrDefault(x => !x.Old && x.Guid == poolSong.Guid);
            existingPoolSong.Old = true;

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
                    EnablePools = tournamentDatabaseModel.EnablePools,
                    ShowTournamentButton = tournamentDatabaseModel.ShowTournamentButton,
                    ShowQualifierButton = tournamentDatabaseModel.ShowQualifierButton,
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
                            Image = Convert.FromBase64String(x.Image)
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

            if (!string.IsNullOrEmpty(tournamentDatabaseModel.BannedMods))
            {
                tournamentProtobufModel.Settings.BannedMods.AddRange(tournamentDatabaseModel.BannedMods.Split(",").ToList());
            }
            return tournamentProtobufModel;
        }

        public void DeleteFromDatabase(string tournamentId)
        {
            foreach (var x in Tournaments.AsEnumerable().Where(x => x.Guid == tournamentId)) x.Old = true;
            foreach (var x in Teams.AsEnumerable().Where(x => x.TournamentId == tournamentId)) x.Old = true;
            foreach (var x in Pools.AsEnumerable().Where(x => x.TournamentId == tournamentId))
            {
                x.Old = true;

                foreach (var y in PoolSongs.AsEnumerable().Where(y => y.PoolId == x.Guid))
                {
                    y.Old = true;
                }
            }
            SaveChanges();
        }

        public bool VerifyHashedPassword(string tournamentId, string hashedPassword)
        {
            var tournament = Tournaments.AsEnumerable().FirstOrDefault(x => x.Guid == tournamentId);

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