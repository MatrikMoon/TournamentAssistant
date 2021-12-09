using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TournamentAssistantShared.Extensions;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantShared.BeatSaver
{
    public class BeatSaverDownloader
    {
        public event Action<Dictionary<string, string>> SongDownloadFinished;
        public event Action<KeyValuePair<string, string>> RetrySongDownloadFinished;
        
        Dictionary<string, Task<string>> TaskList { get; set; }
        private Dictionary<string, int> _progressList;
        Dictionary<string, int> ProgressList
        {
            get
            {
                return _progressList;
            }
            set
            {
                _progressList = value;
            }
        }

        public BeatSaverDownloader()
        {
            TaskList = new();
            _progressList = new();
            ProgressList = new();
        }

        /// <summary>
        /// Downloads all Songs in specified array. If specified, reports progress on specified IProgress interface. Can be cancelled.
        /// </summary>
        /// Parameters:
        /// <param name="songs">Array of songs to be downloaded</param>
        /// <param name="progress">IProgress interface to report on, if specified</param>
        /// <param name="token">CancellationToken to observe while waiting for the tasks to complete</param>
        public async Task GetSongs(SongInfo[] songs, IProgress<int> progress = null, CancellationToken token = default)
        {
            foreach (var song in songs.DistinctBy(song => song.CurrentVersion.hash))
            {
                IProgress<int> individualProgress = new Progress<int>(percent =>
                {
                    int progressPercent = 0;
                    if (ProgressList[song.CurrentVersion.hash] == percent) return; //Dump unnecessary updates
                    lock (ProgressList)
                    {
                        ProgressList[song.CurrentVersion.hash] = percent;
                        foreach (int item in ProgressList.Values)
                            progressPercent += item;
                    }
                    if (progress != null) progress.Report(decimal.ToInt32(decimal.Divide(progressPercent, ProgressList.Keys.Count)));
                    Logger.Debug($"Reported {decimal.ToInt32(decimal.Divide(progressPercent, ProgressList.Keys.Count))}% completion!");
                });

                TaskList.Add(song.CurrentVersion.hash, new Task<string>(() => GetSong(song, individualProgress).Result));
                ProgressList.Add(song.CurrentVersion.hash, 0);
            }

            foreach (var task in TaskList.Values)
            {
                task.Start();
                await Task.Delay(BeatsaverRateLimit);
                if (token.IsCancellationRequested) break;
            }

            //Wait for all tasks to finish
            try
            {
                await Task.WhenAny(Task.WhenAll(TaskList.Values.ToArray()), token.AsTask());
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Dictionary<string, string> outDict = new();
            foreach (var finishedTask in TaskList)
            {
                outDict.Add(finishedTask.Key, finishedTask.Value.Result);
            }

            SongDownloadFinished?.Invoke(outDict);
        }

        /// <summary>
        /// Tries to get song data path. Returns null if not found.
        /// </summary>
        /// <param name="song"></param>
        /// <returns>String path to song directory or null</returns>
        public static string TryGetSongDataPath(string songName)
        {
            //Make map name legal for a folder name
            var legalizedMapName = string.Empty;
            foreach (var illegalChar in IllegalPathCharacters)
            {
                legalizedMapName = songName.Replace(illegalChar, '_');
            }

            var songDir = $"{AppDataSongDataPath}{legalizedMapName}";
            if (!Directory.Exists(songDir))
            {
                return null;
            }

            return songDir;
        }

        //Moon's Note: How does this cancel? Why while (true)? I'm changing it to recursion for now
        /// <summary>
        /// Loops forever with a dialog until either user cancels or data is successfully downloaded
        /// </summary>
        /// <param name="song"></param>
        /// <param name="progress"></param>
        public async void RetrySongDownloadAsync(SongInfo song, IProgress<int> progress = null)
        {
            var songDir = await GetSong(song, progress);
            if (songDir == null)
            {
                var dialogResult = MessageBox.Show($"An error occured when trying to download song {song.name}", "DownloadError", MessageBoxButtons.RetryCancel, MessageBoxIcon.Exclamation);
                switch (dialogResult)
                {
                    case DialogResult.Cancel:
                        RetrySongDownloadFinished?.Invoke(new KeyValuePair<string, string>(song.CurrentVersion.hash, null));
                        break;
                    case DialogResult.Retry:
                        RetrySongDownloadAsync(song, progress);
                        break;
                    default:
                        break;
                }

                RetrySongDownloadFinished?.Invoke(new KeyValuePair<string, string>(song.CurrentVersion.hash, songDir));
            }
        }

        /// <summary>
        /// Downloads specified Song. Returns null value if error is encountered.
        /// </summary>
        /// <param name="song">Song object</param>
        /// <param name="progress">IProgress interface to report on, if specified</param>
        /// <returns>KeyValuePair with key as song hash and value of string representing song directory path</returns>
        public static async Task<DownloadedSong> GetSong(SongInfo song, IProgress<int> progress = null)
        {
            //Make map name legal for a folder name
            string legalizedMapName = song.name;
            foreach (char illegalChar in IllegalPathCharacters)
            {
                legalizedMapName = legalizedMapName.Replace(illegalChar, '_');
            }

            string url = $"{BeatsaverCDN}{song.CurrentVersion.hash}.zip";
            string zipPath = $"{AppDataTemp}{song.CurrentVersion.hash}.zip";
            string songDir = $"{AppDataSongDataPath}{legalizedMapName}";

            using var client = new WebClient();
            client.Headers.Add("user-agent", $"{SharedConstructs.Name}-v{SharedConstructs.Version}");

            try
            {
                Directory.CreateDirectory(songDir);

                client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) => {
                    if (progress != null) progress.Report(e.ProgressPercentage);
                };

                await client.DownloadFileTaskAsync(new Uri(url), zipPath);

                using (ZipArchive zip = ZipFile.OpenRead(zipPath))
                {
                    zip?.ExtractToDirectory(songDir);
                }
                File.Delete(zipPath);

                return new DownloadedSong(songDir);
            }
            catch (Exception e)
            {
                Logger.Error($"Error downloading {song.CurrentVersion.hash}.zip: {e}");
                return null;
            }
        }

        /*public static async Task<string> GetCoverAsync(string songHash, IProgress<int> prog = null)
        {
            if (!Directory.Exists($"{AppDataCache}{songHash}")) Directory.CreateDirectory($"{AppDataCache}{songHash}");
            using var client = new WebClient();
            client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) => { if (prog != null) prog.Report(e.ProgressPercentage); };
            client.Headers.Add("user-agent", $"{SharedConstructs.Name}-v{SharedConstructs.Version}");
            string url = $"{BeatsaverCDN}/{songHash.ToLower()}.jpg";
            await client.DownloadFileTaskAsync(url, $"{AppDataCache}{songHash}{Path.DirectorySeparatorChar}cover.jpg");
            return $"{AppDataCache}{songHash}{Path.DirectorySeparatorChar}cover.jpg";
        }*/

        /// <summary>
        /// Creates a new Song object and parses BeatSaver API info. Can report progress.
        /// </summary>
        /// <param name="id">Song ID to get song info</param>
        /// <param name="progress">IProgress interface to report progress on</param>
        /// <returns>A SongInfo object</returns>
        public async static Task<SongInfo> GetSongInfoByIDAsync(string id, IProgress<int> progress = null)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("user-agent", $"{SharedConstructs.Name}-v{SharedConstructs.Version}");

            var response = await client.GetAsync($"{MapInfoByID}{id.ToLower()}");
            return await HandleBeatSaverResponse(response);
        }

        /// <summary>
        /// Creates a new Song object and parses BeatSaver API info. Can report progress.
        /// </summary>
        /// <param name="hash">Song hash to get song info</param>
        /// <param name="progress">IProgress interface to report progress on</param>
        /// <returns>New Song object</returns>
        public static async Task<SongInfo> GetSongInfoByHashAsync(string hash, IProgress<int> progress = null)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("user-agent", $"{SharedConstructs.Name}-v{SharedConstructs.Version}");

            var response = await client.GetAsync($"{MapInfoByHash}{hash.ToLower()}");
            return await HandleBeatSaverResponse(response);
        }

        private async static Task<SongInfo> HandleBeatSaverResponse(HttpResponseMessage response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    break;
                case HttpStatusCode.NotFound:
                    Logger.Error($"Song {response.Headers.Location.AbsoluteUri} could not be found on BeatSaver.");
                    MessageBox.Show($"Song {response.Headers.Location.AbsoluteUri} could not be found on BeatSaver.", SharedConstructs.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                case HttpStatusCode.RequestTimeout:
                    Logger.Error($"Request to BeatSaver timed out");
                    MessageBox.Show($"Request to BeatSaver timed out, please check your internet connection.\nIf you are sure your internet connection is OK, BeatSaver might be down.", SharedConstructs.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                case HttpStatusCode.ServiceUnavailable:
                    Logger.Error("BeatSaver responded 503 (Service Unavailable)");
                    MessageBox.Show($"BeatSaver is currently unavailable, please try again later", SharedConstructs.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                default:
                    Logger.Error($"BeatSaver responded {response.StatusCode}\n" +
                        $"Reason: {response.ReasonPhrase}");
                    MessageBox.Show($"Something went wrong when requesting to BeatSaver.\n" +
                        $"Song request url: {response.Headers.Location.AbsoluteUri}\n" +
                        $"Server response code: {response.StatusCode}\n" +
                        $"Server response:\n{response.ReasonPhrase}", SharedConstructs.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
            }

            var data = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<SongInfo>(data);
        }

        public static async Task<PlaylistItem> PlaylistItemFromSongInfo(SongInfo songInfo, IProgress<int> progress = null)
        {
            var ret = new PlaylistItem
            {
                SongInfo = songInfo
            };

            var duration = TimeSpan.FromSeconds(songInfo.metadata.duration);
            var durationString = "";
            if (duration.Hours > 0)
            {
                durationString += duration.Hours.ToString();
            }

            durationString += $"{duration:mm\\:ss}";
            ret.DurationString = durationString;

            var handleProgressReport = new Progress<int>(percent =>
            {
                progress?.Report(percent);
            });

            progress?.Report(100);

            return ret;
        }
    }
}
