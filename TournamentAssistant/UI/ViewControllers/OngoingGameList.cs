#pragma warning disable CS0649
#pragma warning disable IDE0060
#pragma warning disable IDE0051
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.CustomListItems;
using TournamentAssistantShared.Models;
using UnityEngine;
using static BeatSaberMarkupLanguage.Components.CustomListTableData;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class OngoingGameList : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        [UIComponent("list")]
        public CustomListTableData customListTableData;

        public event Action<Match> MatchClicked;

        //We need to keep track of matches like this because it is very possible
        //that we'll want to add a match to the list and that logic will come through
        //before the list is actually displayed. This way, we can handle that situation
        //and avoid null exceptions / missing data
        //NOTE: This is called by the Server thread all times except the initial load,
        //so it should not be necessary to lock anything here
        private MatchCellInfo[] _matches = new MatchCellInfo[] { };
        private MatchCellInfo[] Matches
        {
            get
            {
                return _matches;
            }
            set
            {
                _matches = value;
                if (customListTableData != null)
                {
                    customListTableData.data = new List<CustomCellInfo>(_matches.Cast<CustomCellInfo>() as CustomCellInfo[]);

                    //Must be run on main thread
                    UnityMainThreadDispatcher.Instance().Enqueue(() => customListTableData.tableView.ReloadData());
                }
            }
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (firstActivation)
            {
                rectTransform.anchorMin = new Vector3(0.5f, 0, 0);
                rectTransform.anchorMax = new Vector3(0.5f, 1, 0);
                rectTransform.sizeDelta = new Vector3(70, 0, 0);
            }
            if (addedToHierarchy)
            {
                customListTableData.data = new List<CustomCellInfo>(_matches.Cast<CustomCellInfo>() as CustomCellInfo[]);
                customListTableData.tableView.ReloadData();
            }
        }

        [UIAction("match-click")]
        private void ClickedRow(TableView table, int row)
        {
            MatchClicked?.Invoke((customListTableData.data[row] as MatchCellInfo).Match);
        }

        public void AddMatch(Match match)
        {
            var matchList = Matches.ToList();
            matchList.Add(new MatchCellInfo(match));
            Matches = matchList.ToArray();
        }

        public void AddMatches(Match[] matches)
        {
            var matchList = Matches.ToList();
            matchList.AddRange(matches.Select(x => new MatchCellInfo(x)));
            Matches = matchList.ToArray();
        }

        public void RemoveMatch(Match match)
        {
            var matchList = Matches.ToList();
            matchList.RemoveAll(x => (x as MatchCellInfo).Match == match);
            Matches = matchList.ToArray();
        }

        public void ClearMatches()
        {
            Matches = new MatchCellInfo[] { };
        }
    }
}
