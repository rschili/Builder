using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
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
            Entries.Add(new HistoryEventVM() { ID = 1, JobName = "Rebuild", Result = HistoryEventResult.Failed });
            Entries.Add(new HistoryEventVM() { ID = 2, JobName = "Rebuild", Result = HistoryEventResult.Failed });
            Entries.Add(new HistoryEventVM() { ID = 3, JobName = "Build", Result = HistoryEventResult.Failed });
            Entries.Add(new HistoryEventVM() { ID = 42, JobName = "Bootstrap", Result = HistoryEventResult.Success });
            }
        
        }

    public enum HistoryEventResult
        {
        Unknown = 0,
        Success = 1,
        Cancelled = 2,
        Failed = 3
        }

    public class HistoryEventVM
        {
        public HistoryEventVM ()
            {
            }

        public HistoryEventVM (ConfigurationVM configurationVM)
            {
            BuildStrategy = configurationVM.BuildStrategy;
            Stream = configurationVM?.Parent?.Stream;
            SrcDir = configurationVM?.Parent?.SrcPath;
            OutDir = configurationVM?.OutPath;
            Release = configurationVM.Release;
            Platform = "x64";
            }

        public long? ID { get; set; } = null;
        public string Command { get; set; }
        public string JobName { get; set; }
        public DateTime StartTime { get; set; }
        public string BuildStrategy { get; set; }
        public string Stream { get; set; }
        public string SrcDir { get; set; }
        public string OutDir { get; set; }
        public bool Release { get; set; }
        public string Platform { get; set; }
        public HistoryEventResult Result { get; set; }
        public double SecondsDuration { get; set; }

        internal void Insert (SQLiteConnection connection)
            {
            using (var command = new SQLiteCommand(connection))
                {
                command.CommandText = $"INSERT INTO {AppDataManager.HISTORY_TABLENAME}"+
                    "(command,jobName,startTime,resultCode,buildStrategy,stream,sourceDir,outDir,release, platform) "+
                    "VALUES(@pCmd,@pJob, datetime('now'),0,@buildStrat,@stream,@sourceDir,@outDir,@release,@platform)";
                command.Parameters.AddWithValue("pCmd", Command);
                command.Parameters.AddWithValue("pJob", JobName);

                command.Parameters.AddWithValue("buildStrat", BuildStrategy);
                command.Parameters.AddWithValue("stream", Stream);
                command.Parameters.AddWithValue("sourceDir", SrcDir);
                command.Parameters.AddWithValue("outDir", OutDir);
                command.Parameters.AddWithValue("release", Release);
                command.Parameters.AddWithValue("platform", Platform);
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
        }
    }
