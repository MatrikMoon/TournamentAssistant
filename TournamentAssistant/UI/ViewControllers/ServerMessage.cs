#pragma warning disable CS0414
#pragma warning disable IDE0060
#pragma warning disable IDE0051
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class ServerMessage : BSMLAutomaticViewController
    {
        public event Action<ModalOption, string> OptionSelected;

        private Request.ShowModal _msg;

        [UIComponent("msgText")]
        private string msgText;

        [UIValue("msg-title")]
        private string msgTitle;

        [UIValue("showBtn1")]
        private bool _btn1 = false;

        [UIValue("btn1Text")]
        private string _btn1Text;

        [UIValue("showBtn2")]
        private bool _btn2 = false;
        [UIValue("btn2Text")]
        private string _btn2Text;

        [UIValue("canClose")]
        private bool _canClose = true;


        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        }

        public void SetMessage(Request.ShowModal msg)
        {
            _msg = msg;
            setUI();
        }

        private void setUI()
        {
            msgText = _msg.MessageText;
            msgTitle = _msg.MessageTitle;
            _btn1 = false;
            _btn2 = false;
            _canClose = _msg.CanClose;
            if (_msg.Option1 != null)
            {
                _btn1 = true;
                _btn1Text = _msg.Option1.Label;
            }

            if (_msg.Option2 != null)
            {
                _btn2 = true;
                _btn2Text = _msg.Option2.Label;
            }
        }

        [UIAction("press1")]
        private void Btn1Pressed()
        {
            if (_msg.Option1 != null) OptionSelected?.Invoke(_msg.Option1, _msg.ModalId);
        }

        [UIAction("press2")]
        private void Btn2Pressed()
        {
            if (_msg.Option2 != null) OptionSelected?.Invoke(_msg.Option2, _msg.ModalId);
        }

        [UIAction("close")]
        private void Close()
        {
            OptionSelected?.Invoke(null, _msg.ModalId);
        }
    }
}
