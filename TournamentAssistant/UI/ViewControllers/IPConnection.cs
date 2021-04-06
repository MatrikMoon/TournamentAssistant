#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using TournamentAssistantShared.Models;
using UnityEngine;

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
            BackgroundOpacity();
        }

        [UIObject("Background")]
        internal GameObject Background = null;
        void BackgroundOpacity() //<- stolen from BS+
        {
            var Image = Background?.GetComponent<HMUI.ImageView>() ?? null;
            var Color = Image.color;
            Color.a = 0.5f;
            Image.color = Color;
        }

        [UIValue("ip")]
        private string ip = string.Empty;

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

            ServerSelected?.Invoke(server);
        }
    }
}
