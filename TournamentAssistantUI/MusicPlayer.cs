using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Media;
using LibVLCSharp.Shared;
using TournamentAssistantShared;
using System.Threading;
using System.IO;
using TournamentAssistantUI.UI;

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
                EnableHardwareDecoding = true
            };
            player.EndReached += Player_EndReached;
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
            return media;
        }

        public void LoadSong(Song song)
        {
            if (song.SongDataPath != null)
                player.Media = MediaInit(Directory.GetFiles(song.SongDataPath, "*.egg")[0]); //We can assume (no shit) that there is only a single .egg file
        }
    }
}
