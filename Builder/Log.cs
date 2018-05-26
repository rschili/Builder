using log4net;

namespace RSCoreLib
    {
    /// <summary>
    /// Recently switched to log4net, so this class is merely a convenience and compatibility wrapper.
    /// </summary>
    public static class Log
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(Log));

        public static void Information (string message)
            {
            log.Info(message);
            }

        public static void Information (string format, params object[] args)
            {
            log.InfoFormat(format, args);
            }

        public static void Warning (string message)
            {
            log.Warn(message);
            }

        public static void Warning (string format, params object[] args)
            {
            log.WarnFormat(format, args);
            }

        public static void Error (string message)
            {
            log.Error(message);
            }

        public static void Error (string format, params object[] args)
            {
            log.ErrorFormat(format);
            }
        }
    }
