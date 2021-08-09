using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared;

namespace TournamentAssistantShared
{
    class BeatSaverDownloader : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public const string BeatsaverCDN = "https://cdn.beatsaver.com";
        public const string BeatsaverAPI = "https://api.beatsaver.com";
        public static string EnvironmentPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\TournamentAssistantUI";
        public static string EnvironmentTemp = $"{EnvironmentPath}\\Temp";
        Dictionary<string, Task<string>> TaskList { get; set; }
        private Dictionary<string, int> _progressList;
        //ProgressList can be modified by any worker thread, so needs thread safe accessing
        Dictionary<string, int> ProgressList
        {
            get
            {
                return _progressList;
            }
            set
            {
                _progressList = value;
                NotifyPropertyChanged(nameof(ProgressList));
            }
        }

        public BeatSaverDownloader()
        {
            TaskList = new();
            _progressList = new();
            ProgressList = new();
        }


        /// <summary>
        /// Downloads all Songs in specified array. If specified, reports progress on specified IProgress interface. If specified may be cancelled.
        /// </summary>
        /// Parameters:
        /// <param name="songs">Array of songs to be downloaded</param>
        /// <param name="progress">IProgress interface to report on, if specified</param>
        /// <param name="token">CancellationToken to observe while waiting for the tasks to complete</param>
        public void GetSongs(Song[] songs, IProgress<int> progress = null, CancellationToken token = new CancellationToken())
        {
            foreach (var song in songs)
            {
                IProgress<int> prog = new Progress<int>(percent =>
                {
                    int progressPercent = 0;

                    if (ProgressList[song.Hash] == percent) return;

                    lock (ProgressList)
                    {
                        ProgressList[song.Hash] = percent;
                        foreach (int item in ProgressList.Values)
                            progressPercent += item;
                    }

                    if (progress != null) progress.Report(Decimal.ToInt32(Decimal.Divide(progressPercent, ProgressList.Keys.Count)));
                    Logger.Debug($"[{this}]: Reported {Decimal.ToInt32(Decimal.Divide(progressPercent, ProgressList.Keys.Count))}% completion!");
                });

                //Handle Duplicates
                if (TaskList.Keys.Contains(song.Hash)) continue;

                TaskList.Add(song.Hash, new Task<string>(() => GetSong(song.Hash, song.Name, prog).Result));
                ProgressList.Add(song.Hash, 0);
                if (token.IsCancellationRequested) break;
            }

            //Rate limit prevention
            foreach (var task in TaskList.Values)
            {
                task.Start();
                Task.Delay(50).Wait();
                if (token.IsCancellationRequested) break;
            }

            //Wait for all tasks to finish
            try
            {
                Task.WaitAll(TaskList.Values.ToArray(), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            //While this *should* already be reported, there were a few cases where for some reason it didnt, so this is as a precaution
            if (progress != null) progress.Report(100);

            //Add the results to the array sorted by the order in the playlist
            foreach (var song in songs)
                song.SongDataPath = TaskList[song.Hash].Result;

            if (progress != null) progress.Report(100);
        }

        /// <summary>
        /// Downloads specified Song and returns its path
        /// </summary>
        /// <param name="songHash">Hash of the song to be downloaded</param>
        /// <param name="mapName">Name of the song to be downloaded (for folder naming and logging purposes)</param>
        /// <param name="prog">IProgress interface to report on, if specified</param>
        /// <returns>String version of the song folder location</returns>
        public async Task<string> GetSong(string songHash, string mapName, IProgress<int> prog = null)
        {
            char[] illegalCharacters = { '>', '<', ':', '/', '\\', '\"', '|', '?', '*', ' ' };

            if (songHash == string.Empty)
            {
                prog.Report(100);
                return null;
            }

            //Make map name legal for a folder name
            string legalizedMapName = mapName;
            foreach (char illegalChar in illegalCharacters)
                legalizedMapName = legalizedMapName.Replace(illegalChar, '_');

            string url = $"{BeatsaverCDN}/{songHash}.zip";
            string songDir = $"{EnvironmentPath}\\SongFiles\\{legalizedMapName}";
            string zipPath = $"{EnvironmentTemp}\\{songHash}.zip";

            Logger.Debug($"Downloading {mapName} with {songHash} hash from {url}");

            using var client = new WebClient();
            client.Headers.Add("user-agent", SharedConstructs.Name);

            try
            {
                //Don't download if we already have it
                if (Directory.GetDirectories($"{EnvironmentPath}\\SongFiles").All(directory => directory != songDir))
                {
                    //Create DownloadedSongs if it doesn't exist
                    if (!Directory.Exists(songDir)) Directory.CreateDirectory(songDir);
                    if (!Directory.Exists(EnvironmentTemp)) Directory.CreateDirectory(EnvironmentTemp);

                    client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                    {
                        if (prog != null) prog.Report(e.ProgressPercentage);
                    };

                    //Download zip
                    await client.DownloadFileTaskAsync(new Uri(url), zipPath);

                    //Unzip to folder
                    using (ZipArchive zip = ZipFile.OpenRead(zipPath))
                        zip?.ExtractToDirectory(songDir);

                    //Clean up zip
                    File.Delete(zipPath);

                    //Return path
                    return songDir;
                }
                else
                {
                    Logger.Success("Song already downloaded! Skipping download!");
                    return songDir;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Error downloading {mapName} with filename {songHash}.zip: {e}");
                return "";
            }
        }
    }
}
