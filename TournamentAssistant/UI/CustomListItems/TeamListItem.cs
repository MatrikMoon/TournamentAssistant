#pragma warning disable 0649
using BeatSaberMarkupLanguage.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Team = TournamentAssistantShared.Models.Tournament.TournamentSettings.Team;

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
        private Image background;

        public TeamListItem(Team team)
        {
            this.team = team;
            teamName = team.Name;
            teamDetails = team.Guid.ToString();
        }

        [UIAction("refresh-visuals")]
        public void Refresh(bool selected, bool highlighted)
        {
            RoundedListHighlight.Apply(background, selected, highlighted);
            teamDetailsText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        }
    }
}
