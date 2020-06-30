using TournamentAssistantShared.Discord.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace TournamentAssistantShared.Discord
{
    class QualifierBot
    {
        private DiscordSocketClient _client;
        private IServiceProvider _services;
        private string _botToken;
        private string _databaseLocation;

        public QualifierBot(string databaseLocation = "BotDatabase.db", string botToken = null)
        {
            _databaseLocation = databaseLocation;
            _botToken = botToken;
        }

        public void Start()
        {
            MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
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
                .AddSingleton(serviceProvider => new DatabaseService(_databaseLocation, serviceProvider))
                .BuildServiceProvider();
        }
    }
}
