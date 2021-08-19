using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TournamentAssistantShared
{
    [Serializable]
    public class Playlist
    {
        public bool IsLoaded { get; set; } = false;
        public string Name { get; private set; }
        public string Author { get; private set; }
        public string Description { get; private set; }
        public string Image { get; private set; }
        public ObservableCollection<Song> Songs { get; set; }
        public Song SelectedSong { get; set; }

        public Playlist(string name, string author, string description = null, string image = null)
        {
            Name = name;
            Author = author;
            Description = description;
            Image = image;
            Songs = new ObservableCollection<Song>();
        }
    }
}