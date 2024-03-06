using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.Database.Contexts;
using TournamentAssistantServer.Discord.Services;
using TournamentAssistantServer.Helpers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Discord;

namespace TournamentAssistantServer.Discord
{
    public class QualifierBot
    {
        private static Random random = new();

        private DiscordSocketClient _client;
        private IServiceProvider _services;
        private string _botToken;
        private TAServer _server;

        public TournamentDatabaseContext NewTournamentDatabaseContext() => _services?.GetService<DatabaseService>()?.NewTournamentDatabaseContext();
        public QualifierDatabaseContext NewQualifierDatabaseContext() => _services?.GetService<DatabaseService>()?.NewQualifierDatabaseContext();
        public UserDatabaseContext NewUserDatabaseContext() => _services?.GetService<DatabaseService>()?.NewUserDatabaseContext();

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

        public void SendScoreEvent(string channelId, LeaderboardEntry score)
        {
            using var qualifierDatabase = NewQualifierDatabaseContext();
            var map = qualifierDatabase.Songs.Where(x => x.Guid == score.MapId).FirstOrDefault();
            if (map != null)
            {
                var channel = _client.GetChannel(ulong.Parse(channelId)) as SocketTextChannel;
                channel?.SendMessageAsync($"{score.Username} has scored {score.ModifiedScore}{(score.FullCombo ? " (Full Combo!)" : "")} on {map.Name}!");
            }
        }

        public async Task<string> SendLeaderboardUpdate(string channelId, string messageId, string mapId)
        {
            using var tournamentDatabase = NewTournamentDatabaseContext();
            using var qualifierDatabase = NewQualifierDatabaseContext();

            var song = qualifierDatabase.Songs.First(x => x.Guid == mapId);
            var qualifier = qualifierDatabase.Qualifiers.First(x => x.Guid == song.EventId);
            var tournament = tournamentDatabase.Tournaments.First(x => x.Guid == qualifier.TournamentId);

            var channel = _client.GetChannel(ulong.Parse(channelId)) as SocketTextChannel;
            
            RestUserMessage message = null;
            if (!string.IsNullOrWhiteSpace(messageId))
            {
                message = await channel.GetMessageAsync(ulong.Parse(messageId)) as RestUserMessage;
            }

            if (message == null)
            {
                File.AppendAllText("leaderboardDebug.txt", $"Creating new leaderboard: c-{channelId} m-{messageId} map-{mapId} cache-{channel.CachedMessages.Count}\n");

                message = await channel.SendMessageAsync("Leaderboard Placeholder");
            }

            var scores = qualifierDatabase.Scores.Where(x => x.MapId == song.Guid && !x.Old).OrderByDescending(x => x.ModifiedScore);
            var scoreText = $"\n{string.Join("\n", scores.Select(x => $"`{x.ModifiedScore,-8:N0} {(x.FullCombo ? "FC" : "  ")}  {x.Username}`"))}";

            var builder = new EmbedBuilder()
                .WithTitle($"<:page_with_curl:735592941338361897> {tournament.Name} Leaderboards")
                .WithColor(new Color(random.Next(255), random.Next(255), random.Next(255)))
                .WithFooter("Retrieved: ")
                .WithCurrentTimestamp();

            if (scoreText.Length > 1024)
            {
                var fieldText = scoreText[..scoreText[..1024].LastIndexOf("\n")];
                scoreText = scoreText[(scoreText[..1024].LastIndexOf("\n") + 1)..];
                builder.AddField(song.Name, fieldText);

                while (scoreText.Length > 0)
                {
                    if (scoreText.Length > 1024)
                    {
                        fieldText = scoreText[..scoreText[..1024].LastIndexOf("\n")];
                        scoreText = scoreText[(scoreText[..1024].LastIndexOf("\n") + 1)..];
                        builder.AddField($"{song.Name} cont.", fieldText);
                    }
                    else
                    {
                        fieldText = scoreText;
                        scoreText = "";
                        builder.AddField($"{song.Name} cont.", fieldText);
                    }
                }
            }
            else
            {
                builder.AddField(song.Name, scoreText);
            }

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
