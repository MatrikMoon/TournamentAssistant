using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace TournamentAssistantCore.Discord.Services
{
    public class CommandHandlingService
    {
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _discord;
        private readonly MessageUpdateService _messageUpdateService;
        private readonly IServiceProvider _services;

        public CommandHandlingService(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _messageUpdateService = services.GetRequiredService<MessageUpdateService>();
            _services = services;

            _discord.MessageReceived += MessageReceivedAsync;
            _messageUpdateService.MessageUpdated += MessageUpdatedAsync;
        }

        public async Task InitializeAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        public async Task MessageReceivedAsync(SocketMessage rawMessage)
        {
            // Ignore system messages, or messages from other bots
            if (!(rawMessage is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;

            // This value holds the offset where the prefix ends
            var argPos = 0;
            if (!message.HasMentionPrefix(_discord.CurrentUser, ref argPos)) return;

            var context = new SocketCommandContext(_discord, message);
            var result = await _commands.ExecuteAsync(context, argPos, _services);

            if (result.Error.HasValue &&
                result.Error.Value != CommandError.UnknownCommand) // it's bad practice to send 'unknown command' errors
                await context.Channel.SendMessageAsync(result.ToString());
        }

        public async void MessageUpdatedAsync(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            try
            {
                var message = await before.GetOrDownloadAsync();
                Console.WriteLine($"{message} -> {after}");

                //When a message is edited, toss it through the parser again
                //Allows editing to fix spacing errors and such that prevent commands from being run
                //NOTE: Embed checking like this avoids re-parsing commands when an embed is downloaded
                //TODO: Fix the hackiness, implement real "listen for update" system
                if (message.Embeds.Count == after.Embeds.Count) await MessageReceivedAsync(after);
            }
            catch { }
        }
    }
}
