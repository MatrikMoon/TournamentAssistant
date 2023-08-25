using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.Database.Models;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Discord;
using QualifierDatabaseModel = TournamentAssistantServer.Database.Models.Qualifier;
using QualifierProtobufModel = TournamentAssistantShared.Models.QualifierEvent;
using Tournament = TournamentAssistantShared.Models.Tournament;

namespace TournamentAssistantServer.Database.Contexts
{
    public class QualifierDatabaseContext : DatabaseContext
    {
        public QualifierDatabaseContext(string location) : base(location) { }

        public DbSet<Song> Songs { get; set; }
        public DbSet<Score> Scores { get; set; }
        public DbSet<QualifierDatabaseModel> Qualifiers { get; set; }

        public async Task SaveModelToDatabase(string tournamentId, QualifierProtobufModel @event)
        {
            //If it already exists, update it, if not add it
            var databaseModel = new QualifierDatabaseModel
            {
                Guid = @event.Guid.ToString(),
                Name = @event.Name,
                Image = Convert.ToBase64String(@event.Image),
                TournamentId = tournamentId,
                GuildId = @event.Guild.Id,
                GuildName = @event.Guild.Name,
                InfoChannelId = @event.InfoChannel?.Id ?? 0UL,
                InfoChannelName = @event.InfoChannel?.Name ?? "",
                Flags = (int)@event.Flags
            };

            var existingQualifier = Qualifiers.FirstOrDefault(x => !x.Old && x.Guid == @event.Guid);
            if (existingQualifier != null)
            {
                Entry(existingQualifier).CurrentValues.SetValues(databaseModel);
            }
            else
            {
                await Qualifiers.AddAsync(databaseModel);
            }

            //Check for removed songs
            foreach (var databaseSong in Songs.AsQueryable().Where(x => x.EventId == @event.Guid.ToString() && !x.Old))
            {
                if (!@event.QualifierMaps.Any(x => databaseSong.Guid == x.Guid))
                {
                    databaseSong.Old = true;
                }
            }

            //Check for newly added songs
            foreach (var modelSong in @event.QualifierMaps)
            {
                if (!Songs.Any(x => !x.Old && x.Guid == modelSong.Guid))
                {
                    Songs.Add(new Song
                    {
                        Guid = modelSong.Guid,
                        EventId = @event.Guid.ToString(),
                        LevelId = modelSong.GameplayParameters.Beatmap.LevelId,
                        Name = modelSong.GameplayParameters.Beatmap.Name,
                        Characteristic = modelSong.GameplayParameters.Beatmap.Characteristic.SerializedName,
                        BeatmapDifficulty = modelSong.GameplayParameters.Beatmap.Difficulty,
                        GameOptions = (int)modelSong.GameplayParameters.GameplayModifiers.Options,
                        PlayerOptions = (int)modelSong.GameplayParameters.PlayerSettings.Options,
                        Attempts = modelSong.Attempts,
                        DisablePause = modelSong.DisablePause
                    });
                }
            }

            await SaveChangesAsync();
        }

        public async Task<List<QualifierProtobufModel>> LoadModelsFromDatabase(Tournament tournament)
        {
            var events = Qualifiers.AsQueryable().Where(x => !x.Old && x.TournamentId == tournament.Guid);
            var ret = new List<QualifierProtobufModel>();

            foreach (var @event in events)
            {
                var qualifierEvent = new QualifierProtobufModel
                {
                    Guid = @event.Guid,
                    Name = @event.Name,
                    Image = Convert.FromBase64String(@event.Image),
                    Guild = new Guild
                    {
                        Id = @event.GuildId,
                        Name = @event.GuildName
                    },
                    InfoChannel = new Channel
                    {
                        Id = @event.InfoChannelId,
                        Name = @event.InfoChannelName
                    },
                    SendScoresToInfoChannel = @event.InfoChannelId != 0UL,
                    Flags = (QualifierProtobufModel.EventSettings)@event.Flags
                };

                qualifierEvent.QualifierMaps.AddRange(
                    await Songs.AsAsyncEnumerable().Where(x => !x.Old && x.EventId == @event.Guid).Select(x => new QualifierProtobufModel.QualifierMap
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
                            }
                        },
                        Attempts = x.Attempts,
                        DisablePause = x.DisablePause
                    }).ToArrayAsync() ?? new QualifierProtobufModel.QualifierMap[] { });

                ret.Add(qualifierEvent);
            }

            return ret;
        }

        public async Task DeleteFromDatabase(QualifierProtobufModel @event)
        {
            await Qualifiers.AsAsyncEnumerable().Where(x => x.Guid == @event.Guid.ToString()).ForEachAsync(x => x.Old = true);
            await Songs.AsAsyncEnumerable().Where(x => x.EventId == @event.Guid.ToString()).ForEachAsync(x => x.Old = true);
            await Scores.AsAsyncEnumerable().Where(x => x.EventId == @event.Guid.ToString()).ForEachAsync(x => x.Old = true);
            await SaveChangesAsync();
        }
    }
}