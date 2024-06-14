using System;

/**
 * Modified by Moon on ?/?/2018 (Originally taken from andruzzzhka's work)
 * Simple wrapper for Console.Log which makes logging
 * a little prettier
 */

namespace TournamentAssistantShared
{
    public class Logger
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

        public static void Error(object message)
        {
            ColoredLog(message, ConsoleColor.Red);
        }

        public static void Warning(object message)
        {
            ColoredLog(message, ConsoleColor.Yellow);
        }

        public static void Info(object message)
        {
            ColoredLog(message, ConsoleColor.White);
        }

        public static void Success(object message)
        {
            ColoredLog(message, ConsoleColor.Green);
        }

        public static void Debug(object message)
        {
#if DEBUG
            ColoredLog(message, ConsoleColor.Blue);
#endif
        }

        public static void ColoredLog(object message, ConsoleColor color)
        {
            MessageLogged?.Invoke(LogType.Info, message.ToString());
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }
    }
}
