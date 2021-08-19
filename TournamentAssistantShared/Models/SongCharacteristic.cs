using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using TournamentAssistantUI.Shared.Models;

namespace TournamentAssistantShared.Models
{
    public class SongCharacteristic
    {
        public string Name { get; set; }
        public ObservableCollection<SongDifficulty> Difficulties { get; set; }
        public SongDifficulty SelectedDifficulty { get; set; }

        public SongCharacteristic()
        {
            Difficulties = new ObservableCollection<SongDifficulty>();
        }

        public SongCharacteristic(string name)
        {
            Name = name;
            Difficulties = new ObservableCollection<SongDifficulty>();
        }
    }
}
