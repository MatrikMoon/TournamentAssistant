using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TournamentAssistantShared.BeatSaver
{
    public class BeatSaverDownloader
    {
        private static string beatSaverUrl = "https://beatsaver.com";
        private static string beatSaverCdnUrl = "https://cdn.beatsaver.com";
        private static string beatSaverDownloadByHashUrl = $"{beatSaverCdnUrl}/";
        private static string beatSaverDownloadByKeyUrl = $"{beatSaverUrl}/api/download/key/";
        private static string beatSaverGetSongInfoUrl = $"{beatSaverUrl}/api/maps/id/";

        public static void DownloadSong(string hash, Action<string> whenFinished, Action<int> progressChanged = null, string customDownloadUrl = null)
        {
            Logger.Debug($"Downloading {hash} from {customDownloadUrl ?? beatSaverDownloadByHashUrl}");

            //Create DownloadedSongs if it doesn't exist
            Directory.CreateDirectory(DownloadedSong.songDirectory);

            //Get the hash of the indicated song
            string zipPath = $"{DownloadedSong.songDirectory}{hash}.zip";

            Action whenDownloadComplete = () =>
            {
                var idFolder = $"{DownloadedSong.songDirectory}{hash}";
                var songFolder = Directory.GetDirectories(idFolder); //Assuming each id folder has only one song folder
                var subFolder = songFolder.FirstOrDefault() ?? idFolder;
                Logger.Success($"Downloaded (or have previously downloaded) {subFolder}!");

                whenFinished?.Invoke($@"{DownloadedSong.songDirectory}{hash}\");
            };

            //Download zip
            using (var client = new WebClient())
            {
                client.Headers.Add("user-agent", SharedConstructs.Name);

                try
                {
                    //Don't download if we already have it
                    if (Directory.GetDirectories(DownloadedSong.songDirectory).All(o => o != $"{DownloadedSong.songDirectory}{hash}"))
                    {
                        client.DownloadFileCompleted += new AsyncCompletedEventHandler(
                            (object sender, AsyncCompletedEventArgs e) =>
                            {
                                //Unzip to folder
                                using (ZipArchive zip = ZipFile.OpenRead(zipPath))
                                {
                                    zip?.ExtractToDirectory($@"{DownloadedSong.songDirectory}{hash}\");
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
                        client.DownloadFileAsync(new Uri($"{customDownloadUrl ?? beatSaverDownloadByHashUrl}{$"{hash}.zip"}"), zipPath);
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

        public static void DownloadSongThreaded(string hash, Action<bool> whenFinished, Action<int> progressChanged = null, string customDownloadUrl = null)
        {
            new Thread(() =>
            {
                DownloadSong(hash, (songDir) =>
                {
                    whenFinished?.Invoke(songDir != null);
                }, progressChanged, customDownloadUrl);
            })
            .Start();
        }

        public static string GetHashFromID(string id)
        {
            if (OstHelper.IsOst(id)) return id;

            id = id.ToLower();
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

        public static async Task<SongInfo> GetSongInfo(string id)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", SharedConstructs.Name);

                var response = await client.GetStringAsync($"{beatSaverGetSongInfoUrl}{id}");
                return JsonConvert.DeserializeObject<SongInfo>(response);
            }
        }
    }
}
