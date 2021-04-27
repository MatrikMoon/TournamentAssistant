using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System.Threading.Tasks;
using TournamentAssistantUI.Models;
using TournamentAssistantUI.ViewModels;

namespace TournamentAssistantUI.Views
{
    public class ConnectWindow : ReactiveWindow<ConnectWindowViewModel>
    {
        public ConnectWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            this.WhenActivated(d => d(ViewModel.UsernamePasswdDialog.RegisterHandler(DoShowDialogAsync)));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async Task DoShowDialogAsync(InteractionContext<UsernamePasswordDialogViewModel, UsernamePasswordModel> interaction)
        {
            var dialog = new UsernamePasswordDialog();
            dialog.DataContext = interaction.Input;

            var result = await dialog.ShowDialog<UsernamePasswordModel>(this);
            interaction.SetOutput(result);
        }
    }
}
