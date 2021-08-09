using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.SimpleJSON;
using TournamentAssistantUI.Shared.Models;

namespace TournamentAssistantShared
{
    class PlaylistHandler
    {
        public const string BeatsaverCDN = "https://cdn.beatsaver.com";
        public const string BeatsaverAPI = "https://api.beatsaver.com";
        public static string EnvironmentPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\TournamentAssistantUI";
        public static string EnvironmentTemp = $"{EnvironmentPath}\\Temp";
        IProgress<int> IProgress { get; set; }
        Dictionary<string, Task<Song>> TaskList { get; set; }
        Dictionary<string, int> _progressList;
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
            } 
        }
        public Playlist Playlist { get; private set; }
        char[] trimJSON = {'\"', '\\', ' '};
        public readonly object PlaylistSongTableSync = new object();
        public PlaylistHandler(IConnection connection)
        {
            //If we are adding a song to an empty playlist
            //I think an overload constructor is the best soulution here
            Playlist = new("Temp Playlist", connection.Self.Name);
        }
        public PlaylistHandler(string filepath, IProgress<int> prog = null)
        {
            if (!File.Exists(filepath))
            {
                //do something to tell the user invalid path, on the backburner for now
                //technically should be tested before calling this constructor, but including just in case
                return;
            }
            JSONNode JsonData = JSON.Parse(File.ReadAllText(filepath));

            Playlist = new(JsonData["playlistTitle"].ToString().Trim(trimJSON), JsonData["playlistAuthor"].ToString().Trim(trimJSON), JsonData["playlistDescription"].ToString().Trim(trimJSON), JsonData["image"].ToString().Trim(trimJSON));

            IProgress = prog;
            TaskList = new();
            ProgressList = new();
            foreach (var item in JsonData["songs"].AsArray)
            {
                string hash = item.Value["hash"].ToString().Trim(trimJSON);

                //Handle Duplicates
                if (TaskList.Keys.Contains(hash)) continue;

                var progress = new Progress<int>(percent => { ReportProgress(percent, hash); });

                TaskList.Add(hash, new Task<Song>( () => SetupSongAsyncCall(item.Value, progress).Result));
                ProgressList.Add(hash, 0);
            }
            Task.Run(() => 
            {
                //Rate limit prevention
                foreach (var task in TaskList.Values)
                {
                    task.Start();
                    Task.Delay(100).Wait();
                }
                //Wait for all tasks to finish
                Task.WaitAll(TaskList.Values.ToArray());

                //While this *should* already be reported, there were a few cases where for some reason it didnt, so this is as a precaution
                IProgress.Report(100);

                //Add the results to the array sorted by the order in the playlist
                foreach (var playlistEntry in JsonData["songs"].AsArray)
                {
                    lock (PlaylistSongTableSync) 
                        Playlist.Songs.Add(TaskList[playlistEntry.Value["hash"].ToString().Trim(trimJSON)].Result);
                    Task.Delay(20).Wait();
                }
                Playlist.SelectedSong = Playlist.Songs[0];
            });
        }

        private void ReportProgress(int percent, string processSongHash)
        {
            int progress = 0;
            ProgressList[processSongHash] = percent;
            foreach (int item in ProgressList.Values)
                progress += item;
            IProgress.Report(Decimal.ToInt32(Decimal.Divide(progress, ProgressList.Keys.Count)));
            Logger.Debug($"[{this}]: Reported {Decimal.ToInt32(Decimal.Divide(progress, ProgressList.Keys.Count))}% completion!");
        }


        /// <summary>
        /// Setup a playlist song item and parse Beatsaver API data
        /// </summary>
        /// <param name="item">JSONNode with the song hash parsed from a playlist</param>
        public async Task<Song> SetupSongAsyncCall(JSONNode item, IProgress<int> prog = null)
        {
            var response = await GetSongInfoFromHashAsync(item["hash"].ToString().Trim(trimJSON));
            if (response.Contains("HttpRequestException"))
            {
                if (prog != null) prog.Report(100);
                return new Song(response.Split(':')[2].Split('.')[0]); //Split out just the http exception, and strip the rest of the callstack
            }
            JSONNode data = JSON.Parse(response);
            return await SetupSongAsync(data, prog);
        }


        /// <summary>
        /// Setup a playlist song item and parse Beatsaver API data
        /// </summary>
        /// <param name="id">String with the song beatsaver ID</param>
        public async Task<Song> SetupSongAsyncCall(string id, IProgress<int> prog = null)
        {
            var response = await GetSongInfoFromIDAsync(id);
            if (response.Contains("HttpRequestException"))
            {
                if (prog != null) prog.Report(100);
                return new Song(response);
            }
            JSONNode data = JSON.Parse(response);
            return await SetupSongAsync(data, prog);
        }

        private async Task<Song> SetupSongAsync(JSONNode data, IProgress<int> prog)
        {
            TimeSpan Duration = TimeSpan.FromSeconds(double.Parse(data["metadata"]["bpm"].ToString().Trim(trimJSON).Split('.')[0], System.Globalization.NumberStyles.AllowDecimalPoint));
            Song song = new()
            {
                ID = data["id"].ToString().Trim(trimJSON),
                Name = data["name"].ToString().Trim(trimJSON),
                Description = data["description"].ToString().Trim(trimJSON),
                Author = data["metadata"]["songAuthorName"].ToString().Trim(trimJSON),
                Mapper = data["metadata"]["levelAuthorName"].ToString().Trim(trimJSON),
                BPM = data["metadata"]["bpm"].ToString().Trim(trimJSON),
                Duration = Duration,
            };

            song.DeriveDurationString();

            //Technically should handle the possibility of multiple versions, but fuck it, lets assume that there is always only a single version
            //This is subject to revisit in the future
            foreach (var version in data["versions"].AsArray)
            {
                foreach (var diff in version.Value["diffs"].AsArray)
                {
                    SongDifficulty Difficulty = new()
                    {
                        Characteristic = diff.Value["characteristic"].ToString().Trim(trimJSON),
                        NJS = diff.Value["njs"].ToString().Trim(trimJSON),
                        Notes = diff.Value["notes"].AsInt,
                        Obstacles = diff.Value["obstacles"].AsInt,
                        Bombs = diff.Value["bombs"].AsInt,
                        NPS = diff.Value["nps"].ToString().Trim(trimJSON),
                        Name = diff.Value["difficulty"].ToString().Trim(trimJSON)
                    };
                    song.Difficulty.Add(Difficulty);
                }

                song.SelectedDifficulty = song.Difficulty[0];
                var progress = new Progress<int>(percent => { if (prog != null) prog.Report(percent); });
                song.CoverPath = await GetCoverImagePath(version.Value["hash"].ToString().Trim(trimJSON), progress);
                song.Hash = version.Value["hash"].ToString().Trim(trimJSON);
            }
            prog.Report(100);
            return song;
        }

        private async Task<string> GetSongInfoFromHashAsync(string hash)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", $"TournamentAssistant{SharedConstructs.Version}");

                var url = $"{BeatsaverAPI}/maps/hash/{hash.ToLower()}";
                string response;
                try
                {
                    response = await client.GetStringAsync(url);
                }
                catch (HttpRequestException e)
                {
                    Logger.Error(e.ToString());
                    return e.ToString();
                }

                return response;
            }
        }
        private async Task<string> GetSongInfoFromIDAsync(string id)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", $"TournamentAssistant{SharedConstructs.Version}");


                var url = $"{BeatsaverAPI}/maps/id/{id.ToLower()}";
                string response;
                try
                {
                    response = await client.GetStringAsync(url);
                }
                catch (HttpRequestException e)
                {
                    Logger.Error(e.ToString());
                    return e.ToString();
                }

                return response;
            }
        }

        private async Task<string> GetCoverImagePath(string songHash, IProgress<int> prog = null)
        {
            if (!Directory.Exists($"{EnvironmentPath}\\cache\\{songHash}")) Directory.CreateDirectory($"{EnvironmentPath}\\cache\\{songHash}");
            if (!File.Exists($"{EnvironmentPath}\\cache\\{songHash}\\{songHash}.jpg"))
            {
                using var client = new WebClient();
                client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                {
                    if (prog != null) prog.Report(e.ProgressPercentage);
                };
                string url = $"{BeatsaverCDN}/{songHash.ToLower()}.jpg";
                await client.DownloadFileTaskAsync(url, $"{EnvironmentPath}\\cache\\{songHash}\\{songHash}.jpg");
            }

            if (prog != null) prog.Report(100);
            return $"{EnvironmentPath}\\cache\\{songHash}\\{songHash}.jpg";
        }
    }
}
