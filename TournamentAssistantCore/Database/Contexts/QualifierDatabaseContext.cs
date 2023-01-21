using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantCore.Database.Models;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Discord;
using QualifierDatabaseModel = TournamentAssistantCore.Database.Models.Qualifier;
using QualifierProtobufModel = TournamentAssistantShared.Models.QualifierEvent;
using Tournament = TournamentAssistantShared.Models.Tournament;

namespace TournamentAssistantCore.Database.Contexts
{
    public class QualifierDatabaseContext : DatabaseContext
    {
        public QualifierDatabaseContext(string location) : base(location) { }

        public DbSet<Song> Songs { get; set; }
        public DbSet<Score> Scores { get; set; }
        public DbSet<QualifierDatabaseModel> Qualifiers { get; set; }

        public async Task SaveModelToDatabase(QualifierProtobufModel @event)
        {
            //If it already exists, update it, if not add it
            var databaseModel = new QualifierDatabaseModel
            {
                Guid = @event.Guid.ToString(),
                GuildId = @event.Guild.Id,
                GuildName = @event.Guild.Name,
                Name = @event.Name,
                InfoChannelId = @event.InfoChannel?.Id ?? 0UL,
                InfoChannelName = @event.InfoChannel?.Name ?? "",
                Flags = @event.Flags
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
            foreach (var song in Songs.AsQueryable().Where(x => x.EventId == @event.Guid.ToString() && !x.Old))
            {
                if (!@event.QualifierMaps.Any(x => song.LevelId == x.Beatmap.LevelId &&
                                                           song.Characteristic ==
                                                           x.Beatmap.Characteristic.SerializedName &&
                                                           song.BeatmapDifficulty == x.Beatmap.Difficulty &&
                                                           song.GameOptions == (int)x.GameplayModifiers.Options &&
                                                           song.PlayerOptions == (int)x.PlayerSettings.Options))
                {
                    song.Old = true;
                }
            }

            //Check for newly added songs
            foreach (var song in @event.QualifierMaps)
            {
                if (!Songs.Any(x => !x.Old &&
                                             x.EventId == @event.Guid.ToString() &&
                                             x.LevelId == song.Beatmap.LevelId &&
                                             x.Characteristic == song.Beatmap.Characteristic.SerializedName &&
                                             x.BeatmapDifficulty == song.Beatmap.Difficulty &&
                                             x.GameOptions == (int)song.GameplayModifiers.Options &&
                                             x.PlayerOptions == (int)song.PlayerSettings.Options))
                {
                    Songs.Add(new Song
                    {
                        EventId = @event.Guid.ToString(),
                        LevelId = song.Beatmap.LevelId,
                        Name = song.Beatmap.Name,
                        Characteristic = song.Beatmap.Characteristic.SerializedName,
                        BeatmapDifficulty = song.Beatmap.Difficulty,
                        GameOptions = (int)song.GameplayModifiers.Options,
                        PlayerOptions = (int)song.PlayerSettings.Options
                    });
                }
            }

            await SaveChangesAsync();
        }

        public async Task DeleteFromDatabase(QualifierProtobufModel @event)
        {
            await Qualifiers.AsAsyncEnumerable().Where(x => x.Guid == @event.Guid.ToString()).ForEachAsync(x => x.Old = true);
            await Songs.AsAsyncEnumerable().Where(x => x.EventId == @event.Guid.ToString()).ForEachAsync(x => x.Old = true);
            await Scores.AsAsyncEnumerable().Where(x => x.EventId == @event.Guid.ToString()).ForEachAsync(x => x.Old = true);
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
                    Guild = new Guild
                    {
                        Id = @event.GuildId,
                        Name = @event.GuildName
                    },
                    Name = @event.Name,
                    InfoChannel = new Channel
                    {
                        Id = @event.InfoChannelId,
                        Name = @event.InfoChannelName
                    },
                    SendScoresToInfoChannel = @event.InfoChannelId != 0UL,
                    Flags = @event.Flags
                };

                qualifierEvent.QualifierMaps.AddRange(
                    await Songs.AsAsyncEnumerable().Where(x => !x.Old && x.EventId == @event.Guid).Select(x => new GameplayParameters
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
                    }).ToArrayAsync() ?? new GameplayParameters[] { });
            }
            
            return ret;
        }
    }
}