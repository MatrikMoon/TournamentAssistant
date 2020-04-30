#pragma warning disable 0649
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//This file brought to you in part by BeatSaberMultiplayer
//All hail the UI god

namespace BattleSaber.UI.ViewControllers
{
    class SongSelection : BSMLResourceViewController, TableView.IDataSource
    {
        public override string ResourceName => $"BattleSaber.UI.Views.{GetType().Name}.bsml";

        public event Action<IPreviewBeatmapLevel> SongSelected;

        [UIComponent("song-list")]
        public CustomListTableData songTable;

        private LevelListTableCell tableCellBaseInstance;
        private PlayerDataModel _playerDataModel;
        private AdditionalContentModel _additionalContentModel;

        List<IPreviewBeatmapLevel> availableSongs = new List<IPreviewBeatmapLevel>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);
            if (firstActivation)
            {
                _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First();
                _additionalContentModel = Resources.FindObjectsOfTypeAll<AdditionalContentModel>().First();
            }
            if (type == ActivationType.AddedToHierarchy)
            {
                songTable.tableView.didSelectCellWithIdxEvent += SongsTableView_DidSelectRow;
                songTable.tableView.dataSource = this;
            }
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            base.DidDeactivate(deactivationType);
            if (deactivationType == DeactivationType.RemovedFromHierarchy)
            {
                songTable.tableView.didSelectCellWithIdxEvent -= SongsTableView_DidSelectRow;
            }
        }

        public void SetSongs(List<IPreviewBeatmapLevel> levels)
        {
            availableSongs = levels;
            songTable?.tableView.ReloadData();
        }

        private void SongsTableView_DidSelectRow(TableView sender, int row)
        {
            SongSelected?.Invoke(availableSongs[row]);
        }

        public float CellSize()
        {
            return 10f;
        }

        public int NumberOfCells()
        {
            return availableSongs.Count;
        }

        public TableCell CellForIdx(TableView tableView, int idx)
        {
            LevelListTableCell tableCell = (LevelListTableCell)tableView.DequeueReusableCellForIdentifier(songTable.reuseIdentifier);
            if (!tableCell)
            {
                tableCellBaseInstance = tableCellBaseInstance ?? Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell"));
                tableCell = Instantiate(tableCellBaseInstance);
            }

            tableCell.SetDataFromLevelAsync(availableSongs[idx], _playerDataModel.playerData.favoritesLevelIds.Contains(availableSongs[idx].levelID));
            tableCell.RefreshAvailabilityAsync(_additionalContentModel, availableSongs[idx].levelID);

            tableCell.reuseIdentifier = songTable.reuseIdentifier;
            return tableCell;
        }
    }
}
