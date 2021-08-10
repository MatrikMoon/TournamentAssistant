using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TournamentAssistantUI.Shared.Models;

namespace TournamentAssistantShared
{
    public class Song
    {
        public string Name { get; set; }
        public string ID { get; set; }
        public string Hash { get; set; }
        public string CoverPath { get; set; }
        public string SongDataPath { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Mapper { get; set; }
        public string BPM { get; set; }
        public bool Played { get; set; }
        public TimeSpan Duration { get; set; }
        public string DurationString { get; set; }
        public List<SongDifficulty> Difficulty { get; set; }
        public SongDifficulty SelectedDifficulty { get; set; }

        public Song(string name = null, string id = null, string hash = null, string coverPath = null, string description = null, string author = null, string mapper = null, string bpm = null, SongDifficulty selectedDifficulty = null)
        {
            Name = name;
            ID = id;
            Hash = hash;
            CoverPath = coverPath;
            Description = description;
            Author = author;
            Mapper = mapper;
            BPM = bpm;
            SelectedDifficulty = selectedDifficulty;
            Played = false;
            Difficulty = new List<SongDifficulty>();
        }

        /// <summary>
        /// Derives the duration as string. Only use within the data context of an instance of Song
        /// </summary>
        public void DeriveDurationString()
        {
            char[] trim = {'0', ':' };
            DurationString = ($"{Duration:hh\\:mm\\:ss}").TrimStart(trim);
        }
    }
}
