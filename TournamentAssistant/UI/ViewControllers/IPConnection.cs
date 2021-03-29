#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class IPConnection : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action<CoreServer> ServerSelected;
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        }


        [UIValue("ip")]
        private string ip = "192.168.0.24";

        [UIValue("port")]
        private string port = "10150";

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
