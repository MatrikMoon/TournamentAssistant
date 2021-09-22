using System.Collections.ObjectModel;
using TournamentAssistantShared.BeatSaver;

namespace TournamentAssistantShared
{
    public class Playlist
    {
        public bool IsLoaded { get; set; } = false;
        public string Name { get; private set; }
        public string Author { get; private set; }
        public string Description { get; private set; }
        public string Image { get; private set; }
        public ObservableCollection<PlaylistItem> Songs { get; set; } = new ObservableCollection<PlaylistItem>();
        public PlaylistItem SelectedSong { get; set; }

        public Playlist(string name, string author, string description = null, string image = null)
        {
            Name = name;
            Author = author;
            Description = description;
            Image = image;
        }
    }
}