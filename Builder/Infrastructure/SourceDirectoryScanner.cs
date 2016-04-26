using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using RSCoreLib;
using RSCoreLib.WPF;

namespace Builder
    {
    public class BatchFile
        {
        public IDictionary<string, string> EnvVariables { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public bool ContainsSharedShellCall { get; set; } = false;
        public string Title { get; set; }
        }

    public class SourceDirectoryScanner
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(SourceDirectoryScanner));
        public static async Task<ICollection<SourceDirectory>> ScanMachine (CancellationToken cancellationToken, ProgressViewModel progress)
            {
            var drives = await Task.Run(() => DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady && d.TotalSize > 53687091200)//only drives bigger than 50 GB which are fixed and ready
                .Select(d => d.RootDirectory));

            var timer = new Stopwatch();
            timer.Start();
            HashSet<string> skipList = await Task.Run(() => BuildSkipList());
            var inc = new Incrementer(progress);

            var buildStrategyDirs = await Task.Run(() => drives.AsParallel().Select(d => GetAllBuildStrategiesDirectories(d, skipList, inc, cancellationToken)).SelectMany(r => r));
            var environments = await Task.Run(() => buildStrategyDirs.Select(r => IdentifyEnvironment(r)).Where(e => e != null).ToList());

            timer.Stop();
            log.InfoFormat("Searching for environments took {0:0.0} seconds, found {1} source directories.", timer.Elapsed.TotalSeconds, environments.Count);
            return environments;
            }

        internal class Incrementer
            {
            private int _count = 0;
            private int _nextBarrier = 1;
            private ProgressViewModel _progress;

            internal Incrementer(ProgressViewModel progress)
                {
                _progress = progress;
                }

            internal void Increment()
                {
                var i = Interlocked.Increment(ref _count);
                if (i > _nextBarrier)
                    {
                    lock (this)
                        {
                        _progress.ShortStatus = _count.ToString();
                        _nextBarrier += _count > 100 ? 100 : _count;
                        }
                    }
                }
            }

        #region Skiplist
        private static HashSet<string> BuildSkipList ()
            {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            result.Add(Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Name);
            result.Add(new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Name);
            result.Add(new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)).Name);

            result.Add(new DirectoryInfo(Environment.ExpandEnvironmentVariables("%ProgramW6432%")).Name);
            result.Add(new DirectoryInfo(Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%")).Name);
            result.Add(".hg");
            result.Add(".git");
            result.Add("backup");
            log.InfoFormat("Excluding direcotries with the following names from scan: {0}", string.Join(", ", result));
            return result;
            }

        private static bool ShouldSkipDirectory (DirectoryInfo current, int depth, HashSet<string> skipList)
            {
            try
                {
                if (depth > 0)
                    {
                    if (depth > 6) //only go 7 nesting levels deep
                        return true;

                    if (current.Attributes.HasFlag(FileAttributes.Hidden) ||
                        current.Attributes.HasFlag(FileAttributes.System))
                        return true;

                    if (skipList.Contains(current.Name))
                        return true;
                    }

                current.GetAccessControl();

                return false;
                }               //We also get aggregate exceptions here, just skip anyways on all exceptions
            catch (Exception)// when (e is UnauthorizedAccessException || e is DirectoryNotFoundException)
                {
                return true;
                }
            }
        #endregion

        private static IEnumerable<DirectoryInfo> GetAllBuildStrategiesDirectories (DirectoryInfo directory, HashSet<string> skipList, Incrementer inc, CancellationToken token)
            {
            var container = new { Directory = directory, Depth = 0 };
            var queue = CreateQueueFromSingle(container);
            queue.Enqueue(container);

            int i = 0;
            while (queue.Count > 0)
                {
                i++;
                if (i % 100 == 0)
                    token.ThrowIfCancellationRequested();

                var current = queue.Dequeue();
                inc.Increment();
                if (ShouldSkipDirectory(current.Directory, current.Depth, skipList))
                    continue;

                if (current.Directory.Name == "BuildStrategies")
                    yield return current.Directory;


                foreach (DirectoryInfo subdir in current.Directory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                    queue.Enqueue(new { Directory = subdir, Depth = current.Depth + 1 });
                    }
                }
            }

        private static Queue<T> CreateQueueFromSingle<T> (T value)
            {
            return new Queue<T>();
            }

        private static SourceDirectory IdentifyEnvironment (DirectoryInfo buildStrategyDirectory)
            {
            DirectoryInfo srcDir = buildStrategyDirectory.Parent;
            log.InfoFormat("Checking src directory {0} for possible build environment...", buildStrategyDirectory.FullName);

            string hgrcPath = Path.Combine(buildStrategyDirectory.FullName, ".hg\\hgrc");
            if (!File.Exists(hgrcPath))
                {
                log.InfoFormat("Not considering build environment because {0} does not exist.", hgrcPath);
                return null;
                }

            var hgrcContent = File.ReadAllText(hgrcPath);
            var match = Regex.Match(hgrcContent, @"^(?:.*)http://(?<stream>[-_\w]*)\.(?:devteams|hgbranches)(?:\.bentley\.com)(?:.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!match.Success)
                {
                log.InfoFormat("Not considering build environment because {0} does not contain valid stream.", hgrcPath);
                return null;
                }

            var stream = match.Groups["stream"].Value;
            if (string.IsNullOrEmpty(stream))
                {
                log.InfoFormat("Not considering build environment as stream name could not be obtained from {0}.", hgrcPath);
                return null;
                }

            var result = new SourceDirectory() { SrcPath = PathHelper.EnsureTrailingDirectorySeparator(srcDir.FullName), Stream = stream };
            log.InfoFormat("Found new environment in {0}!", srcDir.FullName);
            UpdateEnvironment(result);
            return result;
            }

        public const string ENV_VAR_USE_NEW_BB = "USE_NEW_BB";
        public const string ENV_VAR_BSISRC = "BSISRC";
        public const string ENV_VAR_BSIOUT = "BSIOUT";
        public const string ENV_VAR_BUILDSTRATEGY = "BUILDSTRATEGY";

        private static void UpdateEnvironment (SourceDirectory result)
            {
            var srcPath = new DirectoryInfo(result.SrcPath);
            var candidateSetupEnvScripts = srcPath.Parent.EnumerateFiles("*.bat", SearchOption.TopDirectoryOnly);

            var batchFiles = candidateSetupEnvScripts.Select(s => ReadBatchFile(s)).ToList();

            foreach(var batchFile in batchFiles)
                {
                string outPath, buildStrat;
                if (!batchFile.EnvVariables.TryGetValue(ENV_VAR_BSIOUT, out outPath) || !batchFile.EnvVariables.TryGetValue(ENV_VAR_BUILDSTRATEGY, out buildStrat))
                    continue;

                if (string.IsNullOrEmpty(outPath) || string.IsNullOrEmpty(buildStrat))
                    continue;

                Configuration c = new Configuration();
                c.OutPath = PathHelper.EnsureTrailingDirectorySeparator(outPath);
                c.BuildStrategy = buildStrat;

                if (!string.IsNullOrEmpty(batchFile.Title))
                    c.Alias = batchFile.Title;

                string debug;
                if (batchFile.EnvVariables.TryGetValue("DEBUG", out debug))
                    c.Release = debug != "1";

                result.Configurations.Add(c);
                }
            }

        private static BatchFile ReadBatchFile (FileInfo s)
            {
            var lines = File.ReadAllLines(s.FullName);
            var envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var result = new BatchFile();

            foreach (var rawline in lines)
                {
                var line = rawline.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.IndexOf("REM ", StringComparison.OrdinalIgnoreCase) == 0)
                    continue;

                if (line.StartsWith("::"))
                    continue;

                const string SETTER_REGEX = @"^\s*(?:set)\s+(?<name>\w+)\s*=\s*(?<value>.+)$";
                Match match = Regex.Match(line, SETTER_REGEX, RegexOptions.IgnoreCase);
                if (match.Success)
                    {
                    envVars[match.Groups["name"].Value] = match.Groups["value"].Value;
                    continue;
                    }

                if (line.IndexOf("SharedShellEnv.bat", StringComparison.OrdinalIgnoreCase) > 0)
                    {
                    result.ContainsSharedShellCall = true;
                    continue;
                    }

                const string TITLE_REGEX = @"^\s*(?:TITLE)\s+(?<value>.+)$";
                match = Regex.Match(line, TITLE_REGEX, RegexOptions.IgnoreCase);
                if (match.Success)
                    {
                    result.Title = match.Groups["value"].Value;
                    }
                }

            envVars = envVars.ToDictionary(p => p.Key, p => ExpandVariables(p.Value, envVars), StringComparer.OrdinalIgnoreCase);
            envVars = envVars.ToDictionary(p => p.Key, p => ExpandVariables(p.Value, envVars), StringComparer.OrdinalIgnoreCase);
            foreach (var entry in envVars)
                result.EnvVariables.Add(entry);

            if (!string.IsNullOrEmpty(result.Title))
                result.Title = ExpandVariables(result.Title, envVars);

            return result;
            }

        private static string ExpandVariables (string text, IDictionary<string, string> variables)
            {
            const string VARIABLE_REGEX = @"%\w+%";
            return Regex.Replace(text, VARIABLE_REGEX, m =>
            {
                if (!m.Success)
                    return m.Value;

                string key = m.Value.Trim('%');
                string newValue;
                if (variables.TryGetValue(key, out newValue))
                    return newValue;

                return string.Empty;
            });
            }

        }
    }
