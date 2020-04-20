using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using TournamentAssistantShared;

namespace TournamentAssistantUI.BeatSaver
{
    class BeatSaverDownloader
    {
        private static string beatSaverUrl = "https://beatsaver.com";
        private static string beatSaverSongPageUrl = $"{beatSaverUrl}/beatmap/";
        private static string beatSaverDownloadByHashUrl = $"{beatSaverUrl}/api/download/hash/";
        private static string beatSaverDownloadByKeyUrl = $"{beatSaverUrl}/api/download/key/";

        public static void DownloadSong(string hash, Action<string> whenFinished, Action<int> progressChanged = null)
        {
            Logger.Debug($"Downloading {hash} from {beatSaverUrl}");

            //Create DownloadedSongs if it doesn't exist
            Directory.CreateDirectory(Song.songDirectory);

            //Get the hash of the indicated song
            string zipPath = $"{Song.songDirectory}{hash}.zip";

            Action whenDownloadComplete = () =>
            {
                var idFolder = $"{Song.songDirectory}{hash}";
                var songFolder = Directory.GetDirectories(idFolder); //Assuming each id folder has only one song folder
                var subFolder = songFolder.FirstOrDefault() ?? idFolder;
                Logger.Success($"Downloaded (or have previously downloaded) {subFolder}!");

                whenFinished?.Invoke($@"{Song.songDirectory}{hash}\");
            };

            //Download zip
            using (var client = new WebClient())
            {
                client.Headers.Add("user-agent", SharedConstructs.Name);

                try
                {
                    //Don't download if we already have it
                    if (Directory.GetDirectories(Song.songDirectory).All(o => o != $"{Song.songDirectory}{hash}"))
                    {
                        client.DownloadFileCompleted += new AsyncCompletedEventHandler(
                            (object sender, AsyncCompletedEventArgs e) =>
                            {
                                //Unzip to folder
                                using (ZipArchive zip = ZipFile.OpenRead(zipPath))
                                {
                                    zip?.ExtractToDirectory($@"{Song.songDirectory}{hash}\");
                                }

                                //Clean up zip
                                File.Delete(zipPath);

                                whenDownloadComplete.Invoke();
                            }
                        );
                        client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(
                            (object sender, DownloadProgressChangedEventArgs e) =>
                            {
                                progressChanged?.Invoke(e.ProgressPercentage);
                            }
                        );
                        client.DownloadFileAsync(new Uri($"{beatSaverDownloadByHashUrl}{hash}"), zipPath);
                    }
                    else
                    {
                        Logger.Success("Song already downloaded! Skipping download!");
                        whenDownloadComplete.Invoke();
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"Error downloading {hash}.zip: {e}");
                }
            }            
        }

        public static void DownloadSongInfoThreaded(string hash, Action<bool> whenFinished, Action<int> progressChanged = null)
        {
            new Thread(() =>
            {
                DownloadSong(hash, (songDir) =>
                {
                    whenFinished?.Invoke(songDir != null);
                }, progressChanged);
            })
            .Start();
        }

        public static string GetHashFromID(string id)
        {
            if (OstHelper.IsOst(id)) return id;

            Logger.Debug($"Getting hash for {id} from {beatSaverUrl}");


            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = false;

            using (var client = new HttpClient(httpClientHandler))
            {
                client.DefaultRequestHeaders.Add("user-agent", SharedConstructs.Name);

                var response = client.GetAsync($"{beatSaverDownloadByKeyUrl}{id}");
                response.Wait();

                var result = response.Result.Headers.Location.ToString();
                var startIndex = result.LastIndexOf("/") + 1;
                var length = result.LastIndexOf(".") - startIndex;

                return result.Substring(startIndex, length);
            }
        }
    }
}
