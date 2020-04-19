#pragma warning disable 0649
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
    class RoomSelection : BSMLResourceViewController
    {
        public override string ResourceName => $"TournamentAssistant.UI.View.{GetType().Name}.bsml";

        public event Action<Match> MatchSelected;

        [UIComponent("match-list")]
        public CustomCellListTableData matchList;

        [UIValue("matches")]
        public List<object> matches = new List<object>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);
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

        [UIAction("match-selected")]
        private void MatchClicked(TableView sender, MatchListItem matchListItem)
        {
            MatchSelected?.Invoke(matchListItem.match);
        }
    }
}
