#pragma warning disable CS0649
#pragma warning disable IDE0044
#pragma warning disable IDE0051
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using System.Threading.Tasks;
using TMPro;
using TournamentAssistantShared;
using UnityEngine;
using UnityEngine.UI;
using static TournamentAssistantShared.Models.Packets.Request;

namespace TournamentAssistant.UI.ViewControllers
{
    [HotReload(RelativePathToLayout = @"Prompt.bsml")]
    internal class Prompt : BSMLAutomaticViewController
    {
        public event Action<string> ButtonPressed;

        [UIObject("splash-background")]
        private GameObject splashBackground = null;

        [UIComponent("body-text")]
        private TextMeshProUGUI bodyText;

        [UIComponent("title-text")]
        private TextMeshProUGUI titleText;

        [UIComponent("button1")]
        private Button button1;

        [UIComponent("button2")]
        private Button button2;

        [UIComponent("button3")]
        private Button button3;

        private string fromPacketId;
        private string fromUserId;
        private TAClient client;

        private string messageTitle;
        private string messageText;
        private int timeout;
        private bool showTimer;
        private bool canClose;
        private ShowPrompt.PromptOption[] options;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            SetDetails(messageTitle, messageText, timeout, showTimer, canClose, options);
            BackgroundOpacity();
        }

        public void SetStartingInfo(string fromPacketId, string fromUserId, TAClient client, ShowPrompt prompt)
        {
            this.fromPacketId = fromPacketId;
            this.fromUserId = fromUserId;
            this.client = client;

            messageTitle = prompt.MessageTitle;
            messageText = prompt.MessageText;
            timeout = prompt.Timeout;
            showTimer = prompt.ShowTimer;
            canClose = prompt.CanClose;
            options = prompt.Options.ToArray();
        }

        private void SetDetails(string titleText, string bodyText, int timeout, bool showTimer, bool canClose, ShowPrompt.PromptOption[] options)
        {
            this.titleText.text = titleText;
            this.bodyText.text = bodyText;

            button1.gameObject.SetActive(false);
            button2.gameObject.SetActive(false);
            button3.gameObject.SetActive(false);

            // showTimer
            // timeout
            // canClose

            if (options.Length > 0)
            {
                button1.gameObject.SetActive(true);
                button1.SetButtonText(options[0].Label);
            }
            if (options.Length > 1)
            {
                button2.gameObject.SetActive(true);
                button2.SetButtonText(options[1].Label);
            }
            if (options.Length > 2)
            {
                button3.gameObject.SetActive(true);
                button3.SetButtonText(options[2].Label);
            }
        }

        [UIAction("button1-pressed")]
        private async Task Button1Clicked()
        {
            await SendResponse(options[0].Value);
            ButtonPressed?.Invoke(options[0].Value);
        }

        [UIAction("button2-pressed")]
        private async Task Button2Clicked()
        {
            await SendResponse(options[1].Value);
            ButtonPressed?.Invoke(options[1].Value);
        }

        [UIAction("button3-pressed")]
        private async Task Button3Clicked()
        {
            await SendResponse(options[3].Value);
            ButtonPressed?.Invoke(options[2].Value);
        }

        private Task SendResponse(string value)
        {
            // If we aren't showing a timer, a default response has already been sent
            if (showTimer)
            {
                return Task.Run(async () => await client.SendPromptResopnse(fromPacketId, fromUserId, value));
            }

            return Task.CompletedTask;
        }

        void BackgroundOpacity()
        {
            var image = splashBackground?.GetComponent<HMUI.ImageView>() ?? null;
            var color = image.color;
            color.a = 0.5f;
            image.color = color;
        }
    }
}
