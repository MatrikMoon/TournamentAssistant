using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TournamentAssistantUI.Models;

namespace TournamentAssistantUI.ViewModels
{
    public class UsernamePasswordDialogViewModel : ConnectWindowViewModel
    {
        internal ReactiveCommand<Unit, UsernamePasswordModel> CredentialButtonPressed { get; }
        private bool _CredentialButtonEnabled = true;
        private string _UsernameText;
        private string _PasswordText;
        private bool _RemeberCredentialsChecked = false;
        private bool _IsPasswordProtected;
        public bool CredentialButtonEnabled
        {
            get => _CredentialButtonEnabled;
            set => this.RaiseAndSetIfChanged(ref _CredentialButtonEnabled, value);
        }
        public string UsernameText
        {
            get => _UsernameText;
            set => this.RaiseAndSetIfChanged(ref _UsernameText, value);
        }
        public string PasswordText
        {
            get => _PasswordText;
            set => this.RaiseAndSetIfChanged(ref _PasswordText, value);
        }
        public bool RemeberCredentialsChecked
        {
            get => _RemeberCredentialsChecked;
            set => this.RaiseAndSetIfChanged(ref _RemeberCredentialsChecked, value);
        }
        public new bool IsPasswordProtected
        {
            get => _IsPasswordProtected;
            set => this.RaiseAndSetIfChanged(ref _IsPasswordProtected, value);
        }

        public UsernamePasswordDialogViewModel()
        {
            CredentialButtonPressed = ReactiveCommand.Create(() =>
            {
                if (PasswordText != null && UsernameText == null) return new UsernamePasswordModel { Password = PasswordText };
                return UsernameText != null ? new UsernamePasswordModel { Username = UsernameText, Password = PasswordText } : new UsernamePasswordModel { Password = null };
            });
        }
    }
}
