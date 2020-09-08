using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using TournamentAssistantShared.Database;
using TournamentAssistantShared.Discord.Helpers;
using TournamentAssistantShared.Models;

namespace TournamentAssistantShared.Discord.Database
{
    public class QualifierDatabaseContext : DatabaseContext
    {
        public event Action<QualifierEvent> QualifierEventCreated;
        public event Action<QualifierEvent> QualifierEventUpdated;
        public event Action<QualifierEvent> QualifierEventDeleted;

        public QualifierDatabaseContext(string location) : base(location) { }

        public DbSet<Song> Songs { get; set; }
        public DbSet<Score> Scores { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Player> Players { get; set; }

        public void RaiseEventAdded(Event @event) => QualifierEventCreated?.Invoke(ConvertDatabaseToModel(@event));

        public void RaiseEventUpdated(Event @event) => QualifierEventUpdated?.Invoke(ConvertDatabaseToModel(@event));

        public void RaiseEventRemoved(Event @event) => QualifierEventDeleted?.Invoke(ConvertDatabaseToModel(@event));

        public QualifierEvent ConvertDatabaseToModel(Event @event)
        {
            var songs = Songs.Where(x => !x.Old);

            return new QualifierEvent
            {
                EventId = @event.ID,
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
