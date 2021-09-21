using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using static TournamentAssistantShared.GlobalConstants;

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

        public enum InstanceType
        {
            Server,
            Client,
            Plugin
        }

        //Enable communication between pluginlogger class and shared.
        public static event Action<object, LogType> PluginLog;
        public static event Action<LogType, string> LogMessage;
        public Thread LoggerThread { get; private set; }

        private readonly BlockingCollection<LogObject> JobQueue = new();

        private readonly StreamWriter Writer;
        
        public Logger(InstanceType instanceType)
        {
            string logFolderPath = "";
            bool redirectToPlugin = false;
            switch (instanceType)
            {
                case InstanceType.Server:
                    logFolderPath = ServerDataLogs;
                    break;
                case InstanceType.Client:
                    logFolderPath = AppDataLogs;
                    break;
                case InstanceType.Plugin:
                    redirectToPlugin = true;
                    break;
                default:
                    break;
            }

            if (!redirectToPlugin)
            {
                if (!Directory.Exists(logFolderPath)) Directory.CreateDirectory(logFolderPath);
                Writer = new($"{logFolderPath}{DateTime.Now.ToFileTime()}.txt", true);

                Thread thread = new(new ThreadStart(Log))
                {
                    IsBackground = true
                };
                thread.Start();
                LoggerThread = thread;

                LogMessage += Logger_LogMessage;
            }
            else
            {
                LogMessage += Logger_RedirectMessage;
            }
        }

        private void Logger_RedirectMessage(LogType type, string message)
        {
            PluginLog?.Invoke(message, type);
        }

        private void Logger_LogMessage(LogType type, string message)
        {
            JobQueue.Add(new LogObject(message, type));
        }

        private void Log()
        {
            try
            {
                foreach (var LogJob in JobQueue.GetConsumingEnumerable())
                {
                    var previousColor = Console.ForegroundColor;
                    Console.ForegroundColor = LogJob.Color;
                    Console.WriteLine(LogJob.Message);
                    Writer.WriteLine($"[{LogJob.Type}]: {LogJob.Message}");
                    Console.ForegroundColor = previousColor;
                }
            }
            catch (ThreadAbortException)
            {
                var previousColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("Logger closing....");
                Writer.WriteLine($"[Info]: Logger closing....");
                Console.ForegroundColor = previousColor;

                Writer.Close();
            }
        }

        #region StaticImplementation
        [DebuggerStepThrough]
        public static void Error(object message)
        {
            message = $"[{NameOfCallingClass()}]: {message}";
            LogMessage?.Invoke(LogType.Error, message.ToString());
        }
        [DebuggerStepThrough]
        public static void Warning(object message)
        {
            message = $"[{NameOfCallingClass()}]: {message}";
            LogMessage?.Invoke(LogType.Warning, message.ToString());
        }
        [DebuggerStepThrough]
        public static void Info(object message)
        {
            message = $"[{NameOfCallingClass()}]: {message}";
            LogMessage?.Invoke(LogType.Info, message.ToString());
        }
        [DebuggerStepThrough]
        public static void Success(object message)
        {
            message = $"[{NameOfCallingClass()}]: {message}";
            LogMessage?.Invoke(LogType.Success, message.ToString());
        }
        [DebuggerStepThrough]
        public static void Debug(object message)
        {
            message = $"[{NameOfCallingClass()}]: {message}";
            LogMessage?.Invoke(LogType.Debug, message.ToString());
        } 
        #endregion

        //Stolen from StackOverflow
        //Also edited, but mostly stolen
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

            var className = fullName.Replace("TournamentAssistant", string.Empty);
#if !DEBUG
            if (className.Length > 30) className = className.Substring(0, 30) + "...";
#endif
            return className;
        }


        private class LogObject
        {
            public object Message { get; private set; }
            public LogType Type { get; private set; }
            public ConsoleColor Color { get; private set; }
            public LogObject(object message, LogType type)
            {
                Message = message;
                Type = type;
                switch (type)
                {
                    case LogType.Error:
                        Color = ConsoleColor.DarkRed;
                        break;
                    case LogType.Warning:
                        Color = ConsoleColor.DarkYellow;
                        break;
                    case LogType.Info:
                        Color = ConsoleColor.White;
                        break;
                    case LogType.Success:
                        Color = ConsoleColor.Green;
                        break;
                    case LogType.Debug:
                        Color = ConsoleColor.Cyan;
                        break;
                    default:
                        break;
                }
            }

        }
    }
}
