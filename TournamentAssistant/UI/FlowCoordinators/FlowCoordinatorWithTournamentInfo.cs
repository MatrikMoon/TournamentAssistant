using HMUI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TournamentAssistant.Interop;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using UnityEngine.UI;

namespace TournamentAssistant.UI.FlowCoordinators
{
    abstract class FlowCoordinatorWithTournamentInfo : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;
        protected void RaiseDidFinishEvent() => DidFinishEvent?.Invoke();

        protected List<Scraper.TournamentWithServerInfo> Tournaments = new();

        private bool _scrapeAttempted = false;

        protected virtual Task OnUserDataResolved(string username, ulong userId)
        {
            _scrapeAttempted = true;

            //Run asynchronously to not block ui
            Task.Run(() =>
            {
                Scraper.GetTournaments(TAAuthLibraryWrapper.GetToken(username, userId.ToString()), OnIndividualInfoScraped, (data) => {                    
                    if (data.Tournaments != null)
                    {
                        Tournaments = data.Tournaments;
                    }

                    SetBackButtonInteractivity(true);

                    UnityMainThreadDispatcher.Instance().Enqueue(() => OnInfoScraped(data));
                });
            });
            return Task.CompletedTask;
        }

        protected void SetBackButtonVisibility(bool enable)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                showBackButton = enable;

                var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                screenSystem.SetBackButton(enable, false);
            });
        }

        protected void SetBackButtonInteractivity(bool enable)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                screenSystem.GetField<Button>("_backButton").interactable = enable;
            });
        }

        protected override void TransitionDidFinish()
        {
            base.TransitionDidFinish();

            //TODO: Review whether this could cause issues. Probably need debouncing or something similar
            if (!_scrapeAttempted && Tournaments.Count <= 0)
            {
                SetBackButtonVisibility(true);
                SetBackButtonInteractivity(false);
                Task.Run(() => PlayerUtils.GetPlatformUserData(OnUserDataResolved));
            }
        }

        protected abstract void OnIndividualInfoScraped(Scraper.OnProgressData data);
        protected abstract void OnInfoScraped(Scraper.OnProgressData data);

        public virtual void Dismiss() => RaiseDidFinishEvent();
    }
}
