using System;
using TournamentAssistantShared;

namespace TournamentAssistantCore
{
    class Program
    {
        public IConnection Connection { get; }

        static void Main(string[] args)
        {
            new TournamentAssistantHost().StartHost();
            Console.ReadLine();
        }
    }
}
