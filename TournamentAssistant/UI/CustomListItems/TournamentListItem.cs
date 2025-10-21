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
        private bool loadingImage;

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

            LoadTournamentImage();

            background.texture = texture;
            background.color = new Color(1f, 1f, 1f, 0.125f);
            serverDetailsText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        }

        private void LoadTournamentImage()
        {
            var defaultTexture = new Texture2D(1, 1);
            defaultTexture.SetPixel(0, 0, Color.clear);

            if (!string.IsNullOrWhiteSpace(tournament.Settings.TournamentImage))
            {
                if (tournamentImage.texture == Texture2D.blackTexture)
                {
                    // If the texture *was* loaded, but unity has re-set the image texture to black, we can just set it back to the texture we still have in memory.
                    // This is probably performance heresy, but you'll have to sue me to get me to regret it
                    if (tournamentImageTexture != null)
                    {
                        tournamentImage.texture = tournamentImageTexture ?? defaultTexture;
                    }
                    else if (ImageDownloadManager.IsCached(tournament.Settings.TournamentImage))
                    {
                        tournamentImageTexture = ImageDownloadManager.GetCached(tournament.Settings.TournamentImage);
                        tournamentImage.texture = tournamentImageTexture ?? defaultTexture;
                    }
                    else if (!loadingImage)
                    {
                        loadingImage = true;

                        // Download images in a new thread
                        Task.Run(async () =>
                        {
                            try
                            {
                                var webTexture = await ImageDownloadManager.DownloadTexture($"https://{Constants.MASTER_SERVER}:{Constants.MASTER_API_PORT}/api/file/{tournament.Settings.TournamentImage}", tournament.Settings.TournamentImage);

                                await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                                {
                                    tournamentImageTexture = webTexture;
                                    tournamentImage.texture = tournamentImageTexture ?? defaultTexture;
                                });
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e.Message);
                                Logger.Error(e.StackTrace);
                            }
                        });
                    }
                }
            }
            else if (string.IsNullOrWhiteSpace(tournament.Settings.TournamentImage))
            {
                tournamentImage.color = Color.white;
            }
        }
    }
}
