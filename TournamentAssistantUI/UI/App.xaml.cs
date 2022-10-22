using TournamentAssistantUI.Misc;
using System.Windows;
using System;
using System.Threading.Tasks;
using System.IO;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private StreamWriter? _logWriter;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            SetupExceptionHandling();
        }

        private void SetupExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            DispatcherUnhandledException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };
        }

        private void LogUnhandledException(Exception exception, string source)
        {
            _logWriter ??= File.AppendText($"TournamentAssistantUI-{DateTime.Now:yyMMdd'.log'}");
            try
            {
                string message = $"[{DateTime.Now:O}] Unhandled exception ({source}) {exception}";
                System.Reflection.AssemblyName assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
                _logWriter.WriteLine(message);
            }
            catch (Exception ex)
            {
                string message = $"Exception of unhandled ({source}) {ex}";
                _logWriter.WriteLine(message);
            }
            finally
            {
                _logWriter.Flush();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            MouseHook.StopHook();
        }
    }
}
