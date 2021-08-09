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
        public Media MediaInit(string path)
        {
            var media = new Media(VLC, path);
            media.Parse();
            return media;
        }
    }
}
