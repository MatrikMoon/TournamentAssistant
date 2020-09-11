#pragma warning disable 0649
#pragma warning disable 0414
using BeatSaberMarkupLanguage.Attributes;
using TMPro;
using TournamentAssistantShared.Models;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.UI.CustomListItems
{
    public class ServerListItem
    {
        public CoreServer server;

        [UIValue("server-name")]
        private string serverName;

        [UIValue("server-details")]
        private string serverDetails;

        [UIComponent("server-details-text")]
        private TextMeshProUGUI serverDetailsText;

        [UIComponent("bg")]
        private RawImage background;

        public ServerListItem(CoreServer server)
        {
            this.server = server;
            serverName = server.Name;
            serverDetails = $"{server.Address}:{server.Port}";
        }

        [UIAction("refresh-visuals")]
        public void Refresh(bool selected, bool highlighted)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);

            background.texture = texture;
            background.color = new Color(1f, 1f, 1f, 0.125f);
            serverDetailsText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        }
    }
}
