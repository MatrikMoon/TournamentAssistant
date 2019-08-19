using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantUI.Models;

/**
 * Created by Moon on 8/16/2019
 * This abstracts out some of the sending functionality of my client/server
 * setup so that the MainPage doesn't have to worry about which one it's
 * currently acting as
 */

namespace TournamentAssistantUI
{
    public interface IConnection
    {
        TournamentState State { get; set; }
        MatchCoordinator Self { get; set; }
        void AddPlayer(Player player);
        void RemovePlayer(Player player);
        void AddCoordinator(MatchCoordinator coordinator);
        void RemoveCoordinator(MatchCoordinator coordinator);
        void CreateMatch(Match match);
        void DeleteMatch(Match match);
    }
}
