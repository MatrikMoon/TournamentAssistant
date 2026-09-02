using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using TournamentAssistantServer.ASP.Attributes;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantServer.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Utilities;
using ScoreModel = TournamentAssistantServer.Database.Models.Score;

namespace TournamentAssistantServer.PacketHandlers
{
    [ApiController]
    [Route("tournaments/{tournamentGuid}/qualifiers")]
    [AllowWebsocketToken]
    [AllowPlayerToken]
    public sealed class QualifierController : ControllerBase
    {
        public DatabaseService DatabaseService { get; set; }
        public StateManager StateManager { get; set; }

        public sealed class MapWithScores
        {
            public Map Map { get; set; }
            public List<LeaderboardEntry> Scores { get; set; } = new List<LeaderboardEntry>();
        }

        public sealed class QualifierWithScores
        {
            public QualifierEvent Qualifier { get; set; }
            public List<MapWithScores> Maps { get; set; } = new List<MapWithScores>();
        }

        [HttpGet("{qualifierGuid}/all")]
        [AllowFromPlayer]
        [AllowFromWebsocket]
        public ActionResult<QualifierWithScores> GetAll(
            string tournamentGuid,
            string qualifierGuid,
            [FromUser] User user)
        {
            var qualifier = StateManager.GetQualifier(tournamentGuid, qualifierGuid);
            if (qualifier == null)
                return NotFound();

            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            var accountIds = new[] { user?.discord_info?.UserId, user?.PlatformId }
                .Where(x => !string.IsNullOrWhiteSpace(x));
            var mockAllowed = user?.IsMock == true &&
                StateManager.GetTournament(tournamentGuid)?.Settings.AllowMockClients == true;
            if (!mockAllowed && !accountIds.Any(x => tournamentDatabase.IsUserAuthorized(
                    tournamentGuid, x, Permissions.GetQualifierScores)))
                return Forbid();

            var canSeeHidden = accountIds.Any(x => tournamentDatabase.IsUserAuthorized(
                tournamentGuid, x, Permissions.SeeHiddenQualifierScores));
            var hideScores = qualifier.Flags.HasFlag(QualifierEvent.EventSettings.HideScoresFromPlayers)
                && !canSeeHidden;

            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();
            IQueryable<ScoreModel> scoreQuery = qualifierDatabase.Scores;
            var result = new QualifierWithScores { Qualifier = qualifier };
            foreach (var map in qualifier.QualifierMaps)
            {
                var mapResult = new MapWithScores { Map = map };
                if (!hideScores)
                {
                    mapResult.Scores.AddRange(scoreQuery
                        .Where(x => x.EventId == qualifierGuid && x.MapId == map.Guid && !x.IsPlaceholder && !x.Old)
                        .OrderByQualifierSettings(qualifier.Sort, map.GameplayParameters.Target)
                        .Select(x => new LeaderboardEntry
                        {
                            EventId = x.EventId,
                            MapId = x.MapId,
                            PlatformId = x.PlatformId,
                            Username = x.Username,
                            MultipliedScore = x.MultipliedScore,
                            ModifiedScore = x.ModifiedScore,
                            MaxPossibleScore = x.MaxPossibleScore,
                            Accuracy = x.Accuracy,
                            NotesMissed = x.NotesMissed,
                            BadCuts = x.BadCuts,
                            GoodCuts = x.GoodCuts,
                            MaxCombo = x.MaxCombo,
                            FullCombo = x.FullCombo,
                            Color = "#ffffff"
                        }));
                }
                result.Maps.Add(mapResult);
            }

            return result;
        }
    }
}
