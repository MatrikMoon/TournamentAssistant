﻿using HMUI;
using IPA.Utilities.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistant.UI.FlowCoordinators
{
    abstract class FlowCoordinatorWithScrapedInfo : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;
        protected void RaiseDidFinishEvent() => DidFinishEvent?.Invoke();


        //This is a hack to allow the Qualifier page to see events hosted by servers that aren't the master server
        // The first pass downloads all the hosts from the master server, then the second pass downloads the state (including the Events) from each of those servers
        public bool RescrapeForSecondaryEvents { get; set; }
        public Dictionary<CoreServer, State> ScrapedInfo { get; set; }

        protected virtual Task OnUserDataResolved(string username, ulong userId)
        {
            //Run asynchronously to not block ui
            Task.Run(async () =>
            {
                try
                {
                    async Task ScrapeHosts()
                    {
                        //Commented out is the code that makes this operate as a mesh network
                        ScrapedInfo = (await HostScraper.ScrapeHosts(Plugin.config.GetHosts(), username, userId, onInstanceComplete: OnIndividualInfoScraped))
                            .Where(x => x.Value != null)
                            .ToDictionary(s => s.Key, s => s.Value);

                        //Since we're scraping... Let's save the data we learned about the hosts while we're at it
                        var newHosts = Plugin.config.GetHosts().Union(ScrapedInfo.Values.Where(x => x.KnownHosts != null).SelectMany(x => x.KnownHosts), new CoreServerEqualityComparer()).ToList();
                        Plugin.config.SaveHosts(newHosts.ToArray());
                    }

                    //Clear the saved hosts so we don't have stale ones clogging us up
                    Plugin.config.SaveHosts(new CoreServer[] { });

                    await ScrapeHosts();
                    if (RescrapeForSecondaryEvents) await ScrapeHosts();

                }
                finally
                {
                    await UnityMainThreadTaskScheduler.Factory.StartNew(OnInfoScraped);
                }
            });
            return Task.CompletedTask;
        }

        protected override void TransitionDidFinish()
        {
            base.TransitionDidFinish();

            //TODO: Review whether this could cause issues. Probably need debouncing or something similar
            if (ScrapedInfo == null) Task.Run(() => PlayerUtils.GetPlatformUserData(OnUserDataResolved));
        }

        protected abstract void OnIndividualInfoScraped(CoreServer host, State state, int count, int total);
        protected abstract void OnInfoScraped();

        public virtual void Dismiss() => RaiseDidFinishEvent();
    }
}
