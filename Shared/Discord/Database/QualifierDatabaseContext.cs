using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using TournamentAssistantShared.Database;
using TournamentAssistantShared.Discord.Helpers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantShared.Discord.Database
{
    public class QualifierDatabaseContext : DatabaseContext
    {
        public event Func<QualifierEvent, Response> QualifierEventCreated;
        public event Func<QualifierEvent, Response> QualifierEventUpdated;
        public event Func<QualifierEvent, Response> QualifierEventDeleted;

        public QualifierDatabaseContext(string location) : base(location) { }

        public DbSet<Song> Songs { get; set; }
        public DbSet<Score> Scores { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Player> Players { get; set; }

        public Response RaiseEventAdded(Event @event) => QualifierEventCreated?.Invoke(ConvertDatabaseToModel(@event));

        public Response RaiseEventUpdated(Event @event) => QualifierEventUpdated?.Invoke(ConvertDatabaseToModel(@event));

        public Response RaiseEventRemoved(Event @event) => QualifierEventDeleted?.Invoke(ConvertDatabaseToModel(@event));

        public Event ConvertModelToDatabase(QualifierEvent qualifierEvent)
        {
            return new Event
            {
                EventId = qualifierEvent.EventId.ToString(),
                GuildId = qualifierEvent.Guild.Id,
                GuildName = qualifierEvent.Guild.Name,
                Name = qualifierEvent.Name,
                InfoChannelId = qualifierEvent.InfoChannel?.Id ?? 0,
                InfoChannelName = qualifierEvent.InfoChannel?.Name ?? ""
            };
        }

        public QualifierEvent ConvertDatabaseToModel(Event @event)
        {
            var songs = Songs.Where(x => !x.Old);

            return new QualifierEvent
            {
                EventId = Guid.Parse(@event.EventId),
                Guild = new Models.Discord.Guild
                {
                    Id = @event.GuildId,
                    Name = @event.GuildName
                },
                Name = @event.Name,
                InfoChannel = new Models.Discord.Channel
                {
                    Id = @event.InfoChannelId,
                    Name = @event.InfoChannelName
                },
                ShowScores = @event.InfoChannelId != 0,
                QualifierMaps = songs.Where(y => !y.Old && y.EventId == @event.EventId).Select(y => new GameplayParameters
                {
                    Beatmap = new Beatmap
                    {
                        LevelId = y.LevelId,
                        Characteristic = new Characteristic
                        {
                            SerializedName = y.Characteristic
                        },
                        Difficulty = (SharedConstructs.BeatmapDifficulty)y.BeatmapDifficulty
                    },
                    PlayerSettings = new PlayerSpecificSettings
                    {
                        Options = (PlayerSpecificSettings.PlayerOptions)y.PlayerOptions
                    },
                    GameplayModifiers = new GameplayModifiers
                    {
                        Options = (GameplayModifiers.GameOptions)y.GameOptions
                    }
                }).ToArray()
            };
        }
    }
}
