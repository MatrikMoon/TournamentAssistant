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
using TournamentAssistant.UI.CustomListItems;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class RoomSelection : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action CreateMatchPressed;
        public event Action<Match> MatchSelected;

        [UIComponent("match-list")]
        public CustomCellListTableData matchList;

        [UIValue("matches")]
        public List<object> matches = new();

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            matchList.tableView.ClearSelection();
        }

        public void SetMatches(List<Match> matches)
        {
            this.matches.Clear();

            if (this.matches != null)
            {
                this.matches.AddRange(matches.Select(x => new MatchListItem(x)));
            }

            matchList?.tableView.ReloadData();
        }

        [UIAction("create-room-pressed")]
        private void CreateMatchClicked()
        {
            CreateMatchPressed?.Invoke();
        }

        [UIAction("match-selected")]
        private void MatchClicked(TableView sender, MatchListItem matchListItem)
        {
            MatchSelected?.Invoke(matchListItem.match);
        }
    }
}
