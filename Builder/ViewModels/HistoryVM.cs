using System.Collections.ObjectModel;
using log4net;
using RSCoreLib.WPF;

namespace Builder
    {
    public class HistoryVM : ViewModelBase
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(HistoryVM));
        private readonly MainVM Parent;

        public ObservableCollection<HistoryEventVM> Entries { get; } = new ObservableCollection<HistoryEventVM>();

        public HistoryVM (MainVM parent)
            {
            Parent = parent;
            }

        public HistoryVM ()
            {
            Entries.Add(new HistoryEventVM() { ID = 1, Operation="Rebuild", Result = "Failed" });
            Entries.Add(new HistoryEventVM() { ID = 2, Operation = "Rebuild", Result = "Success" });
            Entries.Add(new HistoryEventVM() { ID = 3, Operation = "Build", Result = "Unknown" });
            Entries.Add(new HistoryEventVM() { ID = 42, Operation = "Bootstrap", Result = "Failed" });
            }
        
        }

    public class HistoryEventVM
        {
        public int ID { get; set; }
        public string Operation { get; set; }
        public string Result { get; set; }
        }
    }
