﻿using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography.X509Certificates;
using TournamentAssistantServer.Utilities;
using TournamentAssistantShared;

namespace TournamentAssistantServer
{
    internal class ServerConfig
    {
        // Server settings
        public Config Config { get; private set; }
        public string Address { get; private set; }
        public int Port { get; private set; }
        public string BotToken { get; private set; }
        public string ServerName { get; private set; }

        // Overlay settings
        public int WebsocketPort { get; private set; }

        // Oauth Settings
        public int OAuthPort { get; private set; }
        public string OAuthClientId { get; private set; }
        public string OAuthClientSecret { get; private set; }

        // Keys
#if DEBUG
        public X509Certificate2 ServerCert { get; } = new("files/server-dev.pfx", "password");
#else
        public X509Certificate2 ServerCert { get; } = new("files/server.pfx", "password");
#endif
        public RsaSecurityKey BeatKhanaPublicKey { get; private set; }
        public X509Certificate2 PluginCert { get; } = new("files/player.pfx", "TAPlayerPass");
#if DEBUG
        public X509Certificate2 MockCert { get; } = new("files/mock.pfx", "password");
#else
        public X509Certificate2 MockCert { get; } = null;
#endif

        public ServerConfig(string botTokenArg = null)
        {
            BeatKhanaPublicKey = new RsaSecurityKey(RSAUtilities.CreateRsaFromPublicKeyPem("files/beatkhana-public.pem"));

            Config = new Config("files/serverConfig.json");

            var portValue = Config.GetString("port");
            if (portValue == string.Empty)
            {
                portValue = "8675";
                Config.SaveString("port", portValue);
            }

            var nameValue = Config.GetString("serverName");
            if (nameValue == string.Empty)
            {
                nameValue = "Default Server Name";
                Config.SaveString("serverName", nameValue);
            }

            var addressValue = Config.GetString("serverAddress");
            if (addressValue == string.Empty || addressValue == "[serverAddress]")
            {
                addressValue = "[serverAddress]";
                Config.SaveString("serverAddress", addressValue);
            }

            var overlayPortValue = Config.GetString("overlayPort");
            if (overlayPortValue == string.Empty || overlayPortValue == "[overlayPort]")
            {
                overlayPortValue = "8676";
                Config.SaveString("overlayPort", overlayPortValue);
            }

            var oauthPortValue = Config.GetString("oauthPort");
            if (oauthPortValue == string.Empty || oauthPortValue == "[oauthPort]")
            {
                oauthPortValue = "8677";
                Config.SaveString("oauthPort", oauthPortValue);
            }

            var discordClientId = Config.GetString("discordClientId");
            if (discordClientId == string.Empty)
            {
                discordClientId = string.Empty;
                Config.SaveString("discordClientId", "[discordClientId]");
            }

            var discordClientSecret = Config.GetString("discordClientSecret");
            if (discordClientSecret == string.Empty)
            {
                discordClientSecret = string.Empty;
                Config.SaveString("discordClientSecret", "[discordClientSecret]");
            }

            var botTokenValue = Config.GetString("botToken");
            if (botTokenValue == string.Empty || botTokenValue == "[botToken]")
            {
                botTokenValue = botTokenArg;
                Config.SaveString("botToken", "[botToken]");
            }

            Address = addressValue;
            Port = int.Parse(portValue);
            WebsocketPort = int.Parse(overlayPortValue);
            OAuthPort = int.Parse(oauthPortValue);
            OAuthClientId = discordClientId;
            OAuthClientSecret = discordClientSecret;
            BotToken = botTokenValue;
            ServerName = nameValue;
        }
    }
}
