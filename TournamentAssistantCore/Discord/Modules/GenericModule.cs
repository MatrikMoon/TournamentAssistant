#pragma warning disable 1998
using Discord;
using Discord.Commands;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantCore.Discord.Helpers;
using TournamentAssistantCore.Discord.Services;

/**
 * Created by Moon on 5/18/2019
 * A Discord.NET module for basic bot functionality, not necessarily relating to Beat Saber
 */

namespace TournamentAssistantCore.Discord.Modules
{
    public class GenericModule : ModuleBase<SocketCommandContext>
    {
        public MessageUpdateService MessageUpdateService { get; set; }
        public DatabaseService DatabaseService { get; set; }
        public CommandService CommandService { get; set; }

        private static Random random = new Random();

        private bool IsAdmin()
        {
            return ((IGuildUser)Context.User).GuildPermissions.Has(GuildPermission.Administrator);
        }

        [Command("test")]
        [Summary("A test command, for quick access to test features")]
        public async Task Test([Remainder] string args = null)
        {
            if (IsAdmin()) await Task.Delay(0);
        }

        [Command("listModules")]
        [Summary("Lists loaded bot modules")]
        public async Task ListModules()
        {
            var reply = string.Empty;
            foreach (var x in CommandService.Modules.Select(x => x.Name))
            {
                reply += $"{x}\n";
            }
            await ReplyAsync(reply);
        }

        [Command("help")]
        [Summary("Shows help message")]
        public async Task HelpAsync()
        {
            var builder = new EmbedBuilder();
            builder.Title = "<:page_with_curl:735592941338361897> Commands";
            builder.Color = new Color(random.Next(255), random.Next(255), random.Next(255));

            foreach (var module in CommandService.Modules)
            {
                //Skip if the module has no commands
                if (module.Commands.Count <= 0) continue;

                builder.AddField(module.Name, $"```\n{string.Join("\n", module.Commands.Select(x => x.Name))}```", true);
            }

            await ReplyAsync(embed: builder.Build());
        }
    }
}
