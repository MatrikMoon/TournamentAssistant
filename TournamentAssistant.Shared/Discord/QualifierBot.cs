using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TournamentAssistantShared.Discord.Database;
using TournamentAssistantShared.Discord.Services;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantShared.Discord
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
            var channel = _client.GroupChannels.FirstOrDefault(x => x.Id == channelId);
            channel?.SendMessageAsync($"{score.Score.Username} has scored {score.Score.Score_}{(score.Score.FullCombo ? "(Full Combo!)" : "")} on {score.Score.Parameters.Beatmap.Name}!");
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