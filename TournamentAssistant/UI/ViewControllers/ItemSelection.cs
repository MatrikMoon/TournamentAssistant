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

namespace TournamentAssistant.UI.ViewControllers
{
    internal class ItemSelection : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action<ListItem> ItemSelected;

        [UIComponent("item-list")]
        public CustomCellListTableData itemList;

        [UIValue("items")]
        public List<object> items = new();

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
