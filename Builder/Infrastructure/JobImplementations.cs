using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using RSCoreLib.OS;
using RSCoreLib.WPF;

namespace Builder
    {
    public static class JobImplementations
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(JobImplementations));

        internal class JobContext<T> : IDisposable
            {
            internal StreamWriter Log { get; }
            internal ProgressViewModel Progress { get; }
            internal CancellationToken CancellationToken { get; }
            internal string Command { get; }
            internal T Information { get; }

            internal JobContext (CancellationToken cancellationToken, ProgressViewModel progress, string fileName, string command, T info)
                {
                Log = new StreamWriter(AppDataManager.GetAppDataPath(fileName + ".log"), false);
                Progress = progress;
                CancellationToken = cancellationToken;
                Command = command;
                Information = info;
                }

            public void Dispose ()
                {
                Log.Dispose();
                }
            }

        internal static OperationResult Execute<T> (CommandLineSandbox shell, JobContext<T> context, T result, Action<JobContext<T>, ShellOutput> lineProcessor)
            {
            try
                {
                using (shell)
                using (context)
                    {
                    context.CancellationToken.Register(() => shell.Dispose(true));
                    context.CancellationToken.ThrowIfCancellationRequested();
                    shell.ExecuteCommand(SET_UNBUFFERED_COMMAND);
                    shell.OutputHandler = o => lineProcessor(context, o);
                    return shell.ExecuteCommand(context.Command).Success ? OperationResult.Success : OperationResult.Failed;
                    }
                }
            catch (Exception e)
                {
                if (context.CancellationToken.IsCancellationRequested)
                    return OperationResult.Aborted;

                log.ErrorFormat("Exception during command {0}:{1}", context.Command, e.Message);
                throw;
                }
            }

        internal static OperationResult Bootstrap (CancellationToken cancellationToken, ProgressViewModel progress, string path, string stream)
            {
            int counter = 0;
            try
                {
                using (CommandLineSandbox cmd = new CommandLineSandbox(path))
                using (StreamWriter sw = new StreamWriter(AppDataManager.GetAppDataPath("Bootstrap.txt"), false))
                    {
                    cancellationToken.Register(() => cmd.Dispose(true));
                    cancellationToken.ThrowIfCancellationRequested();
                    cmd.ExecuteCommand(SET_UNBUFFERED_COMMAND);
                    cmd.OutputHandler = o => ProcessBootstrapLine(o, sw, progress, ref counter);
                    var result = cmd.ExecuteCommand("bentleybootstrap.py " + stream).Success;
                    return result ? OperationResult.Success : OperationResult.Failed;
                    }
                }
            catch(Exception e)
                {
                if (cancellationToken.IsCancellationRequested)
                    return OperationResult.Aborted;

                log.ErrorFormat("Exception while bootstrapping {0}:{1}: {2}", path, stream, e.Message);
                throw;
                }
            }

        private static void ProcessBootstrapLine (ShellOutput o, StreamWriter sw, ProgressViewModel progress, ref int counter)
            {
            string line = o.Data;
            if (string.IsNullOrEmpty(line))
                return;

            if(line.StartsWith("Found team:") && line.Length >= 13)
                {
                string team = line.Substring(11).Trim();
                if (!string.IsNullOrEmpty(team))
                    progress.StatusMessage = "Bootstrapping team " + team;
                }
            else if(line.StartsWith("Cloning"))
                {
                counter++;
                progress.ShortStatus = counter.ToString();
                }

            sw.WriteLine(o.Data);
            }

        const string SET_UNBUFFERED_COMMAND = "SET PYTHONUNBUFFERED=1";
        internal static OperationResult Build (CancellationToken cancellationToken, ProgressViewModel progress, string srcPath, string outPath, string buildStrategy)
            {
            int partCount = -1;
            int partsHandled = 0;
            using (CommandLineSandbox cmd = ShellHelper.SetupEnv(srcPath, outPath, buildStrategy, "hidden build env"))
            using (StreamWriter sw = new StreamWriter(AppDataManager.GetAppDataPath("Buildlog.txt"), false))
                {
                cancellationToken.Register(() => cmd.Dispose(true));
                cancellationToken.ThrowIfCancellationRequested();
                cmd.ExecuteCommand(SET_UNBUFFERED_COMMAND);
                cmd.OutputHandler = o => ProcessLine(o, sw, progress, ref partCount, ref partsHandled);
                var result = cmd.ExecuteCommand("bb b").Success;
                return result ? OperationResult.Success : OperationResult.Failed;
                }
            }


        private static void ProcessLine (ShellOutput o, StreamWriter sw, ProgressViewModel progress, ref int partCount, ref int partsHandled)
            {
            string l = o.Data;
            if (l.StartsWith("Starting part"))
                {
                if (partCount < 0)
                    {
                    var match = Regex.Match(l, @"^Starting\spart\s\(\d+\/(?<PartCount>\d+)\)");
                    int count;
                    if (match.Success && int.TryParse(match.Groups["PartCount"].Value, out count) && count > 0)
                        {
                        partCount = count;
                        progress.ShortStatus = partCount.ToString();
                        }
                    }

                return;
                }

            if (l.StartsWith("Running parts"))
                {
                if (partCount <= 0)
                    return;

                var match = Regex.Match(l, @"^Running\sparts\s\((?<PartCount>\d+)\sremain");
                int count;
                if (match.Success && int.TryParse(match.Groups["PartCount"].Value, out count) && count > 0)
                    {
                    var percent = 1.0 - ((double)count / partCount);
                    progress.Value = (int)(percent * 100);
                    progress.ShortStatus = count.ToString();
                    }

                return;
                }

            lock (sw)
                {
                sw.WriteLine(l);
                }
            }

        }
    }
