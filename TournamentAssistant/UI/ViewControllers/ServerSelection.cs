#pragma warning disable CS0649
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
    internal class ServerSelection : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action<CoreServer> ServerSelected;

        [UIComponent("server-list")]
        public CustomCellListTableData serverList;

        [UIValue("servers")]
        public List<object> servers = new();

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

        [UIAction("#post-parse")]
        private void PostParse()
        {
            serverList?.tableView.ReloadData();
        }
    }
}
