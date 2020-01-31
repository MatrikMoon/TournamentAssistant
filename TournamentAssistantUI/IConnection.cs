using System;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

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

        //Unfortunately I am not smart enough to have the changes in State propegate down to the MatchPages elements without assistance
        event Action<Player> PlayerConnected;
        event Action<Player> PlayerDisconnected;
        event Action<Player> PlayerInfoUpdated;
        event Action<Player> PlayerFinishedSong;
        event Action<Match> MatchInfoUpdated;
        event Action<Match> MatchCreated;
        event Action<Match> MatchDeleted;

        MatchCoordinator Self { get; set; }
        void AddPlayer(Player player);
        void UpdatePlayer(Player player);
        void RemovePlayer(Player player);
        void AddCoordinator(MatchCoordinator coordinator);
        void RemoveCoordinator(MatchCoordinator coordinator);
        void CreateMatch(Match match);
        void UpdateMatch(Match match);
        void DeleteMatch(Match match);
        void Send(string guid, Packet packet);
        void Send(string[] guids, Packet packet);
    }
}
