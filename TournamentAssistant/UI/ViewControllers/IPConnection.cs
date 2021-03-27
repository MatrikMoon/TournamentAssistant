using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections.Generic;
using TournamentAssistantShared.Models;


namespace TournamentAssistant.UI.ViewControllers
{
    class IPConnection : BSMLResourceViewController
    {
        public override string ResourceName => $"TournamentAssistant.UI.Views.{GetType().Name}.bsml";

        public event Action<CoreServer> ServerSelected;
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        }


        [UIValue("ip")]
        private string ip = "";

        [UIValue("port")]
        private string port = "10156";

        [UIAction("ipConnect")]
        public void OnConnect()
        {
            CoreServer server = new()
            {
                Name = "Custom server",
                Address = ip,
                Port = Int32.Parse(port)
            };

            TournamentAssistantShared.Logger.Debug($"OnConnect invoked with values: {ip}:{port}");
            ServerSelected?.Invoke(server);
        }
    }
}