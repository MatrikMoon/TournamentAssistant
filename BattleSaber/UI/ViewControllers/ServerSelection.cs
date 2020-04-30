#pragma warning disable 0649
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using BattleSaber.Models;
using BattleSaber.UI.CustomListItems;

namespace BattleSaber.UI.ViewControllers
{
    class ServerSelection : BSMLResourceViewController
    {
        public override string ResourceName => $"BattleSaber.UI.Views.{GetType().Name}.bsml";

        public event Action<CoreServer> ServerSelected;

        [UIComponent("server-list")]
        public CustomCellListTableData serverList;

        [UIValue("servers")]
        public List<object> servers = new List<object>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);
            serverList.tableView.ClearSelection();
        }

        public void SetServers(List<CoreServer> servers)
        {
            this.servers.Clear();

            if (servers != null)
            {
                this.servers.AddRange(servers.Select(x => new ServerListItem(x)));
            }

            serverList?.tableView.ReloadData();
        }

        [UIAction("server-selected")]
        private void ServerClicked(TableView sender, ServerListItem serverListItem)
        {
            ServerSelected?.Invoke(serverListItem.server);
        }
    }
}
