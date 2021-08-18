using SiraUtil;
using SiraUtil.Tools;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TournamentAssistant.Managers
{
    internal interface ILevelService
    {
        Task<IPreviewBeatmapLevel?> DownloadLevel(string name, string hash, string url, CancellationToken token, IProgress<double>? downloadProgress = null);
        IPreviewBeatmapLevel? TryGetLevel(string hash, bool custom = true);
    }

    internal class TALevelService : ILevelService
    {
        private readonly SiraLog _siraLog;
        private readonly SiraClient _siraClient;
        private readonly BeatmapLevelsModel _beatmapLevelsModel;

        public TALevelService(SiraLog siraLog, SiraClient siraClient, BeatmapLevelsModel beatmapLevelsModel)
        {
            _siraLog = siraLog;
            _siraClient = siraClient;
            _beatmapLevelsModel = beatmapLevelsModel;
        }

        public async Task<IPreviewBeatmapLevel?> DownloadLevel(string name, string hash, string url, CancellationToken token, IProgress<double>? downloadProgress = null)
        {
            var response = await _siraClient.SendAsync(HttpMethod.Get, url, token, progress: downloadProgress);
            if (!response.IsSuccessStatusCode)
            {
                _siraLog.Error(response.ContentToString());
                _siraLog.Error(response.StatusCode);
                return null;
            }

            var extractPath = await ExtractZipAsync(response.ContentToBytes(), name, CustomLevelPathHelper.customLevelsDirectoryPath);
            if (string.IsNullOrEmpty(extractPath))
                return null;

            var semaphoreSlim = new SemaphoreSlim(0, 1);
            void Release(SongCore.Loader _, ConcurrentDictionary<string, CustomPreviewBeatmapLevel> __)
            {
                SongCore.Loader.SongsLoadedEvent -= Release;
                semaphoreSlim?.Release();
            }

            try
            {
                SongCore.Loader.SongsLoadedEvent += Release;
                SongCore.Collections.AddSong($"custom_level_", "");
                SongCore.Loader.Instance.RefreshSongs(false);
                await semaphoreSlim.WaitAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                Release(null!, null!);
                _siraLog.Logger.Error(e);
                return null;
            }
            return TryGetLevel(hash);
        }

        public bool LevelIsInstalled(string hash, bool custom = true)
        {
            if (custom)
                hash = $"custom_level_{hash.ToUpper()}";
            bool levelExists = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.Any(bm => bm.beatmapLevelCollection.beatmapLevels.Any(lvl => lvl.levelID == hash));
            return levelExists;
        }

        public IPreviewBeatmapLevel? TryGetLevel(string hash, bool custom = true)
        {
            if (custom)
                hash = $"custom_level_{hash.ToUpper()}";
            return _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(bm => bm.beatmapLevelCollection.beatmapLevels).FirstOrDefault(lvl => lvl.levelID == hash);
        }

        private async Task<string> ExtractZipAsync(byte[] zip, string name, string customSongsPath, bool overwrite = false)
        {
            Stream zipStream = new MemoryStream(zip);
            try
            {
                string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                Regex r = new(string.Format("[{0}]", Regex.Escape(regexSearch)));

                ZipArchive archive = new(zipStream, ZipArchiveMode.Read);
                string basePath = name;
                string path = customSongsPath + "/" + r.Replace(basePath, ""); ;
                if (!overwrite && Directory.Exists(path))
                {
                    int pathNum = 1;
                    while (Directory.Exists(path + $" ({pathNum})"))
                        ++pathNum;
                    path += $" ({pathNum})";
                }

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                await Task.Run(() =>
                {
                    foreach (var entry in archive.Entries)
                    {
                        var entryPath = Path.Combine(path, entry.Name);
                        if (overwrite || !File.Exists(entryPath))
                            entry.ExtractToFile(entryPath, overwrite);
                    }
                }).ConfigureAwait(false);
                archive.Dispose();
                zipStream.Close();
                return path;
            }
            catch (Exception e)
            {
                _siraLog.Error(e);
                zipStream.Close();
                return "";
            }
        }
    }
}
