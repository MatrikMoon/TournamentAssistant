using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantUI.Models;
using TournamentAssistantUI.ViewModels;

namespace TournamentAssistantUI.Views
{
    public class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            this.WhenActivated(d => d(ViewModel.ConnectDialog.RegisterHandler(DoShowDialogAsync)));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        private async Task DoShowDialogAsync(InteractionContext<ConnectWindowViewModel, SystemClient> interaction)
        {
            var dialog = new UsernamePasswordDialog();
            dialog.DataContext = interaction.Input;

            var result = await dialog.ShowDialog<SystemClient>(this);
            interaction.SetOutput(result);
        }
    }
}
