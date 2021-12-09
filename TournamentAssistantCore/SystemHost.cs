using System.Threading;

namespace TournamentAssistantCore
{
    class SystemHost
    {
        public static SystemServer Server;
        public static AutoResetEvent MainThreadStop = new(false);


        static void Main(string[] args)
        {
            Server = new SystemServer(args.Length > 0 ? args[0] : null);
            Server.Start();

            MainThreadStop.WaitOne();
        }
    }
}
