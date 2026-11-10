using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.Replays;
using TournamentAssistantServer.Utilities;
using TournamentAssistantShared;

namespace TournamentAssistantServer
{
    // Housekeeping that runs every hour: hiding tournaments nobody has touched in a while and removing replays past their retention
    public class Scheduler : IDisposable
    {
        private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly StateManager _stateManager;
        private readonly DatabaseService _databaseService;
        private readonly ReplayStorage _replayStorage;
        private readonly ReplayUploads _replayUploads;
        private Timer _timer;
        private int _running;

        public Scheduler(StateManager stateManager, DatabaseService databaseService, ReplayStorage replayStorage, ReplayUploads replayUploads)
        {
            _stateManager = stateManager;
            _databaseService = databaseService;
            _replayStorage = replayStorage;
            _replayUploads = replayUploads;
        }

        public void Start()
        {
            _timer = new Timer(_ => _ = RunOnce(), null, FirstRunDelay, Interval);
        }

        public async Task RunOnce()
        {
            // A slow run, for example one waiting on an unreachable database, shouldn't overlap with the next one
            if (Interlocked.Exchange(ref _running, 1) == 1) return;

            try
            {
                await HideInactiveTournaments();
                await RemoveOldReplays();
                _replayUploads.DiscardStale();
            }
            catch (Exception e)
            {
                Logger.Error($"Scheduled jobs failed: {e}");
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }

        private async Task HideInactiveTournaments()
        {
            var now = TournamentActivity.Now();
            foreach (var tournament in _stateManager.GetTournaments().Where(x => TournamentActivity.ShouldHide(x, now)).ToList())
            {
                Logger.Info($"Hiding {tournament.Settings.TournamentName}, it hasn't been updated in {TournamentActivity.HideAfter.TotalDays} days");
                tournament.Settings.AllowUnauthorizedView = false;
                await _stateManager.UpdateTournamentSettings(tournament);
            }
        }

        private async Task RemoveOldReplays()
        {
            if (!_replayStorage.IsConfigured) return;

            var expired = await _replayStorage.DeleteExpired(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            // Replays are only stored for tournaments, so one whose tournament is gone has no record left to belong to
            string[] tournamentIds;
            using (var tournamentDatabase = _databaseService.NewTournamentDatabaseContext())
            {
                tournamentIds = tournamentDatabase.Tournaments.Where(x => !x.Old).Select(x => x.Guid).ToArray();
            }
            var orphaned = await _replayStorage.DeleteOrphans(tournamentIds);

            if (expired > 0 || orphaned > 0)
            {
                Logger.Info($"Removed {expired} expired replays and {orphaned} replays of deleted tournaments");
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
