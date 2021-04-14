using TournamentAssistantUI.Misc;
using System.Windows;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            MouseHook.StopHook();
        }
    }
}
