using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using static TournamentAssistantShared.Models.Packets.Connect;

namespace TournamentAssistant
{
    public class PluginClient : SystemClient, IDisposable
    {
        public event Action<IBeatmapLevel>? LoadedSong;
        public event Action<PluginClient, Packet>? PacketReceived;
        public event Action<IPreviewBeatmapLevel, BeatmapCharacteristicSO, BeatmapDifficulty, GameplayModifiers, PlayerSpecificSettings, OverrideEnvironmentSettings, ColorScheme, bool, bool, bool, bool>? PlaySong;

        private readonly Config _config;
        private readonly IPlatformUserModel _platformUserModel;

        public PluginClient(Config config, IPlatformUserModel platformUserModel)
        {
            _config = config;
            _platformUserModel = platformUserModel;
        }

        public async Task<Dictionary<CoreServer, State>> GetCoreServers()
        {
            var userInfo = await _platformUserModel.GetUserInfo();
            var scraped = (await HostScraper.ScrapeHosts(_config.GetHosts(), userInfo.userName, ulong.Parse(userInfo.platformUserId))).Where(x => x.Value != null).ToDictionary(s => s.Key, s => s.Value);

            //Since we're scraping... Let's save the data we learned about the hosts while we're at it
            var newHosts = _config.GetHosts().Union(scraped.Values.Where(x => x.KnownHosts != null).SelectMany(x => x.KnownHosts)).ToList();
            _config.SaveHosts(newHosts.ToArray());

            return scraped;
        }

        public async Task Login(string address, int port)
        {
            var userInfo = await _platformUserModel.GetUserInfo();
            SetConnectionDetails(address, port, userInfo.userName, ConnectTypes.Player, userInfo.platformUserId);
            Start();
        }

        public void Logout()
        {
            if (Connected)
                Shutdown();
        }

        public void Dispose()
        {
            Logout();
        }
    }
}