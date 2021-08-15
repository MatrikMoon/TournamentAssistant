using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.song-selection-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\song-selection-view.bsml")]
    internal class SongSelectionView : BSMLAutomaticViewController
    {
        public event Action<GameplayParameters>? SongSelected;

        [UIComponent("song-list")]
        protected readonly CustomCellListTableData _songList = null!;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            _songList.tableView.ClearSelection();
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            if (removedFromHierarchy)
                DisposeArtTextures();
        }

        public void SetSongs(List<GameplayParameters> songs)
        {
            if (_songList == null)
                return;

            _songList.data.Clear();
            _songList.data.AddRange(songs.Select(x => new SongListItem(x)));

            _songList.tableView.ReloadData();
        }

        [UIAction("song-selected")]
        protected void ServerClicked(TableView _, SongListItem songListItem)
        {
            SongSelected?.Invoke(songListItem.gameplayParameters);
        }

        private void DisposeArtTextures()
        {
            foreach (SongListItem song in _songList.data)
            {
                song.Dispose();
            }
        }

        // I looked at the original version of this class and said "nope". I need to dedicate an entire day rewriting it.
        protected class SongListItem : IDisposable
        {
            public readonly GameplayParameters gameplayParameters;

            public SongListItem(GameplayParameters gameplayParameters)
            {
                this.gameplayParameters = gameplayParameters;
            }

            public void Dispose()
            {

            }
        }
    }
}