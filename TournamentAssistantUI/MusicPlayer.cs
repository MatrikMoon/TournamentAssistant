using LibVLCSharp.Shared;
using System;
using System.Threading.Tasks;
using TournamentAssistantShared.BeatSaver;

namespace TournamentAssistantUI
{
    class MusicPlayer
    {
        public LibVLC VLC;
        public MediaPlayer player;
        public MusicPlayer()
        {
            Core.Initialize();
            VLC = new LibVLC();
            player = new MediaPlayer(VLC)
            {
                EnableHardwareDecoding = true,
                Volume = 20
            };
            player.EndReached += Player_EndReached;
            player.Playing += Player_Playing;
        }

        private void Player_Playing(object sender, EventArgs e)
        {
            player.Volume = 20;
        }

        //LibVLC thread loop bug workaround
        private void Player_EndReached(object sender, EventArgs e)
        {
            Task.Run(() => player.Stop());
        }

        /// <summary>
        /// Initializes a media file
        /// </summary>
        /// <param name="path">File path to initialize</param>
        /// <returns>Media representation of the file at the provided path</returns>
        public Media MediaInit(string path)
        {
            var media = new Media(VLC, path);
            media.Parse().Wait(); //While waiting on the main thread is possible with this, it *shouldnt* take enough time to stop the execution of other code for too long
                                  //Moon's note: whatever you say bossman xD I'll leave this one alone
                                  //Moon's update: it does. It definitely does.
                                  //TODO: This is the one causing it to take 10 seconds to load the BR page
            return media;
        }

        public void LoadSong(PlaylistItem song)
        {
            if (song.DownloadedSong != null)
            {
                player.Media = MediaInit(song.DownloadedSong.GetAudioPath());
            }
        }
    }
}
