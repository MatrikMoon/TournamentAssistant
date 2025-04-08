﻿#pragma warning disable CS0649
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
using Team = TournamentAssistantShared.Models.Tournament.TournamentSettings.Team;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class TeamSelection : BSMLAutomaticViewController
    {
        public event Action CreateTeamPressed;
        public event Action<Team> TeamSelected;

        [UIComponent("team-list")]
        public CustomCellListTableData teamList;

        [UIValue("teams")]
        public List<object> teams = new();

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            teamList.TableView.ClearSelection();
        }

        public void SetTeams(List<Team> teams)
        {
            this.teams.Clear();

            if (this.teams != null)
            {
                this.teams.AddRange(teams.Select(x => new TeamListItem(x)));
            }

            teamList?.TableView.ReloadData();
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
            teamList?.TableView.ReloadData();
        }
    }
}
