#pragma warning disable 0649
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
    class ServerSelection : BSMLResourceViewController
    {
        public override string ResourceName => $"TournamentAssistant.UI.Views.{GetType().Name}.bsml";

        public event Action<CoreServer> ServerSelected;
        public event Action ConnectViaIP;

        [UIComponent("server-list")]
        public CustomCellListTableData serverList;

        [UIValue("servers")]
        public List<object> servers = new List<object>();

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            serverList.tableView.ClearSelection();
        }

        public void SetServers(List<CoreServer> servers)
        {
            if (servers != null)
            {
                this.servers.Clear();
                this.servers.AddRange(servers.Select(x => new ServerListItem(x)));
                serverList?.tableView.ReloadData();
            }
        }

        [UIAction("server-selected")]
        private void ServerClicked(TableView sender, ServerListItem serverListItem)
        {
            ServerSelected?.Invoke(serverListItem.server);
        }

        [UIAction("ippress")]
        private void ipPress()
        {
            ConnectViaIP?.Invoke();
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            serverList?.tableView.ReloadData();
        }
    }
}
