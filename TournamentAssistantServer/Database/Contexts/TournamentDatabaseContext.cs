using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;
using TeamDatabaseModel = TournamentAssistantServer.Database.Models.Team;
using TeamProtobufModel = TournamentAssistantShared.Models.Team;
using TournamentDatabaseModel = TournamentAssistantServer.Database.Models.Tournament;
using TournamentProtobufModel = TournamentAssistantShared.Models.Tournament;

namespace TournamentAssistantServer.Database.Contexts
{
    public class TournamentDatabaseContext : DatabaseContext
    {
        public TournamentDatabaseContext(string location) : base(location) { }

        public DbSet<TournamentDatabaseModel> Tournaments { get; set; }
        public DbSet<TeamDatabaseModel> Teams { get; set; }

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

            //Add teams to the database if they don't already exist
            var nonExistentTeams = tournament.Settings.Teams.Where(x => !Teams.Any(y => !y.Old && y.Guid == x.Guid));
            foreach (var team in nonExistentTeams)
            {
                Teams.Add(new TeamDatabaseModel
                {
                    Guid = team.Guid,
                    TournamentId = tournament.Guid,
                    Name = team.Name,
                });
            }

            //Mark all teams for this Tournament as old if they're no longer in the model
            foreach (var x in Teams.AsEnumerable().Where(x => x.TournamentId == tournament.Guid && !tournament.Settings.Teams.Any(y => y.Guid == x.Guid)))
            {
                x.Old = true;
            }

            SaveChanges();
        }

        public async Task<TournamentProtobufModel> LoadModelFromDatabase(TournamentDatabaseModel tournament)
        {
            var qualifierEvent = new TournamentProtobufModel
            {
                Guid = tournament.Guid,
                Settings = new TournamentProtobufModel.TournamentSettings
                {
                    TournamentName = tournament.Name,
                    TournamentImage = Convert.FromBase64String(tournament.Image),
                    EnableTeams = tournament.EnableTeams,
                    ScoreUpdateFrequency = tournament.ScoreUpdateFrequency,
                },
                Server = new CoreServer
                {
                    Address = tournament.ServerAddress,
                    Name = tournament.ServerName,
                    Port = int.Parse(tournament.ServerPort),
                    WebsocketPort = int.Parse(tournament.ServerWebsocketPort)
                }
            };

            qualifierEvent.Settings.Teams.AddRange(
                await Teams.AsAsyncEnumerable()
                    .Where(x => x.TournamentId == tournament.Guid)
                    .Select(x =>
                        new TeamProtobufModel
                        {
                            Guid = x.Guid,
                            Name = x.Name
                        })
                    .ToListAsync()
            );

            qualifierEvent.Settings.BannedMods.AddRange(tournament.BannedMods.Split(",").ToList());
            return qualifierEvent;
        }

        public void DeleteFromDatabase(TournamentProtobufModel tournament)
        {
            foreach (var x in Tournaments.AsEnumerable().Where(x => x.Guid == tournament.Guid.ToString())) x.Old = true;
            foreach (var x in Teams.AsEnumerable().Where(x => x.TournamentId == tournament.Guid.ToString())) x.Old = true;
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