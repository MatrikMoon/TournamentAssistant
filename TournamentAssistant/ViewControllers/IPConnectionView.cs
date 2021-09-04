using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.ip-connection-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\ip-connection-view.bsml")]
    internal class IPConnectionView : BSMLAutomaticViewController
    {
        public event Action<CoreServer>? ServerSelected;

        [UIValue("ip")]
        protected string IP { get; set; } = string.Empty;

        [UIValue("port")]
        protected string Port { get; set; } = "10156";

        [UIComponent("background")]
        protected readonly ImageView _background = null!;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            _background.color = _background.color.ColorWithAlpha(0.5f);
        }

        [UIAction("ip-connect")]
        protected void Connect()
        {
            if (!int.TryParse(Port, out int port))
                return;

            CoreServer server = new()
            {
                Name = "Custom Server",
                Address = IP,
                Port = port,
            };
            ServerSelected?.Invoke(server);
        }
    }
}