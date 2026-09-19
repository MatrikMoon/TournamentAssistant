using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using GlobalConfigurationModel = TournamentAssistantServer.Database.Models.GlobalConfiguration;
using EndpointAccessModel = TournamentAssistantServer.Database.Models.EndpointAccess;

namespace TournamentAssistantServer.Database.Contexts
{
    public class GlobalConfigurationDatabaseContext : DatabaseContext
    {
        public const string OwnerDiscordId = "229408465787944970";
        public const string SecondaryEndpointManagerDiscordId = "469171963236057120";

        public GlobalConfigurationDatabaseContext()
            : base("files/GlobalConfigurationDatabase.db") { }

        public DbSet<GlobalConfigurationModel> GlobalConfigurations { get; set; }
        public DbSet<EndpointAccessModel> EndpointAccess { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EndpointAccessModel>().HasIndex(x => x.EndpointId).IsUnique();
        }

        public void EnsureDefaults()
        {
            var configuration = GlobalConfigurations.AsQueryable().OrderBy(x => x.ID).FirstOrDefault();
            if (configuration == null)
            {
                GlobalConfigurations.Add(new GlobalConfigurationModel
                {
                    FullAccessDiscordIds = Serialize(new[] { OwnerDiscordId }),
                    EndpointManagerDiscordIds = Serialize(new[] { OwnerDiscordId, SecondaryEndpointManagerDiscordId }),
                });
                SaveChanges();
                return;
            }

            var changed = false;
            if (Deserialize(configuration.FullAccessDiscordIds).Length == 0)
            {
                configuration.FullAccessDiscordIds = Serialize(new[] { OwnerDiscordId });
                changed = true;
            }
            if (Deserialize(configuration.EndpointManagerDiscordIds).Length == 0)
            {
                configuration.EndpointManagerDiscordIds = Serialize(new[] { OwnerDiscordId, SecondaryEndpointManagerDiscordId });
                changed = true;
            }
            if (changed)
            {
                SaveChanges();
            }
        }

        public string[] GetFullAccessDiscordIds() =>
            Deserialize(GetConfiguration().FullAccessDiscordIds);

        public string[] GetEndpointManagerDiscordIds() =>
            Deserialize(GetConfiguration().EndpointManagerDiscordIds);

        public bool HasFullAccess(string discordId) =>
            !string.IsNullOrWhiteSpace(discordId) && GetFullAccessDiscordIds().Contains(discordId);

        public bool CanManageEndpoints(string discordId) =>
            HasFullAccess(discordId) ||
            (!string.IsNullOrWhiteSpace(discordId) && GetEndpointManagerDiscordIds().Contains(discordId));

        public void UpdateAdministrators(IEnumerable<string> fullAccessIds, IEnumerable<string> endpointManagerIds)
        {
            var fullAccess = Normalize(fullAccessIds);
            var endpointManagers = Normalize(endpointManagerIds);
            if (fullAccess.Length == 0)
            {
                fullAccess = new[] { OwnerDiscordId };
            }
            if (endpointManagers.Length == 0)
            {
                endpointManagers = new[] { OwnerDiscordId, SecondaryEndpointManagerDiscordId };
            }

            var configuration = GetConfiguration();
            configuration.FullAccessDiscordIds = Serialize(fullAccess);
            configuration.EndpointManagerDiscordIds = Serialize(endpointManagers);
            SaveChanges();
        }

        public EndpointAccessModel GetEndpointAccess(string endpointId) =>
            EndpointAccess.FirstOrDefault(x => x.EndpointId == endpointId);

        public void SetEndpointAccess(string endpointId, bool websocketEnabled, bool restEnabled, bool playerEnabled)
        {
            var access = GetEndpointAccess(endpointId);
            if (access == null)
            {
                access = new EndpointAccessModel { EndpointId = endpointId };
                EndpointAccess.Add(access);
            }
            access.WebsocketEnabled = websocketEnabled;
            access.RestEnabled = restEnabled;
            access.PlayerEnabled = playerEnabled;
            SaveChanges();
        }

        private GlobalConfigurationModel GetConfiguration()
        {
            EnsureDefaults();
            return GlobalConfigurations.AsQueryable().OrderBy(x => x.ID).First();
        }

        private static string[] Normalize(IEnumerable<string> values) =>
            (values ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToArray();

        private static string Serialize(IEnumerable<string> values) =>
            JsonConvert.SerializeObject(Normalize(values));

        private static string[] Deserialize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }
            try
            {
                return Normalize(JsonConvert.DeserializeObject<string[]>(value));
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }
    }
}
