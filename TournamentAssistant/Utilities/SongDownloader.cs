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
            var levelId = $"custom_level_{hash.ToUpper()}";
            var customSongPath = Path.Combine(CustomLevelPathHelper.customLevelsDirectoryPath, hash);

            // If an earlier download already extracted this song, SongCore just hasn't loaded it yet.
            // Skip the download and go straight to the refresh
            if (!IsSongOnDisk(customSongPath))
            {
                var songUrl = $"{beatSaverDownloadUrl}{hash}.zip";

                if (!string.IsNullOrEmpty(customHostUrl))
                {
                    songUrl = $"{customHostUrl}{hash.ToUpper()}.zip";
                }

                var www = UnityWebRequest.Get(songUrl);
                www.SetRequestHeader("user-agent", Constants.NAME);
                var asyncRequest = www.SendWebRequest();

                // Give up if the download makes no progress for 15 seconds
                var timeout = false;
                var timeSinceProgress = 0f;
                var lastProgress = 0f;

                while (!asyncRequest.isDone)
                {
                    yield return null;

                    if (lastProgress != asyncRequest.progress)
                    {
                        lastProgress = asyncRequest.progress;
                        timeSinceProgress = 0f;
                        downloadProgressChanged?.Invoke(levelId, asyncRequest.progress);
                    }
                    else
                    {
                        timeSinceProgress += Time.deltaTime;
                        if (timeSinceProgress >= 15f)
                        {
                            www.Abort();
                            timeout = true;
                            break;
                        }
                    }
                }

                if (www.isNetworkError || www.isHttpError || timeout)
                {
                    Logger.Error($"Error downloading song {hash}: {(timeout ? "timed out" : www.error)}");
                    songDownloaded?.Invoke(levelId, false);
                    yield break;
                }

                if (!ExtractSong(www.downloadHandler.data, customSongPath))
                {
                    songDownloaded?.Invoke(levelId, false);
                    yield break;
                }

                Logger.Success($"Downloaded!");
            }

            if (refreshWhenDownloaded)
            {
                Action<Loader, ConcurrentDictionary<string, CustomPreviewBeatmapLevel>> songsLoaded = null;
                songsLoaded = (_, __) =>
                    {
                        Loader.SongsLoadedEvent -= songsLoaded;
                        songDownloaded?.Invoke(levelId, true);
                    };
                Loader.SongsLoadedEvent += songsLoaded;
                Loader.Instance.RefreshSongs(false);
            }
            else songDownloaded?.Invoke(levelId, true);
        }

        private static bool IsSongOnDisk(string customSongPath)
        {
            return File.Exists(Path.Combine(customSongPath, "Info.dat")) || File.Exists(Path.Combine(customSongPath, "info.dat"));
        }

        // Saves the downloaded zip into the song's folder and extracts it. Returns false if anything went wrong
        private static bool ExtractSong(byte[] zipData, string customSongPath)
        {
            var zipPath = Path.Combine(customSongPath, Path.GetFileName(customSongPath) + ".zip");

            try
            {
                // Always extract into an empty folder. ExtractToDirectory won't overwrite files, so anything
                // left over from an earlier, unfinished download would make it fail with "File already exists"
                if (Directory.Exists(customSongPath))
                {
                    Directory.Delete(customSongPath, true);
                }
                Directory.CreateDirectory(customSongPath);

                File.WriteAllBytes(zipPath, zipData);
                ZipFile.ExtractToDirectory(zipPath, customSongPath);
            }
            catch (Exception e)
            {
                Logger.Error($"Unable to extract song! Exception: {e}");
                return false;
            }

            // The song is ready at this point, so failing to clean up the zip shouldn't fail the download
            try
            {
                File.Delete(zipPath);
            }
            catch (IOException e)
            {
                Logger.Warning($"Unable to delete zip! Exception: {e}");
            }

            return true;
        }
    }
}
