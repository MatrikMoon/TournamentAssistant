using IPA.Logging;
using LogType = TournamentAssistantShared.Logger.LogType;

namespace TournamentAssistant
{
    class PluginLogger
    {
        public static Logger logger { get; set; }
        public static void Log(object message, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    logger.Error(message.ToString());
                    break;
                case LogType.Warning:
                    logger.Warn(message.ToString());
                    break;
                case LogType.Info:
                    logger.Info(message.ToString());
                    break;
                case LogType.Success:
                    logger.Info(message.ToString()); //IPA logger does not have success, so log it into Info
                    break;
                case LogType.Debug:
                    logger.Info(message.ToString()); //Not using debug because it includes complete call path, which is unnessesary to include (also on release wont be logged anyway)
                    break;
                default:
                    logger.Info(message.ToString());
                    break;
            }
        }
    }
}
