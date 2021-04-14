using System;
using System.Collections.Generic;
using TournamentAssistantShared.Models.Discord;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class QualifierEvent
    {
        [Flags]
        public enum EventSettings
        {
            None = 0,
            HideScoreFromPlayers = 1,
            DisableScoresaberSubmission = 2,
            EnableLeaderboardMessage = 4
        }

        public Guid EventId { get; set; }
        public string Name { get; set; }
        public Guild Guild { get; set; }
        public Channel InfoChannel { get; set; }
        public GameplayParameters[] QualifierMaps { get; set; }
        public bool SendScoresToInfoChannel { get; set; }
        public int Flags { get; set; }

        #region Equality
        public static bool operator ==(QualifierEvent a, QualifierEvent b)
        {
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return ReferenceEquals(a, null) && ReferenceEquals(b, null);
            return a.GetHashCode() == b.GetHashCode();
        }

        public static bool operator !=(QualifierEvent a, QualifierEvent b)
        {
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return ReferenceEquals(a, null) ^ ReferenceEquals(b, null);
            return a.GetHashCode() != b.GetHashCode();
        }

        public override bool Equals(object other)
        {
            if (!(other is QualifierEvent)) return false;
            return GetHashCode() == (other as QualifierEvent).GetHashCode();
        }

        public override int GetHashCode()
        {
            var hashCode = 1119573122;
            hashCode = hashCode * -1521134295 + EqualityComparer<Guid>.Default.GetHashCode(EventId);
            return hashCode;
        }
        #endregion Equality
    }
}