﻿#pragma warning disable CS0649
#pragma warning disable IDE0051

using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class PasswordEntry : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action<string> PasswordEntered;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            //BackgroundOpacity();
        }

        /*[UIObject("Background")]
        internal GameObject Background = null;
        void BackgroundOpacity()
        {
            var Image = Background?.GetComponent<HMUI.ImageView>() ?? null;
            var Color = Image.color;
            Color.a = 0.5f;
            Image.color = Color;
        }*/

        [UIValue("password")]
        private string password;

        [UIAction("connect")]
        public void OnConnect()
        {
            PasswordEntered?.Invoke(password);
        }
    }
}
