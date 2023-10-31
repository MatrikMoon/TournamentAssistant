using Fleck;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TournamentAssistantServer
{
    class Entry
    {
        public static TAServer Server;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            Server = new TAServer(args.Length > 0 ? args[0] : null);
            Server.Start();

            //DatabaseTester.TestDatabases();

            //Block forever
            new AutoResetEvent(false).WaitOne();
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var exception = (Exception)e.Exception;
            Console.WriteLine("Unobserved Task Exception Handler caught : " + exception.Message);
            Console.WriteLine("Stack Trace: " + exception.StackTrace);
        }

        private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception)e.ExceptionObject;
            Console.WriteLine("Unhandled Exception Handler caught : " + exception.Message);
            Console.WriteLine("Stack Trace: " + exception.StackTrace);
            Console.WriteLine("Runtime terminating: {0}", e.IsTerminating);
        }
    }
}
