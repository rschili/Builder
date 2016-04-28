using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using log4net;
using RSCoreLib.WPF;

namespace Builder
    {
    public class HistoryVM : ViewModelBase
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(HistoryVM));
        public MainVM Parent { get; }

        public ObservableCollection<HistoryEventVM> Entries { get; } = new ObservableCollection<HistoryEventVM>();

        public HistoryVM (MainVM parent)
            {
            Parent = parent;
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Entries, Entries);
            }

        public HistoryVM ()
            {
            Entries.Add(new HistoryEventVM(this) { ID = 1, JobName = "Rebuild", Result = HistoryEventResult.Failed });
            Entries.Add(new HistoryEventVM(this) { ID = 2, JobName = "Rebuild", Result = HistoryEventResult.Failed });
            Entries.Add(new HistoryEventVM(this) { ID = 3, JobName = "Build", Result = HistoryEventResult.Failed });
            Entries.Add(new HistoryEventVM(this) { ID = 42, JobName = "Bootstrap", Result = HistoryEventResult.Success });
            }
        
        public HistoryEventVM CreateHistoryEvent()
            {
            var vm = new HistoryEventVM(this);
            lock (Entries)
                Entries.Insert(0, vm);

            return vm;
            }
        }

    public enum HistoryEventResult
        {
        Unknown = 0,
        Success = 1,
        Cancelled = 2,
        Failed = 3
        }

    public class HistoryEventVM : ViewModelBase
        {
        public HistoryVM Parent { get; private set; }
        public HistoryEventVM (HistoryVM parent)
            {
            Parent = parent;
            WireupCommands();
            }

        public void InitializeWith (ConfigurationVM configurationVM)
            {
            BuildStrategy = configurationVM.BuildStrategy;
            Stream = configurationVM?.Parent?.Stream;
            SrcDir = configurationVM?.Parent?.SrcPath;
            OutDir = configurationVM?.OutPath;
            Release = configurationVM.Release;
            Platform = "x64";
            }

        #region Properties
        public long? _id = null;
        public long? ID
            {
            get { return _id; }
            set
                {
                if (_id == value)
                    return;

                _id = value;
                OnPropertyChanged();
                }
            }

        private string _command;
        public string Command
            {
            get { return _command; }
            set
                {
                if (_command == value)
                    return;

                _command = value;
                OnPropertyChanged();
                }
            }

        private string _jobName;
        public string JobName
            {
            get { return _jobName; }
            set
                {
                if (_jobName == value)
                    return;

                _jobName = value;
                OnPropertyChanged();
                }
            }

        private DateTime _startTime;
        public DateTime StartTime
            {
            get { return _startTime; }
            set
                {
                if (_startTime == value)
                    return;

                _startTime = value;
                OnPropertyChanged();
                }
            }

        private string _buildStrategy;
        public string BuildStrategy
            {
            get { return _buildStrategy; }
            set
                {
                if (_buildStrategy == value)
                    return;

                _buildStrategy = value;
                OnPropertyChanged();
                }
            }

        private string _stream;
        public string Stream
            {
            get { return _stream; }
            set
                {
                if (_stream == value)
                    return;

                _stream = value;
                OnPropertyChanged();
                }
            }

        private string _srcDir;
        public string SrcDir
            {
            get { return _srcDir; }
            set
                {
                if (_srcDir == value)
                    return;

                _srcDir = value;
                OnPropertyChanged();
                }
            }

        private string _outDir;
        public string OutDir
            {
            get { return _outDir; }
            set
                {
                if (_outDir == value)
                    return;

                _outDir = value;
                OnPropertyChanged();
                }
            }

        private bool _release;
        public bool Release
            {
            get { return _release; }
            set
                {
                if (_release == value)
                    return;

                _release = value;
                OnPropertyChanged();
                }
            }

        private string _platform;
        public string Platform
            {
            get { return _platform; }
            set
                {
                if (_platform == value)
                    return;

                _platform = value;
                OnPropertyChanged();
                }
            }

        private string _part;
        public string Part
            {
            get { return _part; }
            set
                {
                if (_part == value)
                    return;

                _part = value;
                OnPropertyChanged();
                }
            }

        private HistoryEventResult _result;
        public HistoryEventResult Result
            {
            get { return _result; }
            set
                {
                if (_result == value)
                    return;

                _result = value;
                OnPropertyChanged();
                }
            }

        private double _secondsDuration;
        public double SecondsDuration
            {
            get { return _secondsDuration; }
            set
                {
                if (_secondsDuration == value)
                    return;

                _secondsDuration = value;
                OnPropertyChanged();
                }
            }
        #endregion

        internal void Insert (SQLiteConnection connection)
            {
            StartTime = DateTime.Now;
            using (var command = new SQLiteCommand(connection))
                {
                command.CommandText = $"INSERT INTO {AppDataManager.HISTORY_TABLENAME}"+
                    "(command,jobName,startTime,resultCode,buildStrategy,stream,sourceDir,outDir,release, platform, part) "+
                    "VALUES(@pCmd,@pJob, datetime('now'),0,@buildStrat,@stream,@sourceDir,@outDir,@release,@platform,@part)";
                command.Parameters.AddWithValue("pCmd", Command);
                command.Parameters.AddWithValue("pJob", JobName);

                command.Parameters.AddWithValue("buildStrat", BuildStrategy);
                command.Parameters.AddWithValue("stream", Stream);
                command.Parameters.AddWithValue("sourceDir", SrcDir);
                command.Parameters.AddWithValue("outDir", OutDir);
                command.Parameters.AddWithValue("release", Release);
                command.Parameters.AddWithValue("platform", Platform);
                command.Parameters.AddWithValue("part", Part);
                command.ExecuteNonQuery();
                var id = connection.LastInsertRowId;
                ID = id;
                }
            }

        internal void Update (SQLiteConnection connection, HistoryEventResult result, double secondsDuration)
            {
            if (!ID.HasValue)
                throw new InvalidOperationException("This VM does not have an ID assigned yet.");

            SecondsDuration = secondsDuration;
            Result = result;
            using (var command = new SQLiteCommand(connection))
                {
                command.CommandText = $"UPDATE {AppDataManager.HISTORY_TABLENAME} SET secondsDuration=@pSec, resultCode=@pResult WHERE id=@pId";
                command.Parameters.AddWithValue("pSec", secondsDuration);
                command.Parameters.AddWithValue("pResult", (int)Result);
                command.Parameters.AddWithValue("pId", ID.Value);
                command.ExecuteNonQuery();
                }
            }

        #region Commands
        private void WireupCommands ()
            {
            NavigateToCommand.Handler = NavigateTo;
            }

        public SimpleCommand NavigateToCommand { get; } = new SimpleCommand();
        private void NavigateTo (object obj)
            {
            Task.Run(() =>
            {
                if (!ID.HasValue)
                    return;

                var fileName = AppDataManager.GetLogFilePath(ID.Value);
                if (!File.Exists(fileName))
                    return;

                Process.Start(fileName);
            });
            }
        #endregion
        }
    }
