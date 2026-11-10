#pragma warning disable 0649
#pragma warning disable 0414
using BeatSaberMarkupLanguage.Attributes;
using IPA.Utilities.Async;
using System;
using System.Linq;
using System.Threading;
using TMPro;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using UnityEngine;
using UnityEngine.UI;

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

        public Map map;

        [UIComponent("song-name-text")]
        private TextMeshProUGUI songNameText;

        [UIComponent("song-details-text")]
        private TextMeshProUGUI songDetailsText;

        [UIComponent("song-duration-text")]
        private TextMeshProUGUI songDurationText;

        [UIComponent("song-bpm-text")]
        private TextMeshProUGUI songBpmText;

        [UIComponent("song-bpm-icon")]
        private Image songBpmIcon;

        [UIComponent("cover-image")]
        private Image coverImage;

        [UIComponent("loading-bg")]
        private RawImage loadingBackground;

        [UIComponent("hover-bg")]
        private Image hoverBackground;

        private float downloadProgress = 0;
        private DownloadState downloadState = DownloadState.InProgress;
        private bool disposed;

        public IPreviewBeatmapLevel level;
        private Sprite coverImageSprite;

        private CancellationTokenSource cancellationToken;

        private static Color successColor = Color.clear;
        private static Color progressColor = new Color(1, 1, 1, 0.3f);
        private static Color failColor = new Color(1, 0, 0, 0.3f);
        private static Sprite metronomeIcon;

        private static float defaultHeight = 10f;
        private static float defaultWidth = 50f;

        public SongListItem(Map map)
        {
            this.map = map;
            cancellationToken = new CancellationTokenSource();

            if (OstHelper.IsOst(map.GameplayParameters.Beatmap.LevelId) || SongUtils.masterLevelList.Any(x => x.levelID.ToUpper() == map.GameplayParameters.Beatmap.LevelId.ToUpper()))
            {
                downloadState = DownloadState.Complete;
                level = SongUtils.masterLevelList.FirstOrDefault(x => x.levelID.ToUpper() == map.GameplayParameters.Beatmap.LevelId.ToUpper());
            }
            else
            {
                SongDownloader.DownloadSong(map.GameplayParameters.Beatmap.LevelId, true, OnSongDownloaded, OnDownloadProgress);
            }
        }

        [UIAction("refresh-visuals")]
        public void Refresh(bool selected, bool highlighted)
        {
            if (disposed) return;

            LoadCoverImage();

            RoundedListHighlight.Apply(hoverBackground, selected, highlighted, 10f);

            songDetailsText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
            songBpmText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
            metronomeIcon ??= Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(x => x.name == "MetronomeIcon");
            songBpmIcon.sprite = metronomeIcon;
            songBpmIcon.color = new Color(0.65f, 0.65f, 0.65f, 1f);
            songBpmIcon.enabled = metronomeIcon != null;

            loadingBackground.texture = Texture2D.whiteTexture;
            SetProgress(downloadProgress);
            SetTextForDownloadState(downloadState);
            SetColorForDownloadState(downloadState);
        }

        private void OnSongDownloaded(string levelId, bool success)
        {
            if (!disposed && levelId == map.GameplayParameters.Beatmap.LevelId)
            {
                UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    if (disposed) return;

                    downloadState = success ? DownloadState.Complete : DownloadState.Failed;
                    loadingBackground.color = downloadState == DownloadState.Complete ? Color.clear : failColor;

                    // FirstOrDefault as sometimes a level can not yet be refreshed into the master level list at this point.
                    // We'll have to deal with that below as well
                    if (downloadState == DownloadState.Complete)
                    {
                        level = SongUtils.masterLevelList.FirstOrDefault(x => x.levelID.ToUpper() == map.GameplayParameters.Beatmap.LevelId.ToUpper());
                    }

                    SetTextForDownloadState(downloadState);
                    SetColorForDownloadState(downloadState);
                    LoadCoverImage();
                });
            }
        }

        private void OnDownloadProgress(string levelId, float progress)
        {
            if (!disposed && levelId == map.GameplayParameters.Beatmap.LevelId)
            {
                UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    if (disposed) return;

                    downloadProgress = progress;
                    SetProgress(downloadProgress);
                });
            }
        }

        private void SetProgress(float progress)
        {
            if (loadingBackground != null)
            {
                loadingBackground.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                loadingBackground.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                loadingBackground.rectTransform.pivot = new Vector2(0f, 0.5f);
                loadingBackground.rectTransform.anchoredPosition = Vector2.zero;
                loadingBackground.rectTransform.sizeDelta = new Vector2(defaultWidth * progress, defaultHeight);
            }
            songDetailsText.text = $"{(int)(progress * 100)}%";
        }

        private void SetTextForDownloadState(DownloadState state)
        {
            switch (state)
            {
                case DownloadState.Complete:
                    level ??= SongUtils.masterLevelList.FirstOrDefault(x => x.levelID.ToUpper() == map.GameplayParameters.Beatmap.LevelId.ToUpper());

                    songNameText.text = level?.songName;
                    songDetailsText.text = level?.songAuthorName;
                    songDurationText.text = level != null
                        ? $"{(int)(level.songDuration / 60f)}:{(int)(level.songDuration % 60f):00}"
                        : string.Empty;
                    songBpmText.text = level != null ? Mathf.RoundToInt(level.beatsPerMinute).ToString() : string.Empty;
                    songDetailsText.richText = true;
                    if (!string.IsNullOrEmpty(level?.levelAuthorName))
                    {
                        songDetailsText.text += $" <size=80%>[{level?.levelAuthorName}]</size>";
                    }
                    break;
                case DownloadState.InProgress:
                    songNameText.text = "Loading...";
                    songDurationText.text = string.Empty;
                    songBpmText.text = string.Empty;
                    break;
                case DownloadState.Failed:
                    songNameText.text = "Download Error";
                    songDurationText.text = string.Empty;
                    songBpmText.text = string.Empty;
                    break;
            }
        }

        private void SetColorForDownloadState(DownloadState state)
        {
            switch (state)
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
            if (disposed) return;

            if (coverImageSprite == null && level != null)
            {
                try
                {
                    // This is the same borrowed Sprite used by Solo's LevelListTableCell.
                    coverImageSprite = await level.GetCoverImageAsync(cancellationToken.Token);
                }
                catch (OperationCanceledException) { return; }
            }

            if (disposed) return;

            coverImage.sprite = coverImageSprite;
            coverImage.preserveAspect = true;
            coverImage.color = coverImageSprite ? Color.white : Color.clear;
        }

        public void Dispose()
        {
            if (disposed) return;

            disposed = true;
            cancellationToken.Cancel();
            cancellationToken.Dispose();

            // coverImageSprite is owned by the beatmap level's cache, not this row.
            coverImageSprite = null;
        }
    }
}
