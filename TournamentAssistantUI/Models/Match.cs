using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;

namespace TournamentAssistantUI.Models
{
    [Serializable]

    public class Match
    {
        public string Guid { get; set; }
        public Player[] Players { get; set; }
        public MatchCoordinator Coordinator { get; set; }
    }
}
