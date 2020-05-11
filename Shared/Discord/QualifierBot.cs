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

        public QualifierBot(string botToken, string databaseLocation = "botDatabase.db")
        {
            _botToken = botToken;
            _databaseLocation = databaseLocation;
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

            await _client.LoginAsync(TokenType.Bot, _botToken);
            await _client.StartAsync();

            await _services.GetRequiredService<CommandHandlingService>().InitializeAsync();
        }

        public IServiceProvider GetServices() => _services;

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
                .AddSingleton(serviceProvider => new DatabaseService(_databaseLocation, serviceProvider))
                .BuildServiceProvider();
        }
    }
}
