using log4net;
using RSCoreLib.WPF;

namespace Builder
    {
    public class OperationLogVM : ViewModelBase
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(OperationLogVM));
        private readonly MainVM Parent;

        public OperationLogVM (MainVM parent)
            {
            Parent = parent;
            }

        public string DateTime => "2016-54-45 12:45:34";
        public string Strategy => "buildstrat";
        public string Branch => "**branch**";
        }
    }
