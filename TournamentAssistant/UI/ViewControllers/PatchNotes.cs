using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using System.Linq;
using TournamentAssistantShared;
using UnityEngine;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class PatchNotes : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        [UIObject("Background")]
        private GameObject Background = null;
        [UIComponent("PatchNotes")]
        private HMUI.TextPageScrollView PatchNotesBox = null;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            OnViewCreation();
        }
        void OnViewCreation()
        {
            string[] Notes = SharedConstructs.Changelog.Split(new[] { "\n" }, StringSplitOptions.None);
            string writeToBox = "";
            foreach (string item in Notes.Reverse()) writeToBox += item + "\n";


            PatchNotesBox.SetText(writeToBox);
            BackgroundOpacity();
        }
        void BackgroundOpacity()
        {
            var Image = Background?.GetComponent<HMUI.ImageView>() ?? null;
            var Color = Image.color;
            Color.a = 0.5f;
            Image.color = Color;
        }
    }
}
