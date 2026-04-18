using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using TournamentAssistantShared.Models;
using TournamentAssistantShared;
using TournamentAssistantServer.Database;
using TournamentAssistantDiscordBot.Discord;

namespace TournamentAssistantServer.Utilities
{
    public class AccountLookup
    {
        private const string DefaultAvatarUrl = "https://cdn.discordapp.com/avatars/708801604719214643/d37a1b93a741284ecd6e57569f6cd598.webp?size=100";

        public static async Task<User.DiscordInfo> GetAccountInfo(QualifierBot qualifierBot, DatabaseService databaseService, string accountId)
        {
            var allInfos = await GetAccountInfos(qualifierBot, databaseService, new[] { accountId });
            return allInfos.TryGetValue(accountId, out var info)
                ? info
                : new User.DiscordInfo
                {
                    UserId = accountId,
                    Username = "[OCULUS USER]",
                    AvatarUrl = DefaultAvatarUrl,
                };
        }

        public static async Task<Dictionary<string, User.DiscordInfo>> GetAccountInfos(QualifierBot qualifierBot, DatabaseService databaseService, IEnumerable<string> accountIds)
        {
            var distinctIds = accountIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            var results = new Dictionary<string, User.DiscordInfo>();
            var unresolved = new HashSet<string>(distinctIds);

            ResolveBotTokenUsers(databaseService, unresolved, results);
            await ResolveDiscordUsers(qualifierBot, unresolved, results);

            if (unresolved.Count > 0)
            {
                var scoreSaberProfiles = await ScoreSaberAccountLookup.GetProfilesAsync(unresolved);
                foreach (var pair in scoreSaberProfiles)
                {
                    results[pair.Key] = new User.DiscordInfo
                    {
                        UserId = pair.Key,
                        Username = pair.Value.DisplayName,
                        AvatarUrl = string.IsNullOrWhiteSpace(pair.Value.AvatarUrl) ? DefaultAvatarUrl : pair.Value.AvatarUrl,
                    };
                    unresolved.Remove(pair.Key);
                }
            }

            if (unresolved.Count > 0)
            {
                var beatLeaderProfiles = await BeatLeaderAccountLookup.GetProfilesAsync(unresolved);
                foreach (var pair in beatLeaderProfiles)
                {
                    results[pair.Key] = new User.DiscordInfo
                    {
                        UserId = pair.Key,
                        Username = pair.Value.DisplayName,
                        AvatarUrl = string.IsNullOrWhiteSpace(pair.Value.AvatarUrl) ? DefaultAvatarUrl : pair.Value.AvatarUrl,
                    };
                    unresolved.Remove(pair.Key);
                }
            }

            foreach (var unresolvedId in unresolved)
            {
                results[unresolvedId] = new User.DiscordInfo
                {
                    UserId = unresolvedId,
                    Username = "[OCULUS USER]",
                    AvatarUrl = DefaultAvatarUrl,
                };
            }

            return results;
        }

        private static void ResolveBotTokenUsers(DatabaseService databaseService, HashSet<string> unresolved, Dictionary<string, User.DiscordInfo> results)
        {
            var tokenIds = unresolved.Where(x => Guid.TryParse(x, out _)).ToList();
            if (tokenIds.Count == 0)
            {
                return;
            }

            using var userDatabase = databaseService.NewUserDatabaseContext();
            foreach (var tokenId in tokenIds)
            {
                var botUser = userDatabase.GetUser(tokenId);
                results[tokenId] = new User.DiscordInfo
                {
                    UserId = tokenId,
                    Username = botUser?.Name ?? "[TOKEN NOT FOUND]",
                    AvatarUrl = DefaultAvatarUrl,
                };
                unresolved.Remove(tokenId);
            }
        }

        private static async Task ResolveDiscordUsers(QualifierBot qualifierBot, HashSet<string> unresolved, Dictionary<string, User.DiscordInfo> results)
        {
            if (qualifierBot == null)
            {
                return;
            }

            var discordCandidateIds = unresolved.Where(IsPossibleDiscordId).ToList();
            foreach (var discordId in discordCandidateIds)
            {
                try
                {
                    Logger.Warning($"Looking up info for discord user: {discordId}");
                    var userInfo = await qualifierBot.GetAccountInfo(discordId);
                    if (!string.IsNullOrWhiteSpace(userInfo.Item1))
                    {
                        results[discordId] = new User.DiscordInfo
                        {
                            UserId = discordId,
                            Username = userInfo.Item1,
                            AvatarUrl = string.IsNullOrWhiteSpace(userInfo.Item2) ? DefaultAvatarUrl : userInfo.Item2,
                        };
                        unresolved.Remove(discordId);
                    }
                }
                catch
                {
                    // Keep unresolved. We'll try ScoreSaber, then BeatLeader.
                }
            }
        }

        private static bool IsPossibleDiscordId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 17 || value.Length > 19)
            {
                return false;
            }

            return value.All(char.IsDigit);
        }
    }
}
