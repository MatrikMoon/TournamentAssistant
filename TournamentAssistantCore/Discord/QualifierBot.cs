using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TournamentAssistantCore.Discord.Database;
using TournamentAssistantCore.Discord.Services;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantCore.Discord
{
    public class QualifierBot
    {
        private DiscordSocketClient _client;
        private IServiceProvider _services;
        private string _botToken;
        private SystemServer _server;

        public QualifierDatabaseContext Database => _services?.GetService<DatabaseService>()?.DatabaseContext;

        public QualifierBot(string botToken = null, SystemServer server = null)
        {
            _botToken = botToken;
            _server = server;
        }

        public async Task Start()
        {
            _services = ConfigureServices();
            _services.GetRequiredService<CommandService>().Log += LogAsync;

            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.Log += LogAsync;

            if (_botToken == null) _botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
            if (_botToken == null) throw new ArgumentException("You must pass in a bot token, by setting it in the config, setting it as an environment variable, or passing it in as a command parameter");

            await _client.LoginAsync(TokenType.Bot, _botToken);
            await _client.StartAsync();

            await _services.GetRequiredService<CommandHandlingService>().InitializeAsync();
        }

        public void SendScoreEvent(ulong channelId, SubmitScore score)
        {
            var channel = _client.GetChannel(channelId) as SocketTextChannel;
            channel?.SendMessageAsync($"{score.Score.Username} has scored {score.Score._Score}{(score.Score.FullCombo ? " (Full Combo!)" : "")} on {score.Score.Parameters.Beatmap.Name}!");
        }

        public async Task<ulong> SendLeaderboardUpdate(ulong channelId, ulong messageId, List<Score> scores, List<Discord.Database.Song> maps)
        {
            var channel = _client.GetChannel(channelId) as SocketTextChannel;
            RestUserMessage message = await channel.GetMessageAsync(messageId) as RestUserMessage;
            if (messageId == default) message = await channel.SendMessageAsync("Leaderboard Placeholder");

            var builder = new EmbedBuilder();
            builder.Title = "<:page_with_curl:735592941338361897> Leaderboards";
            builder.Color = Color.Green;

            foreach (var map in maps)
            {
                var mapScores = scores.Where(x => x.LevelId == map.LevelId).OrderByDescending(x => x._Score);
                builder.AddField(map.Name, $"```\n{string.Join("\n", mapScores.Select(x => $"{x.Username} {x._Score} {(x.FullCombo ? "FC" : "")}\n"))}```", true);
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
            var config = new DiscordSocketConfig { MessageCacheSize = 100 };
            return new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(config))
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<HttpClient>()
                .AddSingleton<PictureService>()
                .AddSingleton<MessageUpdateService>()
                .AddSingleton<ScoresaberService>()
                .AddSingleton(new DatabaseService())
                .AddSingleton(new SystemServerService(_server))
                .BuildServiceProvider();
        }
    }
}
