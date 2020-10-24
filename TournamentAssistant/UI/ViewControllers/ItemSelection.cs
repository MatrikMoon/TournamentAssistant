#pragma warning disable 0649
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistant.UI.CustomListItems;

namespace TournamentAssistant.UI.ViewControllers
{
    class ItemSelection : BSMLResourceViewController
    {
        public override string ResourceName => $"TournamentAssistant.UI.Views.{GetType().Name}.bsml";

        public event Action<ListItem> ItemSelected;

        [UIComponent("item-list")]
        public CustomCellListTableData itemList;

        [UIValue("items")]
        public List<object> items = new List<object>();

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            itemList.tableView.ClearSelection();
        }

        public void SetItems(List<ListItem> items)
        {
            this.items.Clear();

            if (this.items != null)
            {
                this.items.AddRange(items.Select(x => new GenericItem(x)));
            }

            itemList?.tableView.ReloadData();
        }

        [UIAction("item-selected")]
        private void ItemClicked(TableView sender, GenericItem itemListItem)
        {
            ItemSelected?.Invoke(itemListItem.item);
        }
    }
}
