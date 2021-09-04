using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TournamentAssistantShared.Extensions;
using TournamentAssistantShared.SimpleJSON;
using static TournamentAssistantShared.GlobalConstants;
using static TournamentAssistantShared.Song;

namespace TournamentAssistantShared
{
    public class PlaylistHandler
    {
        public event Action<Playlist> PlaylistLoadComplete;
        public event Action<Playlist> SongAddComplete;
        IProgress<int> IProgress { get; }
        Dictionary<string, Task<Song>> TaskList { get; set; }
        Dictionary<string, int> ProgressList { get; set; }

        public PlaylistHandler(IProgress<int> progress = default)
        {
            IProgress = progress;
            TaskList = new();
            ProgressList = new();
        }

        public async Task LoadPlaylist(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"Selected path {filePath} is invalid or inaccesible", SharedConstructs.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            JSONNode JsonData = JSON.Parse(File.ReadAllText(filePath));

            Playlist playlist = new(
                JsonData["playlistTitle"].ToString().Trim(TrimJSON), 
                JsonData["playlistAuthor"].ToString().Trim(TrimJSON), 
                JsonData["playlistDescription"].ToString().Trim(TrimJSON), 
                JsonData["image"].ToString().Trim(TrimJSON));


            foreach (var song in JsonData["songs"].AsArray.Linq.DistinctBy(x => x.Value["hash"]))
            {
                string hash = song.Value["hash"].ToString().Trim(TrimJSON);

                TaskList[hash] = new Task<Song>(() => GetSongByHashAsync(hash, new Progress<int>(percent => ReportProgress(percent, hash))).Result);
                ProgressList.Add(hash, 0);
            }

            foreach (var task in TaskList.Values)
            {
                task.Start();
                await Task.Delay(BeatsaverRateLimit);
            }

            //Wait for all tasks to finish
            await Task.WhenAll(TaskList.Values.ToArray());

            //Add the results to the array sorted by the order in the playlist
            foreach (var playlistEntry in JsonData["songs"].AsArray)
            {
                var hash = playlistEntry.Value["hash"].ToString().Trim(TrimJSON);
                var song = TaskList[hash].Result;
                if (song != null) playlist.Songs.Add(song);
            }

            //Set default selected song
            playlist.SelectedSong = playlist.Songs[0];

            PlaylistLoadComplete?.Invoke(playlist);

            //Cleanup
            TaskList.Clear();
            ProgressList.Clear();
        }

        /// <summary>
        /// Gets song info by ID and adds it to the specified playlist. On completion invokes SongAddComplete event with new Playlist object;
        /// </summary>
        /// <param name="id">Song Beatsaver ID</param>
        /// <param name="playlist">Playlist to be added to</param>
        public async void AddSongByIDAsync(string id, Playlist playlist)
        {
            var song = await GetSongByIDAsync(id, IProgress);
            playlist.Songs.Add(song);
            SongAddComplete?.Invoke(playlist);
        }

        private void ReportProgress(int percent, string processSongHash)
        {
            int addedProgress = 0;
            lock (ProgressList)
            {
                ProgressList[processSongHash] = percent;
                foreach (int item in ProgressList.Values)
                    addedProgress += item;
            }

            int totalProgress = decimal.ToInt32(decimal.Divide(addedProgress, ProgressList.Keys.Count));

            IProgress.Report(totalProgress);
            Logger.Debug($"Reported {totalProgress}% completion!");
        }
    }
}
