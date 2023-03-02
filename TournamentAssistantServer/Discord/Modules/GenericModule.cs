#pragma warning disable 1998
using Discord;
using Discord.Interactions;
using System.Threading.Tasks;
using TournamentAssistantServer.Discord.Services;

/**
 * Created by Moon on 5/18/2019
 * A Discord.NET module for basic bot functionality, not necessarily relating to Beat Saber
 */

namespace TournamentAssistantServer.Discord.Modules
{
    public class GenericModule : InteractionModuleBase
    {
        public DatabaseService DatabaseService { get; set; }

        [SlashCommand("test", "A test command, for quick access to test features")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Test(string args = null)
        {
            await Task.Delay(0);
        }
    }
}
