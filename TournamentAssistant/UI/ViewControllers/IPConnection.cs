#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using TournamentAssistantShared.Models;
using UnityEngine;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class IPConnection : BSMLAutomaticViewController
    {
        public event Action<CoreServer> ServerSelected;

        [UIValue("direct-connect-text")]
        private string directConnectText = Plugin.GetLocalized("direct_connect");

        [UIValue("ip-address-domain-name-text")]
        private string ipAddressDomainNameText = Plugin.GetLocalized("ip_address_domain_name");

        [UIValue("port-text")]
        private string portText = Plugin.GetLocalized("port");

        [UIValue("connect-text")]
        private string connectText = Plugin.GetLocalized("connect");

        [UIValue("connect-hint-text")]
        private string connectHintText = Plugin.GetLocalized("connect_hint");

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            BackgroundOpacity();
        }

        [UIObject("Background")]
        internal GameObject Background = null;
        void BackgroundOpacity()
        {
            var Image = Background?.GetComponent<HMUI.ImageView>() ?? null;
            var Color = Image.color;
            Color.a = 0.5f;
            Image.color = Color;
        }

        [UIValue("ip")]
        private string ip = string.Empty;

        [UIValue("port")]
        private string port = "8675";

        [UIAction("ipConnect")]
        public void OnConnect()
        {
            CoreServer server = new()
            {
                Name = "Custom server",
                Address = ip,
                Port = int.Parse(port)
            };

            ServerSelected?.Invoke(server);
        }
    }
}
