using System;
using System.Threading;
using TournamentAssistantShared;

namespace TournamentAssistantCore
{
    class SystemHost
    {
        public static IConnection Connection;
        public static AutoResetEvent MainThreadStop = new(false);


        static void Main(string[] args)
        {
            Connection = new SystemServer(args.Length > 0 ? args[0] : null);
            (Connection as SystemServer).Start();

            MainThreadStop.WaitOne();
        }
    }
}
