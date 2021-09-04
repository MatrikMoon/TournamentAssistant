using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistantShared.Models;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.team-selection-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\team-selection-view.bsml")]
    internal class TeamSelectionView : BSMLAutomaticViewController
    {
        public event Action<Team>? TeamSelected;

        [UIComponent("team-list")]
        protected readonly CustomCellListTableData _teamList = null!;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            _teamList.tableView.ClearSelection();
        }

        internal void SetTeams(List<Team> teams)
        {
            _teamList.data.Clear();
            _teamList.data.AddRange(teams.Select(t => new TeamListItem(t)));
            _teamList.tableView.ReloadData();
        }

        [UIAction("team-selected")]
        protected void TeamClicked(TableView _, TeamListItem teamItem)
        {
            TeamSelected?.Invoke(teamItem.team);
        }

        protected class TeamListItem
        {
            public readonly Team team;
            private readonly Texture2D _texture;

            [UIValue("team-name")]
            protected string _teamName;

            [UIValue("team-details")]
            protected string _teamDetails;

            [UIComponent("team-details-text")]
            protected readonly CurvedTextMeshPro _teamDetailsText  = null!;

            [UIComponent("background")]
            protected readonly RawImage _background = null!;

            public TeamListItem(Team team)
            {
                this.team = team;
                _teamName = team.Name;
                _teamDetails = team.Id.ToString();
                _texture = new Texture2D(1, 1);
            }

            [UIAction("refresh-visuals")]
            public void Refresh(bool _, bool __)
            {
                _texture.SetPixel(0, 0, Color.white);

                _background.texture = _texture;
                _background.color = new Color(1f, 1f, 1f, 0.125f);
                _teamDetailsText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
            }
        }
    }
}