using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using TournamentAssistantCore.Database;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Discord;

namespace TournamentAssistantCore.Discord.Database
{
    public class QualifierDatabaseContext : DatabaseContext
    {
        public QualifierDatabaseContext(string location) : base(location) { }

        public DbSet<Song> Songs { get; set; }
        public DbSet<Score> Scores { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Player> Players { get; set; }

        public Event ConvertModelToEventDatabase(QualifierEvent qualifierEvent)
        {
            return new Event
            {
                EventId = qualifierEvent.EventId.ToString(),
                GuildId = qualifierEvent.Guild.Id,
                GuildName = qualifierEvent.Guild.Name,
                Name = qualifierEvent.Name,
                InfoChannelId = qualifierEvent.InfoChannel?.Id ?? 0,
                InfoChannelName = qualifierEvent.InfoChannel?.Name ?? "",
                Flags = qualifierEvent.Flags
            };
        }

        //knownHostStates is only nullable if you can guarantee there are no songs attached to the event:
        //ie: on event creation
        public QualifierEvent ConvertDatabaseToModel(GameplayParameters[] songs, Event @event)
        {
            return new QualifierEvent
            {
                EventId = Guid.Parse(@event.EventId),
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
                SendScoresToInfoChannel = @event.InfoChannelId != 0,
                Flags = @event.Flags,
                QualifierMaps = songs?.ToArray() ?? new GameplayParameters[] { }
            };
        }
    }
}