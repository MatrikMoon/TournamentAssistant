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
    public class BeatLeaderAccountLookup
    {
        private static readonly HttpClient client = new HttpClient
        {
            BaseAddress = new Uri("https://api.beatleader.com")
        };

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly ConcurrentDictionary<string, ScoreSaberAccountLookup.ExternalAccountProfile> cache = new ConcurrentDictionary<string, ScoreSaberAccountLookup.ExternalAccountProfile>();

        private const int MaxConcurrentRequests = 8;

        public static async Task<Dictionary<string, ScoreSaberAccountLookup.ExternalAccountProfile>> GetProfilesAsync(IEnumerable<string> accountIds)
        {
            var ids = accountIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            var results = new ConcurrentDictionary<string, ScoreSaberAccountLookup.ExternalAccountProfile>();
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
                    var fetchedProfile = await GetProfileInternalAsync(id);
                    if (fetchedProfile == null)
                    {
                        return;
                    }

                    // Map the fetched profile to all known linked IDs so future lookups can be served from cache.
                    var knownIds = new[] { id, fetchedProfile.Id, fetchedProfile.SteamId, fetchedProfile.OculusPCId }
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct()
                        .ToList();

                    var normalizedProfile = new ScoreSaberAccountLookup.ExternalAccountProfile
                    {
                        RequestedId = id,
                        PlayerId = fetchedProfile.Id,
                        DisplayName = fetchedProfile.Name,
                        AvatarUrl = fetchedProfile.Avatar,
                    };

                    foreach (var knownId in knownIds)
                    {
                        cache[knownId] = normalizedProfile;
                    }

                    results[id] = CloneProfile(normalizedProfile, id);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            // If an id was not directly found, but became cached as a linked id from another lookup, include it.
            foreach (var id in ids)
            {
                if (!results.ContainsKey(id) && cache.TryGetValue(id, out var cachedProfile) && cachedProfile != null)
                {
                    results[id] = CloneProfile(cachedProfile, id);
                }
            }

            return results.ToDictionary(x => x.Key, x => x.Value);
        }

        private static async Task<BeatLeaderPlayerDto> GetProfileInternalAsync(string accountId)
        {
            try
            {
                using var response = await client.GetAsync($"/player/{accountId}?stats=false");
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadAsStringAsync();
                var dto = JsonSerializer.Deserialize<BeatLeaderPlayerDto>(payload, jsonOptions);
                if (dto == null || string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Name))
                {
                    return null;
                }

                return dto;
            }
            catch (Exception e)
            {
                Logger.Warning($"BeatLeader lookup failed for {accountId}: {e.Message}");
                return null;
            }
        }

        private static ScoreSaberAccountLookup.ExternalAccountProfile CloneProfile(ScoreSaberAccountLookup.ExternalAccountProfile profile, string requestedId)
        {
            return new ScoreSaberAccountLookup.ExternalAccountProfile
            {
                RequestedId = requestedId,
                PlayerId = profile.PlayerId,
                DisplayName = profile.DisplayName,
                AvatarUrl = profile.AvatarUrl,
            };
        }

        private class BeatLeaderPlayerDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Avatar { get; set; }
            public BeatLeaderLinkedIdsDto LinkedIds { get; set; }

            public string SteamId => LinkedIds?.SteamId;
            public string OculusPCId => LinkedIds?.OculusPCId;
        }

        private class BeatLeaderLinkedIdsDto
        {
            public string SteamId { get; set; }
            public string OculusPCId { get; set; }
        }
    }
}
