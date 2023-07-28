#pragma warning disable 0649
#pragma warning disable 0414
using BeatSaberMarkupLanguage.Attributes;
using System;
using System.Linq;
using System.Threading;
using TMPro;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/**
 * Created by Moon on 9/8/2020
 * Note: I'm not sure a list item should be handling downloads, but... Right now it seems...
 * Actually kinda logical? I mean if I wanna put the progress bar on the list item...
 * Maybe I should revisit this later.
 */

namespace TournamentAssistant.UI.CustomListItems
{
    class SongListItem : IDisposable
    {
        public enum DownloadState
        {
            InProgress,
            Complete,
            Failed
        }

        public GameplayParameters parameters;

        [UIComponent("song-name-text")]
        private TextMeshProUGUI songNameText;

        [UIComponent("song-details-text")]
        private TextMeshProUGUI songDetailsText;

        [UIComponent("cover-image")]
        private RawImage coverImage;

        [UIComponent("loading-bg")]
        private RawImage loadingBackground;

        [UIComponent("hover-bg")]
        private RawImage hoverBackround;

        private float downloadProgress = 0;
        private DownloadState downloadState = DownloadState.InProgress;

        public IPreviewBeatmapLevel level;
        private Texture2D coverImageTexture;

        private CancellationTokenSource cancellationToken;

        private static Color successColor = Color.clear;
        private static Color progressColor = new(0, 1, 0, 0.125f);
        private static Color failColor = new(1, 0, 0, 0.125f);
        private static float defaultHeight = 10f;
        private static float defaultWidth = 60f;

        public SongListItem(GameplayParameters parameters)
        {
            this.parameters = parameters;
            cancellationToken = new CancellationTokenSource();

            if (OstHelper.IsOst(parameters.Beatmap.LevelId) || SongUtils.masterLevelList.Any(x => x.levelID == parameters.Beatmap.LevelId))
            {
                downloadState = DownloadState.Complete;
                level = SongUtils.masterLevelList.First(x => x.levelID == parameters.Beatmap.LevelId);
            }
            else
            {
                SongDownloader.DownloadSong(parameters.Beatmap.LevelId, true, OnSongDownloaded, OnDownloadProgress);
            }
        }

        [UIAction("refresh-visuals")]
        public void Refresh(bool selected, bool highlighted)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);

            coverImage.color = Color.white;
            LoadCoverImage();

            hoverBackround.texture = texture;
            hoverBackround.color = new Color(1f, 1f, 1f, 0.125f);

            songDetailsText.color = new Color(0.65f, 0.65f, 0.65f, 1f);

            loadingBackground.texture = texture;
            SetProgress(downloadProgress);
            SetTextForDownloadState(downloadState);
            SetColorForDownloadState(downloadState);
        }

        private void OnSongDownloaded(string levelId, bool success)
        {
            if (levelId == parameters.Beatmap.LevelId)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    downloadState = success ? DownloadState.Complete : DownloadState.Failed;
                    loadingBackground.color = downloadState == DownloadState.Complete ? Color.clear : failColor;

                    //FirstOrDefault as sometimes a level can not yet be refreshed into the master level list at this point.
                    //We'll have to deal with that below as well
                    if (downloadState == DownloadState.Complete) level = SongUtils.masterLevelList.FirstOrDefault(x => x.levelID == parameters.Beatmap.LevelId);
                    SetTextForDownloadState(downloadState);
                    SetColorForDownloadState(downloadState);
                    LoadCoverImage();
                });
            }
        }

        private void OnDownloadProgress(string levelId, float progress)
        {
            if (levelId == parameters.Beatmap.LevelId)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    downloadProgress = progress;
                    SetProgress(downloadProgress);
                });
            }
        }

        private void SetProgress(float progress)
        {
            if (loadingBackground != null) loadingBackground.rectTransform.sizeDelta = new Vector2(defaultWidth * progress, defaultHeight);
            songDetailsText.text = $"{(int)(progress * 100)}%";
        }

        private void SetTextForDownloadState(DownloadState state)
        {
            switch (state)
            {
                case DownloadState.Complete:
                    if (level == null) level = SongUtils.masterLevelList.FirstOrDefault(x => x.levelID == parameters.Beatmap.LevelId);

                    songNameText.text = level?.songName;
                    songDetailsText.text = level?.songAuthorName;
                    songDetailsText.richText = true;
                    if (!string.IsNullOrEmpty(level?.levelAuthorName)) songDetailsText.text += $" <size=80%>[{level?.levelAuthorName}]</size>";
                    break;
                case DownloadState.InProgress:
                    songNameText.text = "Loading...";
                    break;
                case DownloadState.Failed:
                    songNameText.text = "Download Error";
                    break;
            }
        }

        private void SetColorForDownloadState(DownloadState state)
        {
            switch (downloadState)
            {
                case DownloadState.Complete:
                    loadingBackground.color = successColor;
                    break;
                case DownloadState.InProgress:
                    loadingBackground.color = progressColor;
                    break;
                case DownloadState.Failed:
                    loadingBackground.color = failColor;
                    break;
            }
        }

        private async void LoadCoverImage()
        {
            var defaultTexture = new Texture2D(1, 1);
            defaultTexture.SetPixel(0, 0, Color.clear);

            if (coverImageTexture == null && level != null)
            {
                //The dimensions of the list item are 60x10, so we want to get the top 1/6th of the cover image
                var uncroppedTexture = (await level.GetCoverImageAsync(cancellationToken.Token)).texture;
                if (uncroppedTexture != null)
                {
                    //GetPixels throws a texture unreadable error when trying to read OST textures
                    //We'll just have to squash it
                    try
                    {
                        var rippedColors = uncroppedTexture.GetPixels(0, 0, uncroppedTexture.width, uncroppedTexture.height / 6);
                        coverImageTexture = new Texture2D(uncroppedTexture.width, uncroppedTexture.height / 6);
                        coverImageTexture.SetPixels(rippedColors);
                        coverImageTexture.Apply();
                    }
                    catch { coverImageTexture = uncroppedTexture; }
                }
            }

            coverImage.texture = coverImageTexture ?? defaultTexture;
        }

        public void Dispose()
        {
            if (level is CustomPreviewBeatmapLevel &&
                coverImageTexture != null)
            //(level as CustomPreviewBeatmapLevel).GetField<Texture2D>("_coverImageTexture2D") != null &&
            //!OstHelper.IsOst(level.levelID))
            {
                //Object.Destroy((level as CustomPreviewBeatmapLevel).GetField<Texture2D>("_coverImageTexture2D"));
                Object.Destroy(coverImageTexture);
            }
        }
    }
}
