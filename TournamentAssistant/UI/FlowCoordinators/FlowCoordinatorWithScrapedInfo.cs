using HMUI;
using System;
using System.Collections.Generic;
using TournamentAssistant.Misc;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.FlowCoordinators
{
    abstract class FlowCoordinatorWithScrapedInfo : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;
        protected void RaiseDidFinishEvent() => DidFinishEvent?.Invoke();
        protected Dictionary<CoreServer, State> ScrapedInfo { get; set; }

        protected async virtual void OnUserDataResolved(string username, ulong userId)
        {
            ScrapedInfo = await HostScraper.ScrapeHosts(Plugin.config.GetServers(), username, userId, OnIndividualInfoScraped);
            OnInfoScraped();
        }

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                PlayerUtils.GetPlatformUserData(OnUserDataResolved);
            }
        }

        protected abstract void OnIndividualInfoScraped(CoreServer host, State state, int count, int total);
        protected abstract void OnInfoScraped();

        public virtual void Dismiss() => RaiseDidFinishEvent();
    }
}
