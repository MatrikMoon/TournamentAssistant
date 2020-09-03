#pragma warning disable 0649
using BeatSaberMarkupLanguage.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.UI.CustomListItems
{
    public abstract class ListItem
    {
        public string text;
        public string details;
        public string identifier;
    }

    class GenericItem
    {
        public ListItem item;

        [UIValue("item-name")]
        private string itemName;

        [UIValue("item-details")]
        private string itemDetails;

        [UIComponent("item-details-text")]
        private TextMeshProUGUI itemDetailsText;

        [UIComponent("bg")]
        private RawImage background;

        public GenericItem(ListItem item)
        {
            this.item = item;
            itemName = item.text;
            itemDetails = item.details;
        }

        [UIAction("refresh-visuals")]
        public void Refresh(bool selected, bool highlighted)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);

            background.texture = texture;
            background.color = new Color(1f, 1f, 1f, 0.125f);
            itemDetailsText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        }
    }
}
