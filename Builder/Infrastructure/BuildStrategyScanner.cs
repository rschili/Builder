using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using log4net;
using RSCoreLib;

namespace Builder
    {
    public class BuildStrategy
        {
        public string Name { get; set; }
        public IList<string> ImportStrategies { get; } = new List<string>();
        public DefaultPartOptions DefaultPartOptions { get; set; }
        public DefaultTarget DefaultTarget { get; set; }
        public IList<PartStrategy> PartStrategies { get; } = new List<PartStrategy>();
        public IList<LocalRepository> LocalRepositories { get; } = new List<LocalRepository>();
        public int Priority { get; set; }
        }

    public class CompiledBuildStrategy
        {
        public DefaultPartOptions DefaultPartOptions { get; set; }
        public DefaultTarget DefaultTarget { get; set; }
        public List<PartStrategy> PartStrategies { get; } = new List<PartStrategy>();
        public IDictionary<string, LocalRepository> LocalRepositories { get; } = new Dictionary<string, LocalRepository>(StringComparer.OrdinalIgnoreCase);

        //we ignore the repository flag as PartStrategies are only keyed on part file name.
        internal BuildFromSource GetBuildFromSource (string partFile) //, string repository) 
            {
            foreach(var ps in PartStrategies)
                {
                if (ps.PartName != "*")
                    continue;//we don't handle names of specific parts, just work on the part file level for now.

                if (ps.PartFile == "*")
                    return ps.BuildFromSource;

                if (string.Equals(partFile, ps.PartFile, StringComparison.OrdinalIgnoreCase))
                    return ps.BuildFromSource;
                }

            return DefaultPartOptions?.BuildFromSource ?? BuildFromSource.Never;
            }
        }

    public class DefaultPartOptions
        {
        public BuildFromSource BuildFromSource { get; set; }
        public string OnError { get; set; }
        }

    public class DefaultTarget
        {
        public string Repository { get; set; }
        public string PartFile { get; set; }
        public string PartName { get; set; }
        public string Platform { get; set; }
        }

    public class LocalRepository
        {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Directory { get; set; }
        }

    public enum BuildFromSource
        {
        Always,
        Never,
        Once
        }

    public class PartStrategy
        {
        public string PartFile { get; set; }
        public string PartName { get; set; }
        public BuildFromSource BuildFromSource { get; set; } = BuildFromSource.Never;
        }

    public class BuildStrategyScanner
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(BuildStrategyScanner));

        public static CompiledBuildStrategy LoadCompiledStrategy (string srcPath, string buildStrategies, CancellationToken token)
            {
            if (string.IsNullOrEmpty(srcPath))
                throw new UserfriendlyException("Source Path must be set");

            if (string.IsNullOrEmpty(buildStrategies))
                throw new UserfriendlyException("Build Strategy must be set");

            string strategiesDir = Path.Combine(srcPath, "BuildStrategies");
            if (!Directory.Exists(strategiesDir))
                throw new UserfriendlyException($"Directory {strategiesDir} does not exist.");

            string[] strats = buildStrategies.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            var r = ReadStrategies(strategiesDir, strats);
            ReadImportedStrategies(strategiesDir, r, 0);
            token.ThrowIfCancellationRequested();
            var compiledStrategy = CompileStrategy(r.Values.Where(s => s != null).OrderBy(s => s.Priority).ToList());
            return compiledStrategy;
            }


        private static IDictionary<string, BuildStrategy> ReadStrategies(string strategiesDirectory, IEnumerable<string> strats)
            {
            var result = new Dictionary<string, BuildStrategy>(StringComparer.OrdinalIgnoreCase);
            foreach(var s in strats)
                {
                result.Add(s, ReadBuildStrategy(strategiesDirectory, s, 0));
                }
            return result;
            }

        private static BuildStrategy ReadBuildStrategy (string strategiesDirectory, string strategyName, int priority)
            {
            string fileName = FindStrategy(strategiesDirectory, strategyName);
            if (string.IsNullOrEmpty(fileName))
                throw new UserfriendlyException($"Could not find strategy '{strategyName}' in folder {strategiesDirectory} or a subdir of it.");

            var xml = XDocument.Load(fileName);
            var strategyNode = xml.Elements().Where(e => string.Equals(e.Name.LocalName, "BuildStrategy",StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (strategyNode == null)
                throw new UserfriendlyException($"Did not find BuildStrategy root element in '{fileName}'.");

            var result = new BuildStrategy();
            result.Priority = priority;
            result.Name = strategyName;
            foreach(var node in strategyNode.Elements())
                {
                if (node.NodeType != System.Xml.XmlNodeType.Element)
                    continue;

                switch (node.Name.LocalName)
                    {
                    case "ImportStrategy":
                        var name = node.Attribute("Name");
                        if (name == null)
                            continue;

                        string v = name.Value;
                        if (string.IsNullOrWhiteSpace(v) || result.ImportStrategies.Contains(v, StringComparer.OrdinalIgnoreCase))
                            continue;

                        result.ImportStrategies.Add(v);
                        break;

                    case "DefaultTarget":
                        result.DefaultTarget = new DefaultTarget()
                            {
                            Repository = node.Attribute("Repository")?.Value,
                            PartFile = node.Attribute("PartFile")?.Value,
                            PartName = node.Attribute("PartName")?.Value,
                            Platform = node.Attribute("Platform")?.Value
                            };
                        break;

                    case "DefaultPartOptions":
                        var fs = node.Attribute("BuildFromSource")?.Value;
                        if (string.IsNullOrEmpty(fs))
                            continue;

                        BuildFromSource fse;
                        if (!Enum.TryParse(fs, out fse))
                            continue;

                        result.DefaultPartOptions = new DefaultPartOptions()
                            {
                            BuildFromSource = fse,
                            OnError = node.Attribute("OnError")?.Value
                            };
                        break;

                    case "PartStrategy":
                        var fromSource = node.Attribute("BuildFromSource")?.Value;
                        if (string.IsNullOrEmpty(fromSource))
                            continue;

                        BuildFromSource flag;
                        if (!Enum.TryParse(fromSource, out flag))
                            continue;

                        result.PartStrategies.Add(new PartStrategy()
                            {
                            PartFile = node.Attribute("PartFile")?.Value,
                            PartName = node.Attribute("PartName")?.Value,
                            BuildFromSource = flag
                            });
                        break;

                    case "LocalRepository":
                        var lr = new LocalRepository()
                            {
                            Name = node.Attribute("Name")?.Value,
                            Type = node.Attribute("Type")?.Value
                            };

                        var dir = node.Attribute("Directory")?.Value; 
                        //HACK: instead of resolving the env var, just drop it, as directory seems to start with it.
                        if(!string.IsNullOrEmpty(dir) && dir.StartsWith("${SrcRoot}", StringComparison.OrdinalIgnoreCase))
                        {
                            dir = dir.Substring(10);
                        }

                        lr.Directory = dir;
                        result.LocalRepositories.Add(lr);
                        break;

                    //simply ignore these, they are not relevant
                    case "FirebugJob":
                    case "RepositoryLists":
                    case "BuildTranskitOptions":
                    case "DefaultProvenance":
                    case "RemoteRepositoryList":
                    case "RepositoryTag":
                    case "LastKnownGoodSource":
                    case "LastKnownGoodServer":
                    case "SdkSource":
                    case "TransKitDirectory":
                    case "ToolsetPart":
                    case "DefaultLastKnownGoodSource":
                        break;

                    default:
                        log.InfoFormat("Unknown Element in BuildStrategy: {0}, File: {1}", node.Name, fileName);
                        break;
                    }
                }

            return result;
            }

        private static string FindStrategy (string strategiesDirectory, string strategyName)
            {
            var desiredFileName = $"{strategyName}.BuildStrategy.xml";
            var direct = Path.Combine(strategiesDirectory, desiredFileName);
            if (File.Exists(direct))
                return direct;

            var found = Directory.EnumerateFiles(strategiesDirectory, desiredFileName, SearchOption.AllDirectories).FirstOrDefault();
            return found;
            }



        private static void ReadImportedStrategies (string strategiesDir, IDictionary<string, BuildStrategy> strats, int depth)
            {
            if (depth >= 10)
                {
                log.Warn("Maximum depth of 10 nested strategies reached, exiting..");
                return;
                }

            var allStrats = strats.Values.SelectMany(b => b.ImportStrategies).ToList();
            var missingStrats = allStrats.Except(strats.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            if (missingStrats.Count <= 0)
                return;

            depth++;
            foreach (var s in missingStrats)
                {
                strats.Add(s, ReadBuildStrategy(strategiesDir, s, depth));
                }

            ReadImportedStrategies(strategiesDir, strats, depth);
            }

        private static CompiledBuildStrategy CompileStrategy (List<BuildStrategy> list)
            {
            var result = new CompiledBuildStrategy();
            result.DefaultPartOptions = list.Select(s => s.DefaultPartOptions).FirstOrDefault(dpo => dpo != null);
            result.DefaultTarget = list.Select(s => s.DefaultTarget).FirstOrDefault(dt => dt != null);
            var partStrategies = list.SelectMany(s => s.PartStrategies);
            foreach(var ps in partStrategies)
                {
                if (ps.PartFile?.Equals("$(strategy.defaultPartFile)", StringComparison.OrdinalIgnoreCase) == true)
                    ps.PartFile = result.DefaultTarget?.PartFile;

                if (string.IsNullOrEmpty(ps.PartFile))
                    continue;

                result.PartStrategies.Add(ps);
                }

            var localRepositories = list.SelectMany(s => s.LocalRepositories).Where(l => !string.IsNullOrEmpty(l.Directory));
            var dict = result.LocalRepositories;
            foreach (var lr in localRepositories)
                {
                dict[lr.Name] = lr;
                }
            
            return result;
            }
        }
    }
