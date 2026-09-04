using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WebhookDatabaseModel = TournamentAssistantServer.Database.Models.Webhook;
using WebhookProtobufModel = TournamentAssistantShared.Models.Webhook;

namespace TournamentAssistantServer.Database.Contexts
{
    public class WebhookDatabaseContext : DatabaseContext
    {
        public WebhookDatabaseContext()
            : base("files/WebhookDatabase.db") { }

        public DbSet<WebhookDatabaseModel> Webhooks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WebhookDatabaseModel>().HasIndex(x => x.Guid).IsUnique();
            modelBuilder.Entity<WebhookDatabaseModel>().HasIndex(x => x.TournamentId);
        }

        public List<WebhookProtobufModel> GetWebhooks(string tournamentId)
        {
            return Webhooks.AsQueryable()
                .Where(x => !x.Old && x.TournamentId == tournamentId)
                .AsEnumerable()
                .OrderBy(x => x.ID)
                .Select(ToProtobufModel)
                .ToList();
        }

        public WebhookProtobufModel CreateWebhook(
            string tournamentId,
            string url,
            long triggers,
            string signingSecret
        )
        {
            var webhook = new WebhookDatabaseModel
            {
                Guid = Guid.NewGuid().ToString(),
                TournamentId = tournamentId,
                Url = url,
                Triggers = triggers,
                SigningSecret = signingSecret ?? string.Empty,
            };

            Webhooks.Add(webhook);
            SaveChanges();
            return ToProtobufModel(webhook);
        }

        public WebhookProtobufModel UpdateWebhook(
            string tournamentId,
            string webhookGuid,
            string url,
            long triggers,
            bool replaceSigningSecret,
            string signingSecret
        )
        {
            var webhook = Webhooks.FirstOrDefault(x =>
                !x.Old && x.TournamentId == tournamentId && x.Guid == webhookGuid
            );
            if (webhook == null)
                return null;

            webhook.Url = url;
            webhook.Triggers = triggers;
            if (replaceSigningSecret)
                webhook.SigningSecret = signingSecret ?? string.Empty;

            SaveChanges();
            return ToProtobufModel(webhook);
        }

        public bool DeleteWebhook(string tournamentId, string webhookGuid)
        {
            var webhook = Webhooks.FirstOrDefault(x =>
                !x.Old && x.TournamentId == tournamentId && x.Guid == webhookGuid
            );
            if (webhook == null)
                return false;

            webhook.Old = true;
            SaveChanges();
            return true;
        }

        public void DeleteWebhooksForTournament(string tournamentId)
        {
            foreach (var webhook in Webhooks.AsQueryable().Where(x => !x.Old && x.TournamentId == tournamentId))
                webhook.Old = true;
            SaveChanges();
        }

        private static WebhookProtobufModel ToProtobufModel(WebhookDatabaseModel webhook)
        {
            return new WebhookProtobufModel
            {
                Guid = webhook.Guid,
                TournamentId = webhook.TournamentId,
                Url = webhook.Url,
                Triggers = webhook.Triggers,
                HasSigningSecret = !string.IsNullOrEmpty(webhook.SigningSecret),
            };
        }
    }
}
