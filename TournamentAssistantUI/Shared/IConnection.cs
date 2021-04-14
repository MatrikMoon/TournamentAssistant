using System;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

/**
 * Created by Moon on 8/16/2019
 * This abstracts out some of the sending functionality of my client/server
 * setup so that the MainPage doesn't have to worry about which one it's
 * currently acting as
 */

namespace TournamentAssistantShared
{
    public interface IConnection
    {
        State State { get; set; }

        //Unfortunately I am not smart enough to have the changes in State propegate down to the MatchPages elements without assistance
        event Action<Player> PlayerConnected;
        event Action<Player> PlayerDisconnected;
        event Action<Player> PlayerInfoUpdated;
        event Action<SongFinished> PlayerFinishedSong;
        event Action<Match> MatchInfoUpdated;
        event Action<Match> MatchCreated;
        event Action<Match> MatchDeleted;
        event Action<Acknowledgement, Guid> AckReceived; //<Ack object, From>

        User Self { get; set; }
        void AddPlayer(Player player);
        void UpdatePlayer(Player player);
        void RemovePlayer(Player player);
        void AddCoordinator(Coordinator coordinator);
        void RemoveCoordinator(Coordinator coordinator);
        void CreateMatch(Match match);
        void UpdateMatch(Match match);
        void DeleteMatch(Match match);
        void Send(Guid id, Packet packet);
        void Send(Guid[] ids, Packet packet);
    }
}
