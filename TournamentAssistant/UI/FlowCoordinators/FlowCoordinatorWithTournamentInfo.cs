using HMUI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TournamentAssistant.Interop;
using TournamentAssistant.Misc;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;

namespace TournamentAssistant.UI.FlowCoordinators
{
    abstract class FlowCoordinatorWithTournamentInfo : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;
        protected void RaiseDidFinishEvent() => DidFinishEvent?.Invoke();

        protected List<Scraper.TournamentWithServerInfo> Tournaments = new();

        private bool _scrapeInProgress = false;

        protected virtual Task OnUserDataResolved(string username, ulong userId)
        {
            _scrapeInProgress = true;

            //Run asynchronously to not block ui
            Task.Run(() =>
            {
                Scraper.GetTournaments(TAAuthLibraryWrapper.GetToken(username, userId.ToString()), OnIndividualInfoScraped, (data) => {
                    _scrapeInProgress = false;
                    if (data.Tournaments != null)
                    {
                        Tournaments = data.Tournaments;
                    }

                    UnityMainThreadDispatcher.Instance().Enqueue(() => OnInfoScraped(data));
                });
            });
            return Task.CompletedTask;
        }

        protected override void TransitionDidFinish()
        {
            base.TransitionDidFinish();

            //TODO: Review whether this could cause issues. Probably need debouncing or something similar
            if (!_scrapeInProgress && Tournaments.Count <= 0) Task.Run(() => PlayerUtils.GetPlatformUserData(OnUserDataResolved));
        }

        protected abstract void OnIndividualInfoScraped(Scraper.OnProgressData data);
        protected abstract void OnInfoScraped(Scraper.OnProgressData data);

        public virtual void Dismiss() => RaiseDidFinishEvent();
    }
}
