using System;
using TournamentAssistantShared;

namespace TournamentAssistantCore
{
    class SystemHost
    {
        public static IConnection Connection;

        static void Main(string[] args)
        {
            Connection = new SystemServer(args.Length > 0 ? args[0] : null);
            (Connection as SystemServer).Start();

            Console.ReadLine();
        }
    }
}
