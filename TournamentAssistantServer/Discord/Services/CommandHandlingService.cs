using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Interactions;

namespace TournamentAssistantServer.Discord.Services
{
    public class CommandHandlingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly InteractionService _interactionService;
        private readonly IServiceProvider _services;

        public CommandHandlingService(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _interactionService = services.GetRequiredService<InteractionService>();
            _services = services;

            _discord.SlashCommandExecuted += SlashCommandExecuted;
        }

        private async Task SlashCommandExecuted(SocketSlashCommand arg)
        {
            await _interactionService.ExecuteCommandAsync(new SocketInteractionContext<SocketSlashCommand>(_discord, arg), _services);
        }

        public async Task InitializeAsync()
        {
            var modules = await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            await _interactionService.AddModulesGloballyAsync(true, modules.ToArray());
        }
    }
}
