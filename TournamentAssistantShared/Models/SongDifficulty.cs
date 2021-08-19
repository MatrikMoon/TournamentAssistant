using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TournamentAssistantUI.Shared.Models
{
    public class SongDifficulty
    {
        public string Characteristic { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string NJS { get; set; }
        public int Notes { get; set; }
        public int Bombs { get; set; }
        public int Obstacles { get; set; }
        public string NPS { get; set; }
    }
}
