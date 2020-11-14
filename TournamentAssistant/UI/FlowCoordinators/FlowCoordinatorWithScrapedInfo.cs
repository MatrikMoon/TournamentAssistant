using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.FlowCoordinators
{
    abstract class FlowCoordinatorWithScrapedInfo : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;
        protected void RaiseDidFinishEvent() => DidFinishEvent?.Invoke();
        public Dictionary<CoreServer, State> ScrapedInfo { get; set; }

        protected async virtual void OnUserDataResolved(string username, ulong userId)
        {
            //Commented out is the code that makes this operate as a mesh network
            /*ScrapedInfo = (await HostScraper.ScrapeHosts(Plugin.config.GetHosts(), username, userId, onInstanceComplete: OnIndividualInfoScraped))
                .Where(x => x.Value != null)
                .ToDictionary(s => s.Key, s => s.Value);*/

            //This code will make the network operate as a hub and spoke network, since networkauditor.org is the domain of the master server
            ScrapedInfo = (await HostScraper.ScrapeHosts(Plugin.config.GetHosts().Where(x => x.Address.Contains("networkauditor")).ToArray(), username, userId, onInstanceComplete: OnIndividualInfoScraped))
                .Where(x => x.Value != null)
                .ToDictionary(s => s.Key, s => s.Value);

            //Since we're scraping... Let's save the data we learned about the hosts while we're at it
            var newHosts = ScrapedInfo.Keys.Union(ScrapedInfo.Values.Where(x => x.KnownHosts != null).SelectMany(x => x.KnownHosts)).ToList();
            Plugin.config.SaveHosts(newHosts.ToArray());

            OnInfoScraped();
        }

        protected override void TransitionDidFinish()
        {
            base.TransitionDidFinish();
            if (ScrapedInfo == null) PlayerUtils.GetPlatformUserData(OnUserDataResolved);
        }

        protected abstract void OnIndividualInfoScraped(CoreServer host, State state, int count, int total);
        protected abstract void OnInfoScraped();

        public virtual void Dismiss() => RaiseDidFinishEvent();
    }
}
