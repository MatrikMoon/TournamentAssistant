using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TournamentAssistantUI.Views
{
    public class ScrapedServersView : UserControl
    {
        public ScrapedServersView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
