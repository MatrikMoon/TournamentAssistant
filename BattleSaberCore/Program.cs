using System;
using BattleSaberShared;

namespace BattleSaberCore
{
    class Program
    {
        public IConnection Connection { get; }

        static void Main(string[] args)
        {
            new BattleSaberHost().StartHost();
            Console.ReadLine();
        }
    }
}
