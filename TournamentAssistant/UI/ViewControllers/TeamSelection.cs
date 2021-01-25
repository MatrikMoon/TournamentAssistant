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
    class TeamSelection : BSMLResourceViewController
    {
        public override string ResourceName => $"TournamentAssistant.UI.Views.{GetType().Name}.bsml";

        public event Action CreateTeamPressed;
        public event Action<Team> TeamSelected;

        [UIComponent("team-list")]
        public CustomCellListTableData teamList;

        [UIValue("teams")]
        public List<object> teams = new List<object>();

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            teamList.tableView.ClearSelection();
        }

        public void SetTeams(List<Team> teams)
        {
            this.teams.Clear();

            if (this.teams != null)
            {
                this.teams.AddRange(teams.Select(x => new TeamListItem(x)));
            }

            teamList?.tableView.ReloadData();
        }

        [UIAction("create-room-pressed")]
        private void CreateteamClicked()
        {
            CreateTeamPressed?.Invoke();
        }

        [UIAction("team-selected")]
        private void TeamClicked(TableView sender, TeamListItem teamListItem)
        {
            TeamSelected?.Invoke(teamListItem.team);
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            teamList?.tableView.ReloadData();
        }
    }
}
