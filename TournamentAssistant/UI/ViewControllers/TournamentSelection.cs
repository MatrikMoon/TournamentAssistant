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
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.ViewControllers
{
    [HotReload(RelativePathToLayout = @"TournamentSelection.bsml")]
    internal class TournamentSelection : BSMLAutomaticViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.

        public event Action<Tournament> TournamentSelected;

        [UIComponent("tournament-list")]
        public CustomCellListTableData tournamentList;

        [UIValue("tournaments")]
        public List<object> tournaments = new();

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            tournamentList.tableView.ClearSelection();
        }

        public void SetTournaments(List<Tournament> tournaments)
        {
            if (tournaments != null)
            {
                this.tournaments.Clear();
                this.tournaments.AddRange(tournaments.Select(x => new TournamentListItem(x)));
                tournamentList?.tableView.ReloadData();
            }
        }

        [UIAction("tournament-selected")]
        private void TournamentClicked(TableView sender, TournamentListItem tournamentListItem)
        {
            TournamentSelected?.Invoke(tournamentListItem.tournament);
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            tournamentList?.tableView.ReloadData();
        }
    }
}
