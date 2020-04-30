using SongCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;
using Logger = BattleSaberShared.Logger;

namespace BattleSaber
{
    public class SongDownloader
    {
        private static string beatSaverDownloadUrl = "https://beatsaver.com/api/download/hash/";

        public static void DownloadSong(string levelId, bool refreshWhenDownloaded = true, Action<bool> songDownloaded = null, Action<float> downloadProgressChanged = null)
        {
            SharedCoroutineStarter.instance.StartCoroutine(DownloadSong_internal(levelId.Replace("custom_level_", "").ToLower(), refreshWhenDownloaded, songDownloaded, downloadProgressChanged));
        }

        private static IEnumerator DownloadSong_internal(string hash, bool refreshWhenDownloaded = true, Action<bool> songDownloaded = null, Action<float> downloadProgressChanged = null)
        {
            UnityWebRequest www = UnityWebRequest.Get($"{beatSaverDownloadUrl}{hash}");
            bool timeout = false;
            float time = 0f;
            float lastProgress = 0f;

            www.SetRequestHeader("user-agent", @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/51.0.2704.106 Safari/537.36");
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
                    downloadProgressChanged?.Invoke(asyncRequest.progress);
                }
            }

            if (www.isNetworkError || www.isHttpError || timeout)
            {
                Logger.Error($"Error downloading song {hash}: {www.error}");
                songDownloaded?.Invoke(false);
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
                    songDownloaded?.Invoke(false);
                    yield break;
                }

                try
                {
                    ZipFile.ExtractToDirectory(zipPath, customSongPath);
                }
                catch (Exception e)
                {
                    Logger.Error($"Unable to extract ZIP! Exception: {e}");
                    songDownloaded?.Invoke(false);
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
                    Action<Loader, Dictionary<string, CustomPreviewBeatmapLevel>> songsLoaded = null;
                    songsLoaded = (_, __) =>
                        {
                            Loader.SongsLoadedEvent -= songsLoaded;
                            songDownloaded?.Invoke(true);
                        };
                    Loader.SongsLoadedEvent += songsLoaded;
                    Loader.Instance.RefreshSongs(false);
                }
                else songDownloaded?.Invoke(true);
            }
        }
    }
}
