using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using SiraUtil.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistantShared.Models;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.server-selection-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\server-selection-view.bsml")]
    internal class ServerSelectionView : BSMLAutomaticViewController
    {
        [Inject]
        protected readonly SiraLog _siraLog;

        [UIComponent("server-list")]
        protected readonly CustomCellListTableData _serverList = null!;

        public event Action<CoreServer>? ServerSelected;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            _serverList.tableView.ClearSelection();
        }

        public void SetServers(List<CoreServer> servers)
        {
            if (servers != null)
            {
                _serverList.data.Clear();
                _serverList.data.AddRange(servers.Select(x => new ServerListItem(x)));
                _serverList.tableView.ReloadData();
            }
        }

        [UIAction("server-selected")]
        protected void ServerClicked(TableView _, ServerListItem serverItem)
        {
            _siraLog.Info($"Clicked on: " + serverItem.server.Address);
            ServerSelected?.Invoke(serverItem.server);
        }

        internal class ServerListItem
        {
            public readonly CoreServer server;
            private readonly Texture2D _texture;

            [UIValue("server-name")]
            protected readonly string _serverName;

            [UIValue("server-details")]
            protected readonly string _serverDetails;

            [UIComponent("background")]
            protected readonly RawImage _background = null!;

            [UIComponent("server-details-text")]
            protected readonly CurvedTextMeshPro _serverDetailsText = null!;

            public ServerListItem(CoreServer server)
            {
                this.server = server;
                _serverName = server.Name;
                _serverDetails = $"{server.Address}:{server.Port}";
                _texture = new Texture2D(1, 1);
                _texture.SetPixel(0, 0, Color.white);
            }

            [UIAction("refresh-visuals")]
            public void Refresh(bool _, bool __)
            {
                _background.texture = _texture;
                _background.color = new Color(1f, 1f, 1f, 0.125f);
                _serverDetailsText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
            }
        }
    }
}