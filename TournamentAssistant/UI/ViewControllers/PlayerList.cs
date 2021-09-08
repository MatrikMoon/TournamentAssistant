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
    internal class PlayerList : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        [UIComponent("list")]
        public CustomListTableData customListTableData;

        public event Action<Player> PlayerClicked;

        //We need to keep track of matches like this because it is very possible
        //that we'll want to add a match to the list and that logic will come through
        //before the list is actually displayed. This way, we can handle that situation
        //and avoid null exceptions / missing data
        //NOTE: This is called by the Server thread all times except the initial load,
        //so it should not be necessary to lock anything here
        private PlayerCellInfo[] _players = new PlayerCellInfo[] { };
        public Player[] Players
        {
            get
            {
                return _players.Select(x => x.Player).ToArray();
            }
            set
            {
                _players = value.Select(x => new PlayerCellInfo(x)).ToArray();
                if (customListTableData != null)
                {
                    customListTableData.data = new List<CustomCellInfo>(_players.Cast<CustomCellInfo>() as CustomCellInfo[]);

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
                customListTableData.data = new List<CustomCellInfo>(_players.Cast<CustomCellInfo>() as CustomCellInfo[]);
                customListTableData.tableView.ReloadData();
            }
        }

        [UIAction("player-click")]
        private void ClickedRow(TableView table, int row)
        {
            PlayerClicked?.Invoke((customListTableData.data[row] as PlayerCellInfo).Player);
        }
    }
}
