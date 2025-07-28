#pragma warning disable 0649
#pragma warning disable 0414
using BeatSaberMarkupLanguage.Attributes;
using IPA.Utilities.Async;
using System;
using System.Threading.Tasks;
using TMPro;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using UnityEngine;
using UnityEngine.UI;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UI.CustomListItems
{
    public class TournamentListItem
    {
        public Tournament tournament;

        [UIValue("tournament-name")]
        private string tournamentName;

        [UIValue("tournament-details")]
        private string tournamentDetails;

        [UIComponent("tournament-image")]
        private RawImage tournamentImage;
        private Texture2D tournamentImageTexture;

        [UIComponent("tournament-details-text")]
        private TextMeshProUGUI serverDetailsText;

        [UIComponent("bg")]
        private RawImage background;

        public TournamentListItem(Tournament tournament)
        {
            this.tournament = tournament;
            tournamentName = tournament.Settings.TournamentName;
            tournamentDetails = tournament.Server.Name ?? $"{tournament.Server.Address}:{tournament.Server.Port}";
        }

        [UIAction("refresh-visuals")]
        public void Refresh(bool selected, bool highlighted)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);

            tournamentImage.color = Color.white;
            LoadTournamentImage();

            background.texture = texture;
            background.color = new Color(1f, 1f, 1f, 0.125f);
            serverDetailsText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        }

        private void LoadTournamentImage()
        {
            var defaultTexture = new Texture2D(1, 1);
            defaultTexture.SetPixel(0, 0, Color.clear);

            if (tournamentImageTexture == null && !string.IsNullOrWhiteSpace(tournament.Settings.TournamentImage))
            {
                try
                {
                    // Download images in a new thread
                    Task.Run(async () =>
                    {
                        var webTexture = await ImageDownloadManager.DownloadTexture($"https://{Constants.MASTER_SERVER}:{Constants.MASTER_API_PORT}/api/file/{tournament.Settings.TournamentImage}");

                        await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                        {
                            tournamentImageTexture = webTexture;
                            tournamentImage.texture = tournamentImageTexture ?? defaultTexture;
                        });
                    });

                }
                catch (Exception e)
                {
                    Logger.Error(e.Message);
                    Logger.Error(e.StackTrace);
                }
            }
        }
    }
}
