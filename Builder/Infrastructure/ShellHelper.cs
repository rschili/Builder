using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using log4net;
using RSCoreLib;
using RSCoreLib.OS;

namespace Builder
    {
    public static class ShellHelper
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(ShellHelper));

        public static string TryFindTCC ()
            {
            log.Info("Begin checking machine for installed TCC software");
            try
                {
                var software = InstalledSoftwareHelper.GetInstalledSoftware().ToList();
                return DetermineInstalledTCCVersion(software);
                }
            catch (Exception e)
                {
                log.Error("Failed to scan for installed optional software." + e.Message);
                return null;
                }
            }

        private static string DetermineInstalledTCCVersion (IList<InstalledSoftware> software)
            {
            var tcc = software.Where(s => s.DisplayName != null && s.DisplayName.StartsWith("TCC LE")).OrderByDescending(o => o.VersionMajor).FirstOrDefault();
            if (tcc == null || string.IsNullOrEmpty(tcc.InstallationPath))
                return null;

            if (tcc.VersionMajor.HasValue)
                {
                if (tcc.VersionMajor.Value < 14)
                    {
                    log.WarnFormat("The found version of {0} is {1}. It is suggested to use TCC LE 14 or newer. Going to use this version anyways.", tcc.DisplayName, tcc.VersionMajor);
                    }
                }

            var tccFile = Path.Combine(tcc.InstallationPath, "tcc.exe");
            if (!File.Exists(tccFile))
                {
                log.WarnFormat("Could not find {0} in installation path '{1}'. Will use CMD instead.", tcc.DisplayName, tccFile);
                return string.Empty;
                }

            log.InfoFormat("Found installed TCC client in {0}, version is {1}", tccFile, tcc.VersionMajor);
            return tccFile;
            }

        public static void OpenNewEnvironmentShell (ICollection<string> setupEnvScript, string workingDirectory, string tccPath)
            {
            try
                {
                var tempPath = Path.Combine(Path.GetTempPath(), "Builder_Bats");
                if (!Directory.Exists(tempPath))
                    Directory.CreateDirectory(tempPath);

                var batFileName = Path.Combine(tempPath, "tempsetupenv.bat");
                File.WriteAllLines(batFileName, setupEnvScript);

                ProcessStartInfo startInfo;
                if (!string.IsNullOrEmpty(tccPath) && File.Exists(tccPath))
                    {
                    startInfo = new ProcessStartInfo(tccPath, "\"" + batFileName + "\"");
                    }
                else
                    {
                    startInfo = new ProcessStartInfo("cmd", "/k \"" + batFileName + "\"");
                    }

                startInfo.WorkingDirectory = workingDirectory;
                Process.Start(startInfo);
                }
            catch (Exception e)
                {
                log.ErrorFormat("Failed to open shell. {0}", e.Message);
                }
            }

        public static ICollection<string> GetSetupEnvScript (string src, string outPath, string strategy, string title, bool debug, bool tcc, params string[] commands)
            {
            var sharedShellPath = Path.Combine(src, @"bsicommon\shell\SharedShellEnv.bat");
            src = PathHelper.EnsureTrailingDirectorySeparator(src);
            outPath = PathHelper.EnsureTrailingDirectorySeparator(outPath);
            var result = new List<string>();
            result.Add($"SET BSISRC={src}");
            result.Add($"SET BSIOUT={outPath}");

            var bb20Path = Path.Combine(src, "BentleyBuild");
            if (Directory.Exists(bb20Path))
                result.Add($"SET USE_NEW_BB=1");

            result.Add($"SET BUILDSTRATEGY={strategy}");
            result.Add($"call \"{sharedShellPath}\"");
            result.Add($"title {title}");

            foreach(var command in GetShellCommands(commands))
                {
                if(command.StartsWith("!TCC"))
                    {
                    if (!tcc)
                        continue;

                    result.Add(command.Substring(4));
                    continue;
                    }

                if (command.StartsWith("!CMD"))
                    {
                    if (tcc)
                        continue;

                    result.Add(command.Substring(4));
                    continue;
                    }

                result.Add(command);
                }

            result.Add($"set DEBUG={(debug ? "1" : string.Empty)}");
            result.Add($"set NDEBUG={(!debug ? "1" : string.Empty)}");

            return result;
            }

        public static IEnumerable<string> GetShellCommands (params string[] commands)
            {
            foreach (var c in commands)
                {
                if (string.IsNullOrEmpty(c))
                    continue;

                using (StringReader reader = new StringReader(c))
                    {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        {
                        if (string.IsNullOrEmpty(line))
                            continue;

                        yield return line;
                        }
                    }
                }
            }

        public static CommandLineSandbox SetupEnv (string src, string outPath, string strategy, string title, bool debug, params string[] commands)
            {
            CommandLineSandbox cmd = null;

            if (!Directory.Exists(src))
                {
                throw new InvalidOperationException($"Cannot setup env because directory {src} does not exist.");
                }

            try
                {
                if (!Directory.Exists(outPath))
                    {
                    Directory.CreateDirectory(outPath);
                    }

                cmd = new CommandLineSandbox(src);
                foreach (var line in GetSetupEnvScript(src, outPath, strategy, title, debug, false, commands))
                    {
                    if (!cmd.ExecuteCommand(line).Success)
                        throw new UserfriendlyException("Failed to set-up environment. The following command failed: " + line);
                    }
                }
            catch (Exception)
                {
                if (cmd != null)
                    cmd.Dispose();

                throw;
                }

            return cmd;
            }

        public static bool OpenDirectoryInExplorer (string directoryPath)
            {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                {
                log.WarnFormat("Directory {0} could not be opened.", directoryPath);
                return false;
                }

            try
                {
                Process.Start(directoryPath);
                return true;
                }
            catch (Exception e)
                {
                log.WarnFormat("Exception trying to open directory {0}. {1}", directoryPath, e.Message);
                return false;
                }
            }
        }
    }
