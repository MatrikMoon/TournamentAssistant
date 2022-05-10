using SongCore;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using TournamentAssistantShared;
using UnityEngine;
using UnityEngine.Networking;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.Utilities
{
    public class SongDownloader
    {
        private static string beatSaverDownloadUrl = "https://cdn.beatsaver.com/";

        public static void DownloadSong(string levelId, bool refreshWhenDownloaded = true, Action<string, bool> songDownloaded = null, Action<string, float> downloadProgressChanged = null, string customHostUrl = null)
        {
            DownloadSongs(new List<string> { levelId.Replace("custom_level_", "").ToLower() }, refreshWhenDownloaded, songDownloaded, downloadProgressChanged, customHostUrl);
        }

        public static void DownloadSongs(List<string> songHashes, bool refreshWhenDownloaded = true, Action<string, bool> songDownloaded = null, Action<string, float> downloadProgressChanged = null, string customHostUrl = null)
        {
            SharedCoroutineStarter.instance.StartCoroutine(DownloadSongs_internal(songHashes, refreshWhenDownloaded, songDownloaded, downloadProgressChanged, customHostUrl));
        }

        private static IEnumerator DownloadSongs_internal(List<string> songHashes, bool refreshWhenDownloaded = true, Action<string, bool> songDownloaded = null, Action<string, float> downloadProgressChanged = null, string customHostUrl = null)
        {
            List<IEnumerator> downloadCoroutines = new List<IEnumerator>();
            songHashes.ForEach(x => downloadCoroutines.Add(DownloadSong_internal(x, refreshWhenDownloaded, songDownloaded, downloadProgressChanged, customHostUrl)));
            yield return SharedCoroutineStarter.instance.StartCoroutine(new ParallelCoroutine().ExecuteCoroutines(downloadCoroutines.ToArray()));
        }

        private static IEnumerator DownloadSong_internal(string hash, bool refreshWhenDownloaded = true, Action<string, bool> songDownloaded = null, Action<string, float> downloadProgressChanged = null, string customHostUrl = null)
        {
            var songUrl = $"{beatSaverDownloadUrl}{hash}.zip";
            if (!string.IsNullOrEmpty(customHostUrl))
            {
                songUrl = $"{customHostUrl}{hash.ToUpper()}.zip";
            }
            UnityWebRequest www = UnityWebRequest.Get(songUrl);
            bool timeout = false;
            float time = 0f;
            float lastProgress = 0f;

            www.SetRequestHeader("user-agent", Constants.Name);
            UnityWebRequestAsyncOperation asyncRequest = www.SendWebRequest();

            while (!asyncRequest.isDone || asyncRequest.progress < 1f)
            {
                yield return null;

                time += Time.deltaTime;

                if (time >= 15f && asyncRequest.progress == 0f)
                {
                    www.Abort();
                    timeout = true;
                }

                if (lastProgress != asyncRequest.progress)
                {
                    lastProgress = asyncRequest.progress;
                    downloadProgressChanged?.Invoke($"custom_level_{hash.ToUpper()}", asyncRequest.progress);
                }
            }

            if (www.isNetworkError || www.isHttpError || timeout)
            {
                Logger.Error($"Error downloading song {hash}: {www.error}");
                songDownloaded?.Invoke($"custom_level_{hash.ToUpper()}", false);
            }
            else
            {
                string zipPath = "";
                string customSongsPath = CustomLevelPathHelper.customLevelsDirectoryPath;
                string customSongPath = "";

                byte[] data = www.downloadHandler.data;

                try
                {
                    customSongPath = customSongsPath + "/" + hash + "/";
                    zipPath = customSongPath + hash + ".zip";
                    if (!Directory.Exists(customSongPath))
                    {
                        Directory.CreateDirectory(customSongPath);
                    }
                    File.WriteAllBytes(zipPath, data);
                }
                catch (Exception e)
                {
                    Logger.Error($"Error writing zip: {e}");
                    songDownloaded?.Invoke($"custom_level_{hash.ToUpper()}", false);
                    yield break;
                }

                try
                {
                    ZipFile.ExtractToDirectory(zipPath, customSongPath);
                }
                catch (Exception e)
                {
                    Logger.Error($"Unable to extract ZIP! Exception: {e}");
                    songDownloaded?.Invoke($"custom_level_{hash.ToUpper()}", false);
                    yield break;
                }

                try
                {
                    File.Delete(zipPath);
                }
                catch (IOException e)
                {
                    Logger.Warning($"Unable to delete zip! Exception: {e}");
                    yield break;
                }

                Logger.Success($"Downloaded!");

                if (refreshWhenDownloaded)
                {
                    Action<Loader, ConcurrentDictionary<string, CustomPreviewBeatmapLevel>> songsLoaded = null;
                    songsLoaded = (_, __) =>
                        {
                            Loader.SongsLoadedEvent -= songsLoaded;
                            songDownloaded?.Invoke($"custom_level_{hash.ToUpper()}", true);
                        };
                    Loader.SongsLoadedEvent += songsLoaded;
                    Loader.Instance.RefreshSongs(false);
                }
                else songDownloaded?.Invoke($"custom_level_{hash.ToUpper()}", true);
            }
        }
    }
}
