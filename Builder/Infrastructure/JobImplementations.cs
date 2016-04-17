using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using RSCoreLib;
using RSCoreLib.OS;
using RSCoreLib.WPF;

namespace Builder
    {
    public static class JobImplementations
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(JobImplementations));

        internal class BuildInfo
            {
            internal int PartCount = -1;
            }

        internal static OperationResult Build (CancellationToken cancellationToken, ProgressViewModel progress, ConfigurationVM configurationVM)
            {
            var shell = ShellHelper.SetupEnv(configurationVM?.Parent?.SrcPath, configurationVM?.OutPath, configurationVM?.BuildStrategy, "hidden build env");
            var vm = new HistoryEventVM(configurationVM)
                {
                Command = "bb b",
                JobName = "Build",
                };
            var context = new JobContext<BuildInfo>(shell, cancellationToken, progress, vm);
            return Execute(context, ProcessBuildOutput);
            }

        private static void ProcessBuildOutput (JobContext<BuildInfo> context, ShellOutput o)
            {
            string l = o.Data;
            if (l.StartsWith("Starting part"))
                {
                if (context.Information.PartCount < 0)
                    {
                    var match = Regex.Match(l, @"^Starting\spart\s\(\d+\/(?<PartCount>\d+)\)");
                    int count;
                    if (match.Success && int.TryParse(match.Groups["PartCount"].Value, out count) && count > 0)
                        {
                        context.Information.PartCount = count;
                        context.Progress.ShortStatus = count.ToString();
                        }
                    }

                return;
                }

            if (l.StartsWith("Running parts"))
                {
                if (context.Information.PartCount <= 0)
                    return;

                var match = Regex.Match(l, @"^Running\sparts\s\((?<PartCount>\d+)\sremain");
                int count;
                if (match.Success && int.TryParse(match.Groups["PartCount"].Value, out count) && count > 0)
                    {
                    var percent = 1.0 - ((double)count / context.Information.PartCount);
                    context.Progress.Value = (int)(percent * 100);
                    context.Progress.ShortStatus = count.ToString();
                    }

                return;
                }

            lock (context.Log)
                {
                context.Log.WriteLine(l);
                }
            }

        internal static OperationResult Clean (CancellationToken cancellationToken, ProgressViewModel progress, ConfigurationVM configurationVM)
            {
            var shell = ShellHelper.SetupEnv(configurationVM?.Parent?.SrcPath, configurationVM?.OutPath, configurationVM?.BuildStrategy, "hidden build env");
            var vm = new HistoryEventVM(configurationVM)
                {
                Command = "bb b --tmr --noprompt",
                JobName = "Rebuild"
                };

            var context = new JobContext<BuildInfo>(shell, cancellationToken, progress, vm);
            return Execute(context, (c, o) => c.Log.WriteLine(o.Data));
            }

        internal static OperationResult Rebuild (CancellationToken cancellationToken, ProgressViewModel progress, ConfigurationVM configurationVM)
            {
            var shell = ShellHelper.SetupEnv(configurationVM?.Parent?.SrcPath, configurationVM?.OutPath, configurationVM?.BuildStrategy, "hidden build env");
            var vm = new HistoryEventVM(configurationVM)
                {
                Command = "bb b --tmrbuild --noprompt",
                JobName = "Clean"
                };

            var context = new JobContext<BuildInfo>(shell, cancellationToken, progress, vm);
            return Execute(context, ProcessBuildOutput);
            }

        internal class BootstrapInfo
            {
            internal int Counter = 0;
            }

        internal static OperationResult Bootstrap (CancellationToken cancellationToken, ProgressViewModel progress, string path, string stream)
            {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path) || string.IsNullOrEmpty(stream))
                return OperationResult.Failed;

            var shell = new CommandLineSandbox(path);
            var vm = new HistoryEventVM()
                {
                Command = "bentleybootstrap.py " + stream,
                JobName = "Bootstrap",

                Stream = stream,
                SrcDir = path
                };

            var context = new JobContext<BootstrapInfo>(shell, cancellationToken, progress, vm);
            return Execute(context, ProcessBootstrapOutput);
            }

        private static void ProcessBootstrapOutput (JobContext<BootstrapInfo> context, ShellOutput o)
            {
            string line = o.Data;
            if (string.IsNullOrEmpty(line))
                return;

            if (line.StartsWith("Found team:") && line.Length >= 13)
                {
                string team = line.Substring(11).Trim();
                if (!string.IsNullOrEmpty(team))
                    context.Progress.StatusMessage = "Bootstrapping team " + team;
                }
            else if (line.StartsWith("Cloning"))
                {
                context.Information.Counter += 1;
                context.Progress.ShortStatus = context.Information.Counter.ToString();
                }

            context.Log.WriteLine(o.Data);
            }

        #region Execution
        internal class JobContext<T> : IDisposable where T : new()
            {
            internal StreamWriter Log { get; }
            internal SQLiteConnection DbConnection { get; }
            internal ProgressViewModel Progress { get; }
            internal CancellationToken CancellationToken { get; }
            internal T Information { get; }
            internal HistoryEventVM HistoryVM { get; }
            internal CommandLineSandbox Shell { get; }

            internal JobContext (CommandLineSandbox shell, CancellationToken cancellationToken, ProgressViewModel progress, HistoryEventVM eventVM)
                {
                Shell = shell;
                Progress = progress;
                CancellationToken = cancellationToken;
                Information = new T();
                DbConnection = AppDataManager.ConnectToSqlite();

                try
                    {
                    HistoryVM = eventVM;
                    eventVM.Insert(DbConnection);
                    if (!HistoryVM.ID.HasValue)
                        throw new InvalidOperationException("No ID obtained for Job from DB.");

                    string logPath = AppDataManager.GetAppDataPath(Path.Combine("logs", StringEncoder.ToString(HistoryVM.ID.Value, StringEncoder.BASE36CHARS) + ".txt"));
                    var dir = Path.GetDirectoryName(logPath);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    Log = new StreamWriter(logPath, false);
                    }
                catch (Exception e)
                    {
                    log.ErrorFormat($"Exception while trying to setup context for command '{eventVM.Command}': {e.Message}");
                    DbConnection.Dispose();
                    throw;
                    }
                }

            public void Dispose ()
                {
                Log.Dispose();
                DbConnection.Dispose();
                }
            }

        internal static OperationResult Execute<T> (JobContext<T> context, Action<JobContext<T>, ShellOutput> lineProcessor) where T : new()
            {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
                {
                using (context)
                    {
                    context.Log.WriteLine($"Logfile for Job {context.HistoryVM.JobName}, ID {context.HistoryVM.ID}, Started: {DateTime.Now.ToLongDateString()} {DateTime.Now.ToLongTimeString()}");
                    context.CancellationToken.Register(() => context.Shell.Dispose(true));
                    context.CancellationToken.ThrowIfCancellationRequested();
                    context.Shell.ExecuteCommand(SET_UNBUFFERED_COMMAND);
                    context.Shell.OutputHandler = o => lineProcessor(context, o);
                    var result = context.Shell.ExecuteCommand(context.HistoryVM.Command).Success ? OperationResult.Success : OperationResult.Failed;
                    stopwatch.Stop();
                    context.HistoryVM.Update(context.DbConnection, result == OperationResult.Success ? HistoryEventResult.Success : HistoryEventResult.Failed, stopwatch.Elapsed.TotalSeconds);
                    return result;
                    }
                }
            catch (Exception e)
                {
                if (context.CancellationToken.IsCancellationRequested)
                    {
                    context.HistoryVM.Update(context.DbConnection, HistoryEventResult.Cancelled, stopwatch.Elapsed.TotalSeconds);
                    return OperationResult.Aborted;
                    }

                log.ErrorFormat("Exception during command {0}:{1}", context.HistoryVM.Command, e.Message);
                throw;
                }
            }

        const string SET_UNBUFFERED_COMMAND = "SET PYTHONUNBUFFERED=1";
        #endregion
        }
    }
