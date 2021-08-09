using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

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
            message = $"[{NameOfCallingClass().Replace("TournamentAssistant", string.Empty)}]: {message}";
            MessageLogged?.Invoke(LogType.Error, message.ToString());
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }

        public static void Warning(object message)
        {
            message = $"[{NameOfCallingClass().Replace("TournamentAssistant", string.Empty)}]: {message}";
            MessageLogged?.Invoke(LogType.Warning, message.ToString());
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }

        public static void Info(object message)
        {
            message = $"[{NameOfCallingClass().Replace("TournamentAssistant", string.Empty)}]: {message}";
            MessageLogged?.Invoke(LogType.Info, message.ToString());
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }

        public static void Success(object message)
        {
            message = $"[{NameOfCallingClass().Replace("TournamentAssistant", string.Empty)}]: {message}";
            MessageLogged?.Invoke(LogType.Success, message.ToString());
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }

        public static void Debug(object message)
        {
#if DEBUG
            message = $"[{NameOfCallingClass().Replace("TournamentAssistant", string.Empty)}]: {message}";
            MessageLogged?.Invoke(LogType.Debug, message.ToString());
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
#endif
        }

        public static void ColoredLog(object message, ConsoleColor color)
        {
            message = $"[{NameOfCallingClass().Replace("TournamentAssistant", string.Empty)}]: {message}";
            MessageLogged?.Invoke(LogType.Info, message.ToString());
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }

        public static string NameOfCallingClass()
        {
            string fullName;
            Type declaringType;
            int skipFrames = 2;
            do
            {
                MethodBase method = new StackFrame(skipFrames, false).GetMethod();
                declaringType = method.DeclaringType;
                if (declaringType == null)
                {
                    return method.Name;
                }
                skipFrames++;
                fullName = declaringType.FullName;
            }
            while (declaringType.Module.Name.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase));

            return fullName;
        }
    }
}
