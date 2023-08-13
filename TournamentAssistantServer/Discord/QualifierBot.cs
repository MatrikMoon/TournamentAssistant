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

namespace TournamentAssistantServer.Discord
{
    public class QualifierBot
    {
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

        public async Task Start()
        {
            _services = ConfigureServices();
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
            var socketChannel = _client.GetChannel((ulong)channel.Id) as SocketTextChannel;
            socketChannel?.SendMessageAsync(message);
        }

        public void SendScoreEvent(ulong channelId, LeaderboardScore score)
        {
            var map = QualifierDatabase.Songs.Where(x => x.Guid == score.MapId).FirstOrDefault();

            if (map != null)
            {
                var channel = _client.GetChannel(channelId) as SocketTextChannel;
                channel?.SendMessageAsync($"{score.Username} has scored {score.Score}{(score.FullCombo ? " (Full Combo!)" : "")} on {map.Name}!");
            }
        }

        public async Task<ulong> SendLeaderboardUpdate(ulong channelId, ulong messageId, List<Score> scores, List<Song> maps)
        {
            var channel = _client.GetChannel(channelId) as SocketTextChannel;
            RestUserMessage message;

            if (messageId == default) message = await channel.SendMessageAsync("Leaderboard Placeholder");
            else message = await channel.GetMessageAsync(messageId) as RestUserMessage;

            var builder = new EmbedBuilder
            {
                Title = "<:page_with_curl:735592941338361897> Leaderboards",
                Color = Color.Green
            };

            foreach (var map in maps)
            {
                var mapScores = scores.Where(x => x.LevelId == map.LevelId).OrderByDescending(x => x._Score);
                builder.AddField(map.Name, $"\n{string.Join("\n", mapScores.Select(x => $"`{x._Score,-8} {(x.FullCombo ? "FC" : "  ")} {x.Username}`"))}", false);
            }

            /*var uniqueScores = new List<(string, int)>();
            foreach (var player in scores.Select(x => x.Username).Distinct())
            {
                var total = 0;
                foreach (var playerScore in scores.Where(x => x.Username == player))
                {
                    total += playerScore._Score;
                }
                uniqueScores.Add((player, total));
            }

            builder.AddField("Overall Standings", $"```\n{string.Join("\n", uniqueScores.OrderByDescending(x => x.Item2).Select(x => $"{x.Item1} {x.Item2}\n"))}```", true);*/

            await message.ModifyAsync(x =>
            {
                x.Embed = builder.Build();
                x.Content = "";
            });

            return message.Id;
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }

        private IServiceProvider ConfigureServices()
        {
            var config = new DiscordSocketConfig
            {
                MessageCacheSize = 100,
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
                .AddSingleton(new DatabaseService())
                .AddSingleton(new TAServerService(_server))
                .BuildServiceProvider();
        }
    }
}
