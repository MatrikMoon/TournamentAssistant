using System.Linq;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.UI.CustomListItems
{
    internal static class RoundedListHighlight
    {
        private static Image soloBackground;
        private static Sprite fallbackSprite;

        public static void Apply(Image image, bool selected, bool highlighted, float leftOverflow = 0f)
        {
            ApplyStyle(image, selected, highlighted);

            image.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            image.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = new Vector2(-leftOverflow / 2f, 0f);
            image.rectTransform.sizeDelta = new Vector2(leftOverflow - 1.4f, 10f);
        }

        public static void ApplyStyle(Image image, bool selected, bool highlighted)
        {
            if (soloBackground == null)
            {
                var soloCell = Resources.FindObjectsOfTypeAll<LevelListTableCell>().FirstOrDefault();
                soloBackground = soloCell?.GetField<Image>("_backgroundImage");
            }

            fallbackSprite ??= Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(x => x.name == "RoundRect10Thin");
            image.sprite = soloBackground?.sprite ?? fallbackSprite;
            image.material = soloBackground?.material;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = soloBackground?.pixelsPerUnitMultiplier ?? 1f;
            image.raycastTarget = false;

            if (selected && highlighted)
            {
                image.color = new Color(0f, 0.7529412f, 1f, 0.7490196f);
            }
            else if (selected)
            {
                image.color = new Color(0f, 0.7529412f, 1f, 1f);
            }
            else
            {
                image.color = new Color(1f, 1f, 1f, 0.2f);
            }

            image.enabled = selected || highlighted;
        }
    }
}
