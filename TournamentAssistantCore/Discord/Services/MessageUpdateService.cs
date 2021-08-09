#pragma warning disable 1998
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace TournamentAssistantCore.Discord.Services
{
    public class MessageUpdateService
    {
        public DiscordSocketClient _discordClient;

        public event Action<SocketReaction> ReactionAdded;
        public event Action<SocketReaction> ReactionRemoved;
        public event Action<Cacheable<IMessage, ulong>, SocketMessage, ISocketMessageChannel> MessageUpdated;
        public event Action<Cacheable<IMessage, ulong>, ISocketMessageChannel> MessageDeleted;

        public MessageUpdateService(IServiceProvider services)
        {
            _discordClient = services.GetRequiredService<DiscordSocketClient>();

            _discordClient.ReactionAdded += ReactionAddedAsync;
            _discordClient.ReactionRemoved += ReactionRemovedAsync;
            _discordClient.MessageUpdated += MessageUpdatedAsync;
            _discordClient.MessageDeleted += MessageDeletedAsync;
        }

        private async Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> before, ISocketMessageChannel channel, SocketReaction reaction)
        {
            ReactionAdded?.Invoke(reaction);
        }

        private async Task ReactionRemovedAsync(Cacheable<IUserMessage, ulong> before, ISocketMessageChannel channel, SocketReaction reaction)
        {
            ReactionRemoved?.Invoke(reaction);
        }

        private async Task MessageUpdatedAsync(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            MessageUpdated?.Invoke(before, after, channel);
        }

        private async Task MessageDeletedAsync(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            MessageDeleted?.Invoke(message, channel);
        }
    }
}
