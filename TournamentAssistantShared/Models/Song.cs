using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using TournamentAssistantShared.BeatSaver;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.SimpleJSON;
using TournamentAssistantUI.Shared.Models;
using static TournamentAssistantShared.BeatSaverDownloader;
using static TournamentAssistantShared.GlobalConstants;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantShared
{
    public class Song
    {
        public string Name { get; set; }
        public string ID { get; set; }
        public string Hash { get; set; }
        public string CoverPath { get; set; }
        public string SongDataPath { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Mapper { get; set; }
        public string BPM { get; set; }
        public bool Played { get; set; }
        public TimeSpan Duration { get; set; }
        public string DurationString { get; set; }
        public Dictionary<string, SongCharacteristic> Characteristics { get; set; }
        public SongCharacteristic SelectedCharacteristic { get; set; }

        //Legacy models start
        public Beatmap BeatmapObject { get; set; }
        public DownloadedSong DownloadedSongObject { get; set; }
        public PreviewBeatmapLevel PreviewBeatmapLevelObject { get; set; }
        public Characteristic SelectedCharacteristicObject { get; set; }
        public BeatmapDifficulty SelectedBeatmapDifficulty { get; set; }

        public Song()
        {
            Characteristics = new Dictionary<string, SongCharacteristic>();
        }


        public void SetLegacyData()
        {
            if (SongDataPath == null) return;

            DownloadedSongObject = new DownloadedSong(Hash, $"{SongDataPath}\\info.dat");
            var mapFormattedLevelHash = $"custom_level_{Hash.ToUpper()}";

            var previewBeatmapLevelObject = new PreviewBeatmapLevel()
            {
                LevelId = mapFormattedLevelHash,
                Name = Name
            };

            List<Characteristic> characteristics = new List<Characteristic>();
            foreach (var characteristic in Characteristics.Values)
            {
                var newchar = new Characteristic()
                {
                    SerializedName = characteristic.Name,
                };

                List<BeatmapDifficulty> diffList = new List<BeatmapDifficulty>();
                foreach (var difficulty in characteristic.Difficulties)
                {
                    diffList.Add((BeatmapDifficulty)Enum.Parse(typeof(BeatmapDifficulty), difficulty.Name));
                }
                newchar.Difficulties = diffList.ToArray();
                characteristics.Add(newchar);
            }


            previewBeatmapLevelObject.Characteristics = characteristics.ToArray();
            PreviewBeatmapLevelObject = previewBeatmapLevelObject;
        }


        public void ParseInfoData()
        {
            

            var data = JSON.Parse(File.ReadAllText($"{SongDataPath}\\info.dat"));
        }

        public static Characteristic GetSelectedCharacteristic(SongCharacteristic songCharacteristic)
        {
            var selectedCharacteristic = new Characteristic()
            {
                SerializedName = songCharacteristic.Name,
            };

            List<BeatmapDifficulty> diffList = new List<BeatmapDifficulty>();
            foreach (var difficulty in songCharacteristic.Difficulties)
            {
                diffList.Add((BeatmapDifficulty)Enum.Parse(typeof(BeatmapDifficulty), difficulty.Name));
            }
            selectedCharacteristic.Difficulties = diffList.ToArray();
            return selectedCharacteristic;
        }


        /// <summary>
        /// Creates a new Song object and parses BeatSaver API info. Can report progress.
        /// </summary>
        /// <param name="id">Song ID to get song info</param>
        /// <param name="progress">IProgress interface to report progress on</param>
        /// <returns>New Song object</returns>
        public static async Task<Song> GetSongByIDAsync(string id, IProgress<int> progress = null)
        {
            var response = await GetSongInfoIDAsync(id);

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    break;
                case HttpStatusCode.NotFound:
                    Logger.Error($"Song with ID {id} could not be found on BeatSaver.");
                    MessageBox.Show($"Song with ID {id} could not be found on BeatSaver.", SharedConstructs.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    Logger.Error($"BeatSaver responded {response.StatusCode}\nReason: {response.ReasonPhrase}");
                    MessageBox.Show($"Something went wrong when requesting to BeatSaver.\nSong request id: {id}\nServer response code: {response.StatusCode}\nServer response:\n{response.ReasonPhrase}", SharedConstructs.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
            }

            var data = JSON.Parse(await response.Content.ReadAsStringAsync());

            var song = await ParseDataAsync(data, progress);
            return song;
        }



        /// <summary>
        /// Creates a new Song object and parses BeatSaver API info. Can report progress.
        /// </summary>
        /// <param name="hash">Song hash to get song info</param>
        /// <param name="progress">IProgress interface to report progress on</param>
        /// <returns>New Song object</returns>
        public static async Task<Song> GetSongByHashAsync(string hash, IProgress<int> progress = null)
        {
            var response = await GetSongInfoHashAsync(hash);

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    break;
                case HttpStatusCode.NotFound:
                    Logger.Error($"Song with hash {hash} could not be found on BeatSaver.");
                    MessageBox.Show($"Song with hash {hash} could not be found on BeatSaver.", SharedConstructs.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    Logger.Error($"BeatSaver responded {response.StatusCode}\nReason: {response.ReasonPhrase}");
                    MessageBox.Show($"Something went wrong when requesting to BeatSaver.\nSong request hash: {hash}\nServer response code: {response.StatusCode}\nServer response:\n{response.ReasonPhrase}", SharedConstructs.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
            }

            var data = JSON.Parse(await response.Content.ReadAsStringAsync());

            var song = await ParseDataAsync(data, progress);
            return song;
        }

        private static async Task<Song> ParseDataAsync(JSONNode data, IProgress<int> progress = null)
        {
            Song song = new()
            {
                ID = data["id"].ToString().Trim(TrimJSON),
                Name = data["name"].ToString().Trim(TrimJSON),
                Description = data["description"].ToString().Trim(TrimJSON),
                Author = data["metadata"]["songAuthorName"].ToString().Trim(TrimJSON),
                Mapper = data["metadata"]["levelAuthorName"].ToString().Trim(TrimJSON),
                BPM = data["metadata"]["bpm"].ToString().Trim(TrimJSON),
                Duration = TimeSpan.FromSeconds(Int32.Parse(data["metadata"]["duration"].ToString().Trim(TrimJSON)))
            };


            string durationString = "";
            if (song.Duration.Hours > 0)
                durationString += song.Duration.Hours.ToString();

            durationString += $"{song.Duration:mm\\:ss}";

            song.DurationString = durationString;


            //Technically should handle the possibility of multiple versions, but fuck it, lets assume that there is always only a single version for now
            //This is subject to revisit in the future
            foreach (var version in data["versions"].AsArray)
            {
                foreach (var diff in version.Value["diffs"].AsArray)
                {

                    if (song.Characteristics.Values.Count == 0)
                    {
                        song.Characteristics.Add(diff.Value["characteristic"].ToString().Trim(TrimJSON), new SongCharacteristic(diff.Value["characteristic"].ToString().Trim(TrimJSON)));
                    }

                    try
                    {
                        var characteristic = song.Characteristics[diff.Value["characteristic"].ToString().Trim(TrimJSON)];
                    }
                    catch (KeyNotFoundException)
                    {
                        song.Characteristics.Add(diff.Value["characteristic"].ToString().Trim(TrimJSON), new SongCharacteristic(diff.Value["characteristic"].ToString().Trim(TrimJSON)));
                    }

                    SongDifficulty Difficulty = new()
                    {
                        Characteristic = diff.Value["characteristic"].ToString().Trim(TrimJSON),
                        NJS = diff.Value["njs"].ToString().Trim(TrimJSON),
                        Notes = diff.Value["notes"].AsInt,
                        Obstacles = diff.Value["obstacles"].AsInt,
                        Bombs = diff.Value["bombs"].AsInt,
                        NPS = diff.Value["nps"].ToString().Trim(TrimJSON),
                        Name = diff.Value["difficulty"].ToString().Trim(TrimJSON),
                        Type = diff.Value["difficulty"].ToString().Trim(TrimJSON)
                    };
                    song.Characteristics[diff.Value["characteristic"].ToString().Trim(TrimJSON)].Difficulties.Add(Difficulty);
                }


                song.SongDataPath = TryGetSongDataPath(song.Name);
                //Default to standard, but if not found try the first one in the list
                try
                {
                    song.SelectedCharacteristic = song.Characteristics["Standard"];
                }
                catch (KeyNotFoundException)
                {
                    song.SelectedCharacteristic = song.Characteristics.Values.First();
                }
                song.SelectedCharacteristic.SelectedDifficulty = song.SelectedCharacteristic.Difficulties.Last();
                var handleProgressReport = new Progress<int>(percent => { if (progress != null) progress.Report(percent); });
                song.Hash = version.Value["hash"].ToString().Trim(TrimJSON);


                if (!File.Exists($"{AppDataCache}{song.Hash}\\cover.jpg"))
                {
                    await GetCoverAsync(version.Value["hash"].ToString().Trim(TrimJSON), handleProgressReport);
                }
                song.CoverPath = $"{AppDataCache}{song.Hash}\\cover.jpg";
            }
            progress.Report(100);

            if (song.SongDataPath != null)
            {
                song.SetLegacyData();
            }

            return song;
        }
    }
}
