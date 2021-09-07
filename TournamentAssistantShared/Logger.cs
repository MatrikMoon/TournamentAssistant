using SiraUtil.Tools;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using static TournamentAssistantShared.GlobalConstants;

/**
 * Modified by Moon on ?/?/2018 (Originally taken from andruzzzhka's work)
 * Simple wrapper for Console.Log which makes logging
 * a little prettier
 */

namespace TournamentAssistantShared
{
    public class Logger
    {
        private static object LockObject = new object();

        private static string[] LogFiles = 
        {
            "Error.txt",
            "Warning.txt",
            "Info.txt",
            "Success.txt",
            "Debug.txt",
            "All.txt"
        };

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

        public static void LoggerFileInit()
        {
            if (LogAllToFile)
            {
                string path;
                if (IsServer) path = ServerDataLogs;
                else path = AppDataLogs;

                foreach (var fileName in LogFiles)
                {
                    if (!File.Exists($"{path}{fileName}")) File.Create($"{path}{fileName}");
                }
            }
        }

        public static void ArchiveLogs()
        {
            string path;
            if (IsServer) path = ServerDataLogs;
            else path = AppDataLogs;

            string archivePath = $"{path}";
            string folderName = $"{DateTime.UtcNow}";

            foreach (char illegalChar in IllegalPathCharacters)
                folderName = folderName.Replace(illegalChar, '_');

            archivePath += folderName;

            Directory.CreateDirectory($"{archivePath}");

            foreach (var fileName in LogFiles)
            {
                File.Move($"{path}{fileName}", $"{archivePath}{fileName}");
            }
        }

        public static void Error(object message)
        {
            MessageLogged?.Invoke(LogType.Error, message.ToString());
            LogString(message, LogType.Error);
        }

        public static void Warning(object message)
        {
            MessageLogged?.Invoke(LogType.Warning, message.ToString());
            LogString(message, LogType.Warning);
        }

        public static void Info(object message)
        {
            MessageLogged?.Invoke(LogType.Info, message.ToString());
            LogString(message, LogType.Info);
        }

        public static void Success(object message)
        {
            MessageLogged?.Invoke(LogType.Success, message.ToString());
            LogString(message, LogType.Success);
        }

        public static void Debug(object message)
        {
#if DEBUG
            MessageLogged?.Invoke(LogType.Debug, message.ToString());
            LogString(message, LogType.Debug);
#endif
        }

        public static void ColoredLog(object message, ConsoleColor color, LogType type)
        {
            MessageLogged?.Invoke(type, message.ToString());
            LogString(message, type, color);
        }

        private static void LogString(object message, LogType type, ConsoleColor color = default)
        {
            message = $"[{NameOfCallingClass().Replace("TournamentAssistant", string.Empty)}]: {message}";

            //Prevent two threads writing to console at the same time (and to a file too)
            lock (LockObject)
            {
                if (IsPlugin)
                {
                    SiraLog siraLog = new();
                    switch (type)
                    {
                        case LogType.Error:
                            siraLog.Error(message);
                            break;
                        case LogType.Warning:
                            siraLog.Warning(message);
                            break;
                        case LogType.Info:
                            siraLog.Info(message);
                            break;
                        case LogType.Success:
                            siraLog.Debug(message); //Sira log does not have success, so log it into debug
                            break;
                        case LogType.Debug:
                            siraLog.Debug(message);
                            break;
                        default:
                            break;
                    }

                    return;
                }

                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ForegroundColor = default;
                if (LogAllToFile)
                {
                    string path;
                    if (IsServer) path = ServerDataLogs;
                    else path = AppDataLogs;

                    StreamWriter writer = new($"{path}All.txt", true);
                    StreamWriter errorWriter = new($"{path}Error.txt", true);
                    StreamWriter warningWriter = new($"{path}Warning.txt", true);
                    StreamWriter infoWriter = new($"{path}Info.txt", true);
                    StreamWriter successWriter = new($"{path}Success.txt", true);
                    StreamWriter debugWriter = new($"{path}Debug.txt", true);

                    switch (type)
                    {
                        case LogType.Error:
                            errorWriter.WriteLineAsync($"[{type}]: {message}");
                            writer.WriteLineAsync($"[{type}]: {message}");
                            break;
                        case LogType.Warning:
                            warningWriter.WriteLineAsync($"[{type}]: {message}");
                            writer.WriteLineAsync($"[{type}]: {message}");
                            break;
                        case LogType.Info:
                            infoWriter.WriteLineAsync($"[{type}]: {message}");
                            writer.WriteLineAsync($"[{type}]: {message}");
                            break;
                        case LogType.Success:
                            successWriter.WriteLineAsync($"[{type}]: {message}");
                            writer.WriteLineAsync($"[{type}]: {message}");
                            break;
                        case LogType.Debug:
                            debugWriter.WriteLineAsync($"[{type}]: {message}");
                            writer.WriteLineAsync($"[{type}]: {message}");
                            break;
                        default:
                            writer.WriteLineAsync($"[{type}]: {message}");
                            break;
                    }

                    writer.Close();
                    errorWriter.Close();
                    warningWriter.Close();
                    infoWriter.Close();
                    successWriter.Close();
                    debugWriter.Close();
                }
            }
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
