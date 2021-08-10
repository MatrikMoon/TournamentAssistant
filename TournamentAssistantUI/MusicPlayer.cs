using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Media;
using LibVLCSharp.Shared;
using TournamentAssistantShared;

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
            player = new MediaPlayer(VLC);
        }

        /// <summary>
        /// Initializes a media file
        /// </summary>
        /// <param name="path">File path to initialize</param>
        /// <returns>Media representation of the file at the provided path</returns>
        public Media MediaInit(string path)
        {
            var media = new Media(VLC, path);
            media.Parse();
            return media;
        }
    }
}
