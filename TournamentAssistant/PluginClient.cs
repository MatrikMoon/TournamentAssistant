using System;
using System.Threading.Tasks;
using TournamentAssistantShared;
using static TournamentAssistantShared.Models.Packets.Connect;

namespace TournamentAssistant
{
    public class PluginClient : SystemClient, IDisposable
    {
        public event Action<IBeatmapLevel>? LoadedSong;
        public event Action<PluginClient, Packet>? PacketReceived;
        public event Action<IPreviewBeatmapLevel, BeatmapCharacteristicSO, BeatmapDifficulty, GameplayModifiers, PlayerSpecificSettings, OverrideEnvironmentSettings, ColorScheme, bool, bool, bool, bool>? PlaySong;

        private readonly IPlatformUserModel _platformUserModel;

        public PluginClient(IPlatformUserModel platformUserModel)
        {
            _platformUserModel = platformUserModel;
        }

        public async Task Login(string address, int port)
        {
            var userInfo = await _platformUserModel.GetUserInfo();
            SetConnectionDetails(address, port, userInfo.userName, ConnectTypes.Player, userInfo.platformUserId);
            Start();
        }

        public void Logout()
        {
            Shutdown();
        }

        public void Dispose()
        {
            Logout();
        }
    }
}