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
        public static async Task<User.DiscordInfo> GetAccountInfo(QualifierBot qualifierBot, DatabaseService databaseService, string accountId)
        {
            var name = "";
            var avatarUrl = "https://cdn.discordapp.com/avatars/708801604719214643/d37a1b93a741284ecd6e57569f6cd598.webp?size=100";

            // If discordId is a guid, we're dealing with a bot token
            if (Guid.TryParse(accountId, out var _))
            {
                var userDatabase = databaseService.NewUserDatabaseContext();
                var botUser = userDatabase.GetUser(accountId);

                name = botUser?.Name ?? "[AUTHORIZATION REVOKED]";
            }

            // If we don't have a bot token on our hands, maybe we have a discord id?
            // Discord id-s (snowflakes) are 17-19 characters long. Add the 19 and allow for easier adjustment in the future
            if (string.IsNullOrEmpty(name) && (accountId.Length > 16 && accountId.Length < 20))
            {
                Logger.Warning($"Looking up info for discord user: {accountId}");
                var userInfo = await qualifierBot.GetAccountInfo(accountId);

                name = userInfo.Item1;
                avatarUrl = userInfo.Item2;
            }

            // If we still don't have any info, maybe it was a steam ID?
            if (string.IsNullOrEmpty(name) && accountId.Length == 17)
            {
                Logger.Warning($"Looking up info for steam user: {accountId}");

                try
                {
                    var steamInfo = await SteamAccountLookup.GetProfileFromSteamId64Async(accountId);

                    name = steamInfo.SteamID;
                    avatarUrl = steamInfo.AvatarIcon;
                }
                catch
                {
                    name = $"{accountId} (stop rate limiting me you dummy)";
                }
            }

            // If we STILL don't have any info, it's probably an Oculus ID, and there's nothing I can do about that
            if (string.IsNullOrEmpty(name))
            {
                name = "[OCULUS USER]";
            }

            return new User.DiscordInfo
            {
                UserId = accountId,
                Username = name,
                AvatarUrl = avatarUrl,
            };
        }
    }
}
