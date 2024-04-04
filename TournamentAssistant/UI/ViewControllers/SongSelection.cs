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
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class SongSelection : BSMLAutomaticViewController
    {
        public event Action<Map> SongSelected;

        [UIComponent("song-list")]
        public CustomCellListTableData songList;

        [UIValue("maps")]
        public List<object> maps = new();

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            songList.tableView.ClearSelection();
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

            if (removedFromHierarchy) DisposeArtTextures();
        }

        /*public void SetSongs(List<IPreviewBeatmapLevel> songs)
        {
            this.songs.Clear();
            this.songs.AddRange(songs.Select(x =>
            {
                var parameters = new GameplayParameters
                {
                    Beatmap = new Beatmap
                    {
                        LevelId = x.levelID,
                        Characteristic = new Characteristic
                        {
                            SerializedName = "Standard"
                        },
                        Difficulty = (int)TournamentAssistantShared.Constants.BeatmapDifficulty.ExpertPlus
                    },
                    GameplayModifiers = new TournamentAssistantShared.Models.GameplayModifiers(),
                    PlayerSettings = new TournamentAssistantShared.Models.PlayerSpecificSettings()
                };
                return new SongListItem(parameters);
            }));

            songList?.tableView.ReloadData();
        }*/

        public void SetSongs(List<Map> maps)
        {
            this.maps.Clear();
            this.maps.AddRange(maps.Select(x => new SongListItem(x)));

            songList?.tableView.ReloadData();
        }

        [UIAction("song-selected")]
        private void ServerClicked(TableView sender, SongListItem songListItem)
        {
            SongSelected?.Invoke(songListItem.map);
        }

        //We need to dispose all the textures we've created, so... This is the best option I know of
        //Also disposes the textures that would be loaded by scrolling normally in the solo menu, so... Win-win?
        public void DisposeArtTextures() => maps.ForEach(x => (x as SongListItem).Dispose());
    }
}
