using CustomUI.BeatSaber;
using HMUI;
using System;
using System.Linq;
using TMPro;
using TournamentAssistant.Misc;
using UnityEngine;

namespace TournamentAssistant.UI.ViewControllers
{
    class SongViewController : CustomViewController, ITableCellOwner
    {
        private IPreviewBeatmapLevel _selectedLevel;
        private LevelListTableCell _songCell;

        public event Action<IPreviewBeatmapLevel> CellSelected;

        public TableViewSelectionType selectionType => TableViewSelectionType.None;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            base.DidActivate(firstActivation, activationType);
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                rectTransform.sizeDelta -= new Vector2(80, 0); //ViewController width needs to be 80ish to fit on screen with a detail view controller

                _songCell = Instantiate(Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell")), rectTransform, false);
                (_songCell.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
                (_songCell.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
                (_songCell.transform as RectTransform).anchoredPosition = new Vector2(-25f, 17.5f);
                _songCell.SetField("_bought", true);
                _songCell.TableViewSetup(this, 0);
                _songCell.gameObject.SetActive(false);
            }
        }

        /*
        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            Plugin.client.Shutdown();
        }
        */

        //This is here just in case the user quits the game while on our screen
        public void OnApplicationQuit()
        {
            Plugin.client.Shutdown();
        }

        public void SetData(IBeatmapLevel level)
        {
            _selectedLevel = level;

            _songCell.SetDataFromLevelAsync(level);
            _songCell.gameObject.SetActive(true);
        }

        public void CellSelectionStateDidChange(TableCell changedCell)
        {
            
        }

        public void CellWasPressed(TableCell cell)
        {
            CellSelected?.Invoke(_selectedLevel);
        }
    }
}
