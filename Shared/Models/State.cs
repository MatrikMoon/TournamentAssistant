using System;
using BattleSaberShared.Models;

/**
 * Represents the current state of the tournament. This is intended
 * to be sent to newly-connected match coordinators, AND *NOT* used
 * as a way to propegate state changes to currently connected cordinators
 */

namespace BattleSaberShared.Models
{
    [Serializable]
    public class State
    {
        public ServerSettings ServerSettings { get; set; }
        public Player[] Players { get; set; }
        public MatchCoordinator[] Coordinators { get; set; }
        public Match[] Matches { get; set; }
    }
}
