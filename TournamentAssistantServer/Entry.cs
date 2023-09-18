using Fleck;
using System;
using System.Threading;

namespace TournamentAssistantServer
{
    class Entry
    {
        public static TAServer Server;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            Server = new TAServer(args.Length > 0 ? args[0] : null);
            Server.Start();

            //Block forever
            new AutoResetEvent(false).WaitOne();
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
