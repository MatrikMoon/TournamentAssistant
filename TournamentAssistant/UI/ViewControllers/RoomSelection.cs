#pragma warning disable CS0649
#pragma warning disable IDE0060
#pragma warning disable IDE0051
#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections.Generic;
using TournamentAssistant.UI.CustomListItems;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class RoomSelection : BSMLAutomaticViewController
    {
        public event Action CreateMatchPressed;
        public event Action<Match> MatchSelected;

        [UIComponent("match-list")]
        public CustomCellListTableData matchList;

        [UIValue("create-room-text")]
        private string createRoomText = Plugin.GetLocalized("create_room");

        [UIValue("matches")]
        public List<object> matches = new();

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            matchList.tableView.ClearSelection();
        }

        public void SetMatches(List<Match> matches)
        {
            // TODO
            /*this.matches.Clear();

            if (this.matches != null)
            {
                this.matches.AddRange(matches.Select(x => new MatchListItem(Plugin.client.SelectedTournament, x)));
            }

            matchList?.tableView.ReloadData();*/
        }

        [UIAction("create-room-pressed")]
        private void CreateMatchClicked()
        {
            CreateMatchPressed?.Invoke();
        }

        [UIAction("match-selected")]
        private void MatchClicked(TableView sender, MatchListItem matchListItem)
        {
            MatchSelected?.Invoke(matchListItem.Match);
        }
    }
}
