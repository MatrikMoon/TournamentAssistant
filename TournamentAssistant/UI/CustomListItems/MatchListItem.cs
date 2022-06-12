#pragma warning disable 0649
using BeatSaberMarkupLanguage.Attributes;
using System.Linq;
using TMPro;
using TournamentAssistantShared.Models;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.UI.CustomListItems
{
    class MatchListItem
    {
        public Match match;

        [UIValue("match-name")]
        private string matchName;

        [UIValue("match-details")]
        private string matchDetails;

        [UIComponent("match-details-text")]
        private TextMeshProUGUI matchDetailsText;

        [UIComponent("bg")]
        private RawImage background;

        public MatchListItem(Match match)
        {
            this.match = match;
            matchName = $"{Plugin.client.GetUserByGuid(match.Leader).Name}\'s Room";
            matchDetails = match.Guid;
        }

        [UIAction("refresh-visuals")]
        public void Refresh(bool selected, bool highlighted)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);

            background.texture = texture;
            background.color = new Color(1f, 1f, 1f, 0.125f);
            matchDetailsText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        }
    }
}
