using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TournamentAssistantUI.Models
{
    class UsernamePasswordModel
    {
        public string Username { get; set; }
        public string? Password { get; set; }

        public UsernamePasswordModel()
        {
            Username = "User";
            Password = "";
        }
    }
}
