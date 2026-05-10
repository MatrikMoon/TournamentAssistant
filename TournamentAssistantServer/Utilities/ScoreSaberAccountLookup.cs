using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared;

namespace TournamentAssistantServer.Utilities
{
    public class ScoreSaberAccountLookup
    {
        public class ExternalAccountProfile
        {
            public string RequestedId { get; set; }
            public string PlayerId { get; set; }
            public string DisplayName { get; set; }
            public string AvatarUrl { get; set; }
        }

        private static readonly HttpClient client = new HttpClient
        {
            BaseAddress = new Uri("https://scoresaber.com")
        };

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly ConcurrentDictionary<string, ExternalAccountProfile> cache = new ConcurrentDictionary<string, ExternalAccountProfile>();

        // ScoreSaber rate limit is generous (400 rpm), but we still keep concurrency bounded.
        private const int MaxConcurrentRequests = 8;

        public static async Task<Dictionary<string, ExternalAccountProfile>> GetProfilesAsync(IEnumerable<string> accountIds)
        {
            var ids = accountIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            var results = new ConcurrentDictionary<string, ExternalAccountProfile>();
            var missingIds = new List<string>();

            foreach (var id in ids)
            {
                if (cache.TryGetValue(id, out var cachedProfile) && cachedProfile != null)
                {
                    results[id] = CloneProfile(cachedProfile, id);
                }
                else
                {
                    missingIds.Add(id);
                }
            }

            using var semaphore = new SemaphoreSlim(MaxConcurrentRequests);
            var tasks = missingIds.Select(async id =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var profile = await GetProfileInternalAsync(id);
                    if (profile != null)
                    {
                        cache[id] = profile;
                        results[id] = CloneProfile(profile, id);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return results.ToDictionary(x => x.Key, x => x.Value);
        }

        private static async Task<ExternalAccountProfile> GetProfileInternalAsync(string accountId)
        {
            try
            {
                using var response = await client.GetAsync($"/api/player/{accountId}/basic");
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadAsStringAsync();
                var dto = JsonSerializer.Deserialize<ScoreSaberPlayerDto>(payload, jsonOptions);
                if (dto == null || string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Name))
                {
                    return null;
                }

                return new ExternalAccountProfile
                {
                    RequestedId = accountId,
                    PlayerId = dto.Id,
                    DisplayName = dto.Name,
                    AvatarUrl = dto.ProfilePicture,
                };
            }
            catch (Exception e)
            {
                Logger.Warning($"ScoreSaber lookup failed for {accountId}: {e.Message}");
                return null;
            }
        }

        private static ExternalAccountProfile CloneProfile(ExternalAccountProfile profile, string requestedId)
        {
            return new ExternalAccountProfile
            {
                RequestedId = requestedId,
                PlayerId = profile.PlayerId,
                DisplayName = profile.DisplayName,
                AvatarUrl = profile.AvatarUrl,
            };
        }

        private class ScoreSaberPlayerDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string ProfilePicture { get; set; }
        }
    }
}
