using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TournamentAssistantDiscordBot.Discord.Services;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Discord;

namespace TournamentAssistantDiscordBot.Discord
{
    public class QualifierBot
    {
        private static Random random = new();

        private DiscordSocketClient _client;
        private IServiceProvider _services;
        private string _botToken;

        private TAInteractionService _taInteractionService;

        public QualifierBot(Func<string, Task<List<Tournament>>> getTournamentsWhereUserIsAdmin, Action<string, string, List<Role>> addAuthorizedUser, string botToken = null)
        {
            _botToken = botToken;
            _taInteractionService = new TAInteractionService(getTournamentsWhereUserIsAdmin, addAuthorizedUser);
        }

        public async Task Start()
        {
            _services = ConfigureServices();
            _services.GetRequiredService<InteractionService>().Log += LogAsync;

            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.Log += LogAsync;

            if (_botToken == null) _botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
            if (_botToken == null) throw new ArgumentException("You must pass in a bot token, by setting it in the config, setting it as an environment variable, or passing it in as a command parameter");

            await _services.GetRequiredService<InteractionHandler>().InitializeAsync();

            await _client.LoginAsync(TokenType.Bot, _botToken);
            await _client.StartAsync();
        }

        public async Task<(string?, string?)> GetAccountInfo(string accountId)
        {
            var userInfo = await _client.GetUserAsync(ulong.Parse(accountId));

            return (userInfo?.Username, userInfo?.GetDisplayAvatarUrl());
        }

        public void SendMessage(Channel channel, string message)
        {
            var socketChannel = _client.GetChannel(ulong.Parse(channel.Id)) as SocketTextChannel;
            socketChannel?.SendMessageAsync(message);
        }

        public void SendScoreEvent(string channelId, string mapName, LeaderboardEntry score)
        {
            var channel = _client.GetChannel(ulong.Parse(channelId)) as SocketTextChannel;
            channel?.SendMessageAsync($"{score.Username} has scored {score.ModifiedScore}{(score.FullCombo ? " (Full Combo!)" : "")} on {mapName}!");
        }

        public async Task<string> SendLeaderboardUpdate(string channelId, string messageId, string mapId, string mapName, LeaderboardEntry[] scores, string tournamentName)
        {
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

            var scoreText = $"\n{string.Join("\n", scores.Select(x => $"`{x.ModifiedScore,-8:N0} {(x.FullCombo ? "FC" : "  ")}  {x.Username}`"))}";

            var builder = new EmbedBuilder()
                .WithTitle($"<:page_with_curl:735592941338361897> {tournamentName} Leaderboards")
                .WithColor(new Color(random.Next(255), random.Next(255), random.Next(255)))
                .WithFooter("Retrieved: ")
                .WithCurrentTimestamp();

            if (scoreText.Length > 1024)
            {
                var fieldText = scoreText[..scoreText[..1024].LastIndexOf("\n")];
                scoreText = scoreText[(scoreText[..1024].LastIndexOf("\n") + 1)..];
                builder.AddField(mapName, fieldText);

                while (scoreText.Length > 0)
                {
                    if (scoreText.Length > 1024)
                    {
                        fieldText = scoreText[..scoreText[..1024].LastIndexOf("\n")];
                        scoreText = scoreText[(scoreText[..1024].LastIndexOf("\n") + 1)..];
                        builder.AddField($"{mapName} cont.", fieldText);
                    }
                    else
                    {
                        fieldText = scoreText;
                        scoreText = "";
                        builder.AddField($"{mapName} cont.", fieldText);
                    }
                }
            }
            else
            {
                builder.AddField(mapName, scoreText);
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

        private IServiceProvider ConfigureServices()
        {
            var config = new DiscordSocketConfig
            {
                MessageCacheSize = 0,
                GatewayIntents = (GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers) & ~(GatewayIntents.GuildScheduledEvents | GatewayIntents.GuildInvites),
                LogGatewayIntentWarnings = true,
            };

            return new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(config))
                .AddSingleton(provider => new InteractionService(provider.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<InteractionHandler>()
                .AddSingleton<HttpClient>()
                .AddSingleton<PictureService>()
                .AddSingleton<ScoresaberService>()
                .AddSingleton(_taInteractionService)
                .BuildServiceProvider();
        }
    }
}
