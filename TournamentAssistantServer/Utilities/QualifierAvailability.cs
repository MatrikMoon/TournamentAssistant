using System;
using TournamentAssistantShared.Models;

namespace TournamentAssistantServer.Utilities
{
    public static class QualifierAvailability
    {
        public static bool IsActive(QualifierEvent qualifier, DateTimeOffset? now = null)
        {
            if (qualifier == null)
                return false;

            var utcNow = (now ?? DateTimeOffset.UtcNow).UtcDateTime;
            return (!qualifier.StartTime.HasValue || qualifier.StartTime.Value.ToUniversalTime() <= utcNow)
                && (!qualifier.EndTime.HasValue || utcNow < qualifier.EndTime.Value.ToUniversalTime());
        }

        public static bool IsActive(Database.Models.Qualifier qualifier, DateTimeOffset? now = null)
        {
            if (qualifier == null)
                return false;

            var unixSeconds = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
            return (!qualifier.StartTimeUnixSeconds.HasValue || qualifier.StartTimeUnixSeconds.Value <= unixSeconds)
                && (!qualifier.EndTimeUnixSeconds.HasValue || unixSeconds < qualifier.EndTimeUnixSeconds.Value);
        }
    }
}
