using System;
using TournamentAssistantShared.Models;

namespace TournamentAssistantServer.Utilities
{
    // Visible tournaments that nobody has changed for a while are hidden automatically. Any change to a tournament resets the timer
    public static class TournamentActivity
    {
        public static readonly TimeSpan HideAfter = TimeSpan.FromDays(90);

        public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static void Apply(Tournament.TournamentSettings settings, long updatedAt)
        {
            settings.UpdatedAt = updatedAt;
            settings.AutoHideAt = settings.AllowUnauthorizedView ? updatedAt + (long)HideAfter.TotalMilliseconds : 0;
        }

        public static bool ShouldHide(Tournament tournament, long now)
        {
            return tournament.Settings.AllowUnauthorizedView && tournament.Settings.AutoHideAt > 0 && tournament.Settings.AutoHideAt <= now;
        }
    }
}
