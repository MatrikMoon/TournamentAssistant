using HMUI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/**
 * Created by Moon on 10/28/2018 at 2:18am
 * Intended to extend the functionality of LeaderboardTableView
 * so we can set custom colors on all the players
 * Update 9/11/2020: Modified to remove teams
 */

namespace TournamentAssistant.UI.Views
{
    class CustomLeaderboardTable : MonoBehaviour, TableView.IDataSource
    {
        protected int _specialScoreRow;
        protected float _rowHeight = 5f;

        private TableView _tableView;
        private LeaderboardTableCell _cellInstance;
        private List<CustomScoreData> _scores;

        public void Awake()
        {
            var viewGO = new GameObject();
            viewGO.SetActive(false);
            _tableView = viewGO.AddComponent<TableView>();
            _tableView.transform.SetParent(transform, false);
            _tableView.SetField("_isInitialized", false);
            _tableView.SetField("_preallocatedCells", new TableView.CellsGroup[0]);
            _tableView.InvokeMethod("Init");
            viewGO.SetActive(true);

            //Following fix courtesy of superrob's multiplayer fork
            RectTransform viewport = new GameObject("Viewport").AddComponent<RectTransform>();
            viewport.SetParent(_tableView.transform as RectTransform, false);
            viewport.sizeDelta = new Vector2(0f, 58f);
            _tableView.SetField("_scrollRectTransform", viewport);

            var currentView = Resources.FindObjectsOfTypeAll<LeaderboardTableView>().First();
            var currentTransform = (currentView.transform as RectTransform);
            var newTransform = (_tableView.transform as RectTransform);

            //TODO: Wouldn't it be easier to set anchors to .5 across the board, then work from there?
            newTransform.anchorMin = new Vector2(currentTransform.anchorMin.x, currentTransform.anchorMin.y);
            newTransform.anchorMax = new Vector2(currentTransform.anchorMax.x, currentTransform.anchorMax.y);
            newTransform.anchoredPosition = new Vector2(currentTransform.anchoredPosition.x - 6, currentTransform.anchoredPosition.y + 2); //In 0.12.0, the table was moved slightly to the right. Here I'm moving it back. Oh, and down.
                                                                                                                                           //In 0.13.1, it was changed again slightly. Just wanted to note that
            newTransform.sizeDelta = new Vector2(currentTransform.sizeDelta.x - 44, currentTransform.sizeDelta.y - 20);

            _cellInstance = Resources.FindObjectsOfTypeAll<LeaderboardTableCell>().First(x => x.name == "LeaderboardTableCell");
        }

        public TableCell CellForIdx(TableView tableView, int row)
        {
            LeaderboardTableCell leaderboardTableCell = Instantiate(_cellInstance);
            leaderboardTableCell.reuseIdentifier = "Cell";

            CustomScoreData scoreData = _scores[row];
            leaderboardTableCell.rank = scoreData.rank;
            leaderboardTableCell.playerName = scoreData.playerName;
            leaderboardTableCell.score = scoreData.score / 2;
            leaderboardTableCell.showFullCombo = scoreData.fullCombo;
            leaderboardTableCell.showSeparator = (row != _scores.Count - 1);
            leaderboardTableCell.specialScore = (_specialScoreRow == row);
            if (_specialScoreRow != row)
            {
                ColorUtility.TryParseHtmlString(scoreData.Color, out var parsedColor);
                leaderboardTableCell.GetField<TextMeshProUGUI>("_playerNameText").color = parsedColor;
            }

            return leaderboardTableCell;
        }

        public int NumberOfCells() => _scores == null ? 0 : _scores.Count;

        public float CellSize() => _rowHeight;

        public virtual void SetScores(List<CustomScoreData> scores, int specialScoreRow)
        {
            _scores = scores;
            _specialScoreRow = specialScoreRow;
            if (_tableView.dataSource == null)
            {
                _tableView.dataSource = this;
            }
            else
            {
                _tableView.ReloadData();
            }
        }

        public class CustomScoreData : LeaderboardTableView.ScoreData
        {
            public string Color
            {
                get;
                private set;
            }

            public CustomScoreData(int score, string playerName, int place, bool fullCombo, string color = "#ffffff") : base(score, playerName, place, fullCombo)
            {
                Color = color;
            }
        }
    }
}
