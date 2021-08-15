using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistantShared.Models;
using UnityEngine;
using static BeatSaberMarkupLanguage.Components.CustomListTableData;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.ongoing-game-list-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\ongoing-game-list-view.bsml")]
    internal class OngoingGameListView : BSMLAutomaticViewController
    {
        [UIComponent("game-list")]
        protected readonly CustomListTableData _gameList = null!;

        public event Action<Match>? MatchClicked;

        private List<MatchCellInfo> _matches = new();
        private List<MatchCellInfo> Matches
        {
            get
            {
                return _matches;
            }
            set
            {
                _matches = value;
                if (_gameList != null)
                {
                    _gameList.data = new List<CustomCellInfo>(_matches.Cast<CustomCellInfo>() as CustomCellInfo[]);
                    _gameList.tableView.ReloadData();
                }
            }
        }

        [UIAction("match-clicked")]
        private void ClickedRow(TableView table, int row)
        {
            MatchClicked?.Invoke((_gameList.data[row] as MatchCellInfo)!.match);
        }

        internal void AddMatches(params Match[] matches)
        {
            Matches.AddRange(matches.Select(m => new MatchCellInfo(m)));
            Matches = _matches;
        }

        internal void RemoveMatch(Match match)
        {
            _matches.RemoveAll(m => m.match == match);
            Matches = _matches;
        }

        internal void ClearMatches()
        {
            Matches = new();
        }

        protected class MatchCellInfo : CustomCellInfo
        {
            public readonly Match match;

            public MatchCellInfo(Match match) : base($"Host: {match.Leader.Name} - {match.Players.Length} Players")
            {
                this.match = match;
            }
        }
    }
}