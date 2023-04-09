#pragma warning disable 0649
#pragma warning disable 0414
using BeatSaberMarkupLanguage.Attributes;
using TMPro;
using TournamentAssistantShared;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.UI.CustomListItems
{
    public class TournamentListItem
    {
        public Scraper.TournamentWithServerInfo tournament;

        [UIValue("tournament-name")]
        private string tournamentName;

        [UIValue("tournament-details")]
        private string tournamentDetails;

        [UIComponent("tournament-details-text")]
        private TextMeshProUGUI serverDetailsText;

        [UIComponent("bg")]
        private RawImage background;

        public TournamentListItem(Scraper.TournamentWithServerInfo tournament)
        {
            this.tournament = tournament;
            tournamentName = tournament.Tournament.Settings.TournamentName;
            tournamentDetails = $"{tournament.Address}:{tournament.Port}";
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
