using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using TournamentAssistantUI.ViewModels;

namespace TournamentAssistantUI.Views
{
    public class LoadingDialog : ReactiveWindow<LoadingDialogViewModel>
    {
        public LoadingDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            this.WhenActivated(d => d(ViewModel.CredentialButtonPressed.Subscribe(Close)));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
