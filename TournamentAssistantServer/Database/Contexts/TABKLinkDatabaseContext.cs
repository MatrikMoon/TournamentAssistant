using System.Linq;
using Microsoft.EntityFrameworkCore;
using TABKLinkModel = TournamentAssistantServer.Database.Models.TABKLink;

namespace TournamentAssistantServer.Database.Contexts
{
    public class TABKLinkDatabaseContext : DatabaseContext
    {
        public TABKLinkDatabaseContext()
            : base("files/TABKLinkDatabase.db") { }

        public DbSet<TABKLinkModel> TournamentLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TABKLinkModel>().HasIndex(x => x.TournamentId).IsUnique();
            modelBuilder.Entity<TABKLinkModel>().HasIndex(x => x.BeatKhanaTournamentGuid).IsUnique();
        }

        public bool BeatKhanaTournamentExists(string beatKhanaTournamentGuid) =>
            TournamentLinks.Any(x => x.BeatKhanaTournamentGuid == beatKhanaTournamentGuid);

        public string GetBeatKhanaTournamentGuid(string tournamentId) => TournamentLinks.FirstOrDefault(x => x.TournamentId == tournamentId)?.BeatKhanaTournamentGuid;

        public void AddLink(string tournamentId, string beatKhanaTournamentGuid)
        {
            TournamentLinks.Add(new TABKLinkModel
            {
                TournamentId = tournamentId,
                BeatKhanaTournamentGuid = beatKhanaTournamentGuid,
            });
            SaveChanges();
        }

        public void RemoveLink(string tournamentId)
        {
            var link = TournamentLinks.FirstOrDefault(x => x.TournamentId == tournamentId);
            if (link == null)
            {
                return;
            }
            TournamentLinks.Remove(link);
            SaveChanges();
        }
    }
}
