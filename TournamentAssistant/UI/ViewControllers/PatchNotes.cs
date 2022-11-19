#pragma warning disable IDE0044
#pragma warning disable CS0649
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

        [UIObject("background")]
        private GameObject background;

        [UIComponent("patchNotes")]
        private HMUI.TextPageScrollView patchNotesBox;

        [UIValue("patch-notes-text")]
        private string patchNotesText = Plugin.GetLocalized("patch_notes");

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            OnViewCreation();
        }
        void OnViewCreation()
        {
            string[] notes = Constants.Changelog.Split(new[] { "\n" }, StringSplitOptions.None);
            string writeToBox = "";
            foreach (string item in notes.Reverse()) writeToBox += item + "\n";

            patchNotesBox.SetText(writeToBox);
            BackgroundOpacity();
        }
        void BackgroundOpacity()
        {
            var Image = background?.GetComponent<HMUI.ImageView>() ?? null;
            var Color = Image.color;
            Color.a = 0.5f;
            Image.color = Color;
        }
    }
}
