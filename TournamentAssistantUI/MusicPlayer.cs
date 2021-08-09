using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Media;
using WMPLib;

namespace TournamentAssistantUI
{
    class MusicPlayer
    {
        
        public WindowsMediaPlayer player;
        public MusicPlayer()
        {
            player = new();
            player.settings.volume = 40;
        }

        public void PlayFile(string path)
        {
            player.URL = path;
            player.controls.play();
        }
        public void StopPlayback()
        {
            player.controls.stop();
        }
    }
}
