using System;

/**
 * Modified by Moon on ?/?/2018 (Originally taken from andruzzzhka's work)
 * Simple wrapper for Console.Log which makes logging
 * a little prettier
 */

namespace TournamentAssistantShared
{
    class Logger
    {
        //Added for the purpose of viewing log info in the UI
        public enum LogType
        {
            Error,
            Warning,
            Info,
            Success,
            Debug
        }

        public static event Action<LogType, string> MessageLogged;

        private static string prefix = $"[{SharedConstructs.Name}]: ";

        public static void Error(string message)
        {
            MessageLogged?.Invoke(LogType.Error, message);
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(prefix + message);
            Console.ForegroundColor = originalColor;
        }

        public static void Warning(string message)
        {
            MessageLogged?.Invoke(LogType.Warning, message);
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(prefix + message);
            Console.ForegroundColor = originalColor;
        }

        public static void Info(string message)
        {
            MessageLogged?.Invoke(LogType.Info, message);
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(prefix + message);
            Console.ForegroundColor = originalColor;
        }

        public static void Success(string message)
        {
            MessageLogged?.Invoke(LogType.Success, message);
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(prefix + message);
            Console.ForegroundColor = originalColor;
        }

        public static void Debug(string message)
        {
#if DEBUG
            MessageLogged?.Invoke(LogType.Debug, message);
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(prefix + message);
            Console.ForegroundColor = originalColor;
#endif
        }
    }
}
