using System;
using TournamentAssistantShared.Models;

namespace TournamentAssistantShared
{
    [Serializable]
    class Playlist
    {
        public string Name { get; private set; }
        public string Author { get; private set; }
        public string Description { get; private set; }
        public string Image { get; private set; }
        public Beatmap[] Songs { get; set; }

        public Playlist(string name, string author, string description, string image)
        {
            Name = name;
            Author = author;
            Description = description;
            Image = image;
        }
    }
}