#pragma warning disable 0649
using BeatSaberMarkupLanguage.Attributes;
using TMPro;
using TournamentAssistantShared.Models;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.UI.CustomListItems
{
    class TeamListItem
    {
        public Team team;

        [UIValue("team-name")]
        private string teamName;

        [UIValue("team-details")]
        private string teamDetails;

        [UIComponent("team-details-text")]
        private TextMeshProUGUI teamDetailsText;

        [UIComponent("bg")]
        private RawImage background;

        public TeamListItem(Team team)
        {
            this.team = team;
            teamName = team.Name;
            teamDetails = team.Id.ToString();
        }

        [UIAction("refresh-visuals")]
        public void Refresh(bool selected, bool highlighted)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);

            background.texture = texture;
            background.color = new Color(1f, 1f, 1f, 0.125f);
            teamDetailsText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        }
    }
}
