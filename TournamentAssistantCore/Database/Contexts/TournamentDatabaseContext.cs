using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantCore.Database.Models;
using TeamDatabaseModel = TournamentAssistantCore.Database.Models.Team;
using TeamProtobufModel = TournamentAssistantShared.Models.Team;
using TournamentDatabaseModel = TournamentAssistantCore.Database.Models.Tournament;
using TournamentProtobufModel = TournamentAssistantShared.Models.Tournament;

namespace TournamentAssistantCore.Database.Contexts
{
    public class TournamentDatabaseContext : DatabaseContext
    {
        public TournamentDatabaseContext(string location) : base(location) { }

        public DbSet<TournamentDatabaseModel> Tournaments { get; set; }
        public DbSet<TeamDatabaseModel> Teams { get; set; }

        public async Task SaveModelToDatabase(TournamentProtobufModel tournament)
        {
            var databaseModel = new TournamentDatabaseModel
            {
                Guid = tournament.Guid,
                Name = tournament.Settings.TournamentName,
                Image = Convert.ToBase64String(tournament.Settings.TournamentImage),
                EnableTeams = tournament.Settings.EnableTeams,
                ScoreUpdateFrequency = tournament.Settings.ScoreUpdateFrequency,
                BannedMods = string.Join(",", tournament.Settings.BannedMods)
            };

            var existingTournament = Tournaments.FirstOrDefault(x => !x.Old && x.Guid == tournament.Guid);
            if (existingTournament != null)
            {
                Entry(existingTournament).CurrentValues.SetValues(databaseModel);
            }
            else
            {
                await Tournaments.AddAsync(databaseModel);
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
            await Teams.AsAsyncEnumerable()
                .Where(x => 
                    x.TournamentId == tournament.Guid && 
                    !tournament.Settings.Teams.Any(y => y.Guid == x.Guid))
                .ForEachAsync(x => x.Old = true);

            await SaveChangesAsync();
        }

        public async Task<TournamentProtobufModel> LoadModelFromDatabase(TournamentDatabaseModel tournament)
        {
            var qualifierEvent = new TournamentProtobufModel
            {
                Guid = tournament.Guid,
                Settings =
                {
                    TournamentName = tournament.Name,
                    TournamentImage = Convert.FromBase64String(tournament.Image),
                    EnableTeams = tournament.EnableTeams,
                    ScoreUpdateFrequency = tournament.ScoreUpdateFrequency,
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

        public async Task DeleteFromDatabase(TournamentProtobufModel tournament)
        {
            await Tournaments.AsAsyncEnumerable().Where(x => x.Guid == tournament.Guid.ToString()).ForEachAsync(x => x.Old = true);
            await Teams.AsAsyncEnumerable().Where(x => x.TournamentId == tournament.Guid.ToString()).ForEachAsync(x => x.Old = true);
            await SaveChangesAsync();
        }

        public async Task<bool> VerifyHashedPassword(string tournamentId, string hashedPassword)
        {
            var tournament = await Tournaments.AsAsyncEnumerable().FirstOrDefaultAsync(x => x.Guid == tournamentId);
            
            if (tournament == null)
            {
                return false;
            }

            if (hashedPassword == null && string.IsNullOrWhiteSpace(tournament.HashedPassword))
            {
                return true;
            }

            //TODO: Actual hashing please, this is testing-only
            return tournament.HashedPassword == hashedPassword;
        }
    }
}