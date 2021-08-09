using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.SimpleJSON;

namespace TournamentAssistantUI.Shared
{
    class PlaylistHandler
    {
        public Playlist Playlist { get; private set; }
        public PlaylistHandler(string filepath)
        {
            if (!File.Exists(filepath))
            {
                //do something to tell the user invalid path, on the backburner for now
                return;
            }
            JSONNode JsonData = JSON.Parse(File.ReadAllText(filepath));

            Playlist = new(JsonData["playlistTitle"].ToString(), JsonData["playlistAuthor"].ToString(), JsonData["playlistDescription"].ToString(), JsonData["image"].ToString());

            
        }
    }
}
