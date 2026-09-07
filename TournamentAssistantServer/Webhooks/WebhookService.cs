using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using TournamentAssistantServer.Database;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.Webhooks
{
    public class WebhookService : IDisposable
    {
        private static readonly JsonSerializerSettings SerializerSettings =
            new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Include,
            };

        private readonly DatabaseService databaseService;
        private readonly HttpClient httpClient;

        public WebhookService(DatabaseService databaseService)
        {
            this.databaseService = databaseService;
            httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(10),
            };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TournamentAssistant-Webhook/1.0");
        }

        public static bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && !string.IsNullOrWhiteSpace(uri.Host)
                && string.IsNullOrEmpty(uri.UserInfo)
                && url.Length <= 2048;
        }

        public static bool AreValidTriggers(long triggers)
        {
            return triggers > 0 && (triggers & ~(long)Webhook.Trigger.All) == 0;
        }

        public void Publish(
            string tournamentId,
            Webhook.Trigger trigger,
            string oneOfKind,
            object payload
        )
        {
            try
            {
                using var database = databaseService.NewWebhookDatabaseContext();
                var webhooks = database.Webhooks.AsQueryable()
                    .Where(x =>
                        !x.Old
                        && x.TournamentId == tournamentId
                        && (x.Triggers & (long)trigger) != 0
                    )
                    .ToList();
                if (webhooks.Count == 0)
                    return;

                var deliveryId = Guid.NewGuid().ToString();
                var body = new JObject
                {
                    ["id"] = deliveryId,
                    ["timestamp"] = DateTime.UtcNow.ToString("O"),
                    ["tournamentId"] = tournamentId,
                    ["oneOfKind"] = oneOfKind,
                    ["data"] = new JObject
                    {
                        [oneOfKind] = JToken.FromObject(
                            payload,
                            JsonSerializer.Create(SerializerSettings)
                        ),
                    },
                }.ToString(Formatting.None);

                foreach (var webhook in webhooks)
                    _ = Deliver(webhook.Url, webhook.SigningSecret, deliveryId, oneOfKind, body);
            }
            catch (Exception exception)
            {
                Logger.Warning($"Could not prepare webhook {oneOfKind}: {exception.Message}");
            }
        }

        public void PublishEvent(string tournamentId, Event @event)
        {
            switch (@event.ChangedObjectCase)
            {
                case Event.ChangedObjectOneofCase.tournament_updated:
                    Publish(tournamentId, Webhook.Trigger.TournamentUpdated, "tournamentUpdated", @event.tournament_updated);
                    break;
                case Event.ChangedObjectOneofCase.tournament_deleted:
                    Publish(tournamentId, Webhook.Trigger.TournamentDeleted, "tournamentDeleted", @event.tournament_deleted);
                    break;
                case Event.ChangedObjectOneofCase.user_added:
                    Publish(tournamentId, Webhook.Trigger.UserAdded, "userAdded", @event.user_added);
                    break;
                case Event.ChangedObjectOneofCase.user_updated:
                    Publish(tournamentId, Webhook.Trigger.UserUpdated, "userUpdated", @event.user_updated);
                    break;
                case Event.ChangedObjectOneofCase.user_left:
                    Publish(tournamentId, Webhook.Trigger.UserLeft, "userLeft", @event.user_left);
                    break;
                case Event.ChangedObjectOneofCase.match_created:
                    Publish(tournamentId, Webhook.Trigger.MatchCreated, "matchCreated", @event.match_created);
                    break;
                case Event.ChangedObjectOneofCase.match_updated:
                    Publish(tournamentId, Webhook.Trigger.MatchUpdated, "matchUpdated", @event.match_updated);
                    break;
                case Event.ChangedObjectOneofCase.match_deleted:
                    Publish(tournamentId, Webhook.Trigger.MatchDeleted, "matchDeleted", @event.match_deleted);
                    break;
                case Event.ChangedObjectOneofCase.qualifier_created:
                    Publish(tournamentId, Webhook.Trigger.QualifierCreated, "qualifierCreated", @event.qualifier_created);
                    break;
                case Event.ChangedObjectOneofCase.qualifier_updated:
                    Publish(tournamentId, Webhook.Trigger.QualifierUpdated, "qualifierUpdated", @event.qualifier_updated);
                    break;
                case Event.ChangedObjectOneofCase.qualifier_deleted:
                    Publish(tournamentId, Webhook.Trigger.QualifierDeleted, "qualifierDeleted", @event.qualifier_deleted);
                    break;
            }
        }

        private async Task Deliver(
            string url,
            string signingSecret,
            string deliveryId,
            string oneOfKind,
            string body
        )
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                request.Headers.Add("X-TA-Webhook-Event", oneOfKind);
                request.Headers.Add("X-TA-Webhook-Delivery", deliveryId);
                if (!string.IsNullOrEmpty(signingSecret))
                    request.Headers.Add("X-TA-Signature-256", CreateSignature(signingSecret, body));

                using var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    Logger.Warning($"Webhook {url} returned {(int)response.StatusCode}");
            }
            catch (Exception exception)
            {
                Logger.Warning($"Webhook {url} failed: {exception.Message}");
            }
        }

        private static string CreateSignature(string secret, string body)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            return "sha256=" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}
