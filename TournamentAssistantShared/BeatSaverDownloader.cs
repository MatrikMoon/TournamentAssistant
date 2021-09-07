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
using static TournamentAssistantShared.GlobalConstants;

namespace TournamentAssistantShared
{
    public class BeatSaverDownloader
    {
        public event Action<Dictionary<string, string>> SongDownloadFinished;
        public event Action<KeyValuePair<string, string>> RetrySongDownloadFinished;
        
        Dictionary<string, Task<KeyValuePair<string, string>>> TaskList { get; set; }
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
        public async Task GetSongs(Song[] songs, IProgress<int> progress = null, CancellationToken token = default)
        {
            foreach (var song in songs.DistinctBy(song => song.Hash)) //Name your damn lambda parameters, I hate having to dig through large lambdas trying to figure out what X is, I have enough of that in algebra 
            {
                IProgress<int> individualProgress = new Progress<int>(percent =>
                {
                    int progressPercent = 0;
                    if (ProgressList[song.Hash] == percent) return; //Dump unnecessary updates
                    lock (ProgressList)
                    {
                        ProgressList[song.Hash] = percent;
                        foreach (int item in ProgressList.Values)
                            progressPercent += item;
                    }
                    if (progress != null) progress.Report(decimal.ToInt32(decimal.Divide(progressPercent, ProgressList.Keys.Count)));
                    Logger.Debug($"Reported {decimal.ToInt32(decimal.Divide(progressPercent, ProgressList.Keys.Count))}% completion!");
                });

                TaskList.Add(song.Hash, new Task<KeyValuePair<string, string>>(() => GetSong(song, individualProgress).Result));
                ProgressList.Add(song.Hash, 0);
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
            foreach (var finishedTask in TaskList.Values)
            {
                var result = finishedTask.Result;
                outDict.Add(result.Key, result.Value);
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
            string legalizedMapName = songName;
            foreach (char illegalChar in IllegalPathCharacters)
                legalizedMapName = legalizedMapName.Replace(illegalChar, '_');

            string songDir = $"{AppDataSongDataPath}{legalizedMapName}";

            if (Directory.GetDirectories(AppDataSongDataPath).All(directory => directory != songDir))
                return null;

            Logger.Success($"Success! Song {songName} already downloaded!");

            return songDir;
        }

        /// <summary>
        /// Loops forever with a dialog until either user cancels or data is successfully downloaded
        /// </summary>
        /// <param name="song"></param>
        /// <param name="progress"></param>
        public async void RetrySongDownloadAsync(Song song, IProgress<int> progress = null)
        {
            while (true)
            {
                var pair = await GetSong(song, progress);

                if (pair.Value == null)
                {
                    var dialogResult = MessageBox.Show($"An error occured when trying to download song {song.Name}", "DownloadError", MessageBoxButtons.RetryCancel, MessageBoxIcon.Exclamation);
                    switch (dialogResult)
                    {
                        case DialogResult.Cancel:
                            RetrySongDownloadFinished?.Invoke(new KeyValuePair<string, string>(song.Hash, null));
                            return; 
                        case DialogResult.Retry:
                            continue;
                        default:
                            continue;
                    }
                }

                RetrySongDownloadFinished?.Invoke(pair);
                break;
            }
        }

        /// <summary>
        /// Downloads specified Song. Returns null value if error is encountered.
        /// </summary>
        /// <param name="song">Song object</param>
        /// <param name="prog">IProgress interface to report on, if specified</param>
        /// <returns>KeyValuePair with key as song hash and value of string representing song directory path</returns>
        public static async Task<KeyValuePair<string, string>> GetSong(Song song, IProgress<int> prog = null)
        {
            //Make map name legal for a folder name
            string legalizedMapName = song.Name;
            foreach (char illegalChar in IllegalPathCharacters)
                legalizedMapName = legalizedMapName.Replace(illegalChar, '_');

            string url = $"{BeatsaverCDN}{song.Hash}.zip";
            string zipPath = $"{AppDataTemp}{song.Hash}.zip";
            string songDir = $"{AppDataSongDataPath}{legalizedMapName}";


            using var client = new WebClient();
            client.Headers.Add("user-agent", $"{SharedConstructs.Name}-v{SharedConstructs.Version}");

            try
            {
                Directory.CreateDirectory(songDir);


                client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) => { if (prog != null) prog.Report(e.ProgressPercentage); };
                await client.DownloadFileTaskAsync(new Uri(url), zipPath);


                using (ZipArchive zip = ZipFile.OpenRead(zipPath))
                    zip?.ExtractToDirectory(songDir);
                File.Delete(zipPath);


                return new KeyValuePair<string, string>(song.Hash, songDir);
            }
            catch (Exception e)
            {
                Logger.Error($"Error downloading {song.Hash}.zip: {e}");
                return new KeyValuePair<string, string>(song.Hash, null);
            }
        }

        public static async Task<HttpResponseMessage> GetSongInfoHashAsync(string hash)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", $"{SharedConstructs.Name}-v{SharedConstructs.Version}");
                var url = $"{MapInfoByHash}{hash.ToLower()}";
                return await client.GetAsync(url);
            }
        }

        public static async Task<HttpResponseMessage> GetSongInfoIDAsync(string id)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", $"{SharedConstructs.Name}-v{SharedConstructs.Version}");
                var url = $"{MapInfoByID}{id.ToLower()}";
                return await client.GetAsync(url);
            }
        }

        public static async Task<string> GetCoverAsync(string songHash, IProgress<int> prog = null)
        {
            if (!Directory.Exists($"{AppDataCache}{songHash}")) Directory.CreateDirectory($"{AppDataCache}{songHash}");
            using var client = new WebClient();
            client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) => { if (prog != null) prog.Report(e.ProgressPercentage); };
            client.Headers.Add("user-agent", $"{SharedConstructs.Name}-v{SharedConstructs.Version}");
            string url = $"{BeatsaverCDN}/{songHash.ToLower()}.jpg";
            await client.DownloadFileTaskAsync(url, $"{AppDataCache}{songHash}{Path.DirectorySeparatorChar}cover.jpg");
            return $"{AppDataCache}{songHash}{Path.DirectorySeparatorChar}cover.jpg";
        }
    }
}
