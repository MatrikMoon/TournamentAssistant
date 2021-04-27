using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;

namespace TournamentAssistantUI.ViewModels
{
    public class ScrapedServersModel : ViewModelBase
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public bool IsPasswordProtected { get; set; }
        public CoreServer ServerObjectReference { get; set; }
        public int ServerVersion { get; set; } //Prep work for cross-versioning
        public bool ConnectionPossible { get; set; } //Prep work for cross-versioning
    }
}
