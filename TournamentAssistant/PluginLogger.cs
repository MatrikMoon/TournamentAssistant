using IPA.Logging;
using LogType = TournamentAssistantShared.Logger.LogType;

namespace TournamentAssistant
{
    class PluginLogger
    {
        private Logger _logger { get; set; }
        public PluginLogger(Logger logger)
        {
            _logger = logger;
        }

        public void Log(object message, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    _logger.Error(message.ToString());
                    break;
                case LogType.Warning:
                    _logger.Warn(message.ToString());
                    break;
                case LogType.Info:
                    _logger.Info(message.ToString());
                    break;
                case LogType.Success:
                    _logger.Info(message.ToString()); //IPA logger does not have success, so log it into Info
                    break;
                case LogType.Debug:
                    _logger.Info(message.ToString()); //Not using debug because it includes complete call path, which is unnessesary to include (also on release wont be logged anyway)
                    break;
                default:
                    _logger.Info(message.ToString());
                    break;
            }
        }
    }
}
