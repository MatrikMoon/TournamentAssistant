using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TournamentAssistantServer.Discord.Services;
using TournamentAssistantShared.Models.Discord;
using TournamentAssistantShared.Models;
using TournamentAssistantServer.Database.Contexts;
using TournamentAssistantServer.Database.Models;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.Helpers;
using TournamentAssistantShared;

namespace TournamentAssistantServer.Discord
{
    public class QualifierBot
    {
        private static Random random = new();

        private DiscordSocketClient _client;
        private IServiceProvider _services;
        private string _botToken;
        private TAServer _server;

        public TournamentDatabaseContext TournamentDatabase => _services?.GetService<DatabaseService>()?.TournamentDatabase;
        public QualifierDatabaseContext QualifierDatabase => _services?.GetService<DatabaseService>()?.QualifierDatabase;
        public UserDatabaseContext UserDatabase => _services?.GetService<DatabaseService>()?.UserDatabase;

        public QualifierBot(string botToken = null, TAServer server = null)
        {
            _botToken = botToken;
            _server = server;
        }

        public async Task Start(DatabaseService databaseService = null)
        {
            _services = ConfigureServices(databaseService);
            _services.GetRequiredService<InteractionService>().Log += LogAsync;

            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.Log += LogAsync;

            if (_botToken == null) _botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
            if (_botToken == null) throw new ArgumentException("You must pass in a bot token, by setting it in the config, setting it as an environment variable, or passing it in as a command parameter");

            await _client.LoginAsync(TokenType.Bot, _botToken);
            await _client.StartAsync();

            _client.Ready += async () =>
            {
                await _services.GetRequiredService<CommandHandlingService>().InitializeAsync();
            };
        }

        public void SendMessage(Channel channel, string message)
        {
            var socketChannel = _client.GetChannel(ulong.Parse(channel.Id)) as SocketTextChannel;
            socketChannel?.SendMessageAsync(message);
        }

        public void SendScoreEvent(string channelId, LeaderboardScore score)
        {
            var map = QualifierDatabase.Songs.Where(x => x.Guid == score.MapId).FirstOrDefault();

            if (map != null)
            {
                var channel = _client.GetChannel(ulong.Parse(channelId)) as SocketTextChannel;
                channel?.SendMessageAsync($"{score.Username} has scored {score.Score}{(score.FullCombo ? " (Full Combo!)" : "")} on {map.Name}!");
            }
        }

        public async Task<string> SendLeaderboardUpdate(string channelId, string messageId, string mapId)
        {
            var song = QualifierDatabase.Songs.First(x => x.Guid == mapId);

            var channel = _client.GetChannel(ulong.Parse(channelId)) as SocketTextChannel;
            RestUserMessage message;

            if (string.IsNullOrWhiteSpace(messageId)) message = await channel.SendMessageAsync("Leaderboard Placeholder");
            else message = await channel.GetMessageAsync(ulong.Parse(messageId)) as RestUserMessage;

            var scores = QualifierDatabase.Scores.Where(x => x.MapId == song.Guid && !x.Old);

            var builder = new EmbedBuilder()
                .WithTitle($"<:page_with_curl:735592941338361897> {song.Name}")
                .WithColor(new Color(random.Next(255), random.Next(255), random.Next(255)))
                .AddField("•", $"\n{string.Join("\n", scores.Select(x => $"{x._Score,-8} {(x.FullCombo ? "FC" : "  ")} {x.Username}"))}", inline: true)
                .WithFooter("Retrieved: ")
                .WithCurrentTimestamp();

            await message.ModifyAsync(x =>
            {
                x.Embed = builder.Build();
                x.Content = "";
            });

            return message.Id.ToString();
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }

        private IServiceProvider ConfigureServices(DatabaseService databaseService = null)
        {
            var config = new DiscordSocketConfig
            {
                MessageCacheSize = 0,
                GatewayIntents = GatewayIntents.AllUnprivileged & ~(GatewayIntents.GuildScheduledEvents | GatewayIntents.GuildInvites),
                LogGatewayIntentWarnings = true,
            };

            return new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(config))
                .AddSingleton(provider => new InteractionService(provider.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<HttpClient>()
                .AddSingleton<PictureService>()
                .AddSingleton<ScoresaberService>()
                .AddSingleton(databaseService ?? new DatabaseService())
                .AddSingleton(new TAServerService(_server))
                .BuildServiceProvider();
        }
    }
}
