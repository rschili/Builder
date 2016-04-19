using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using log4net;
using Newtonsoft.Json.Linq;
using RSCoreLib.WPF;

namespace Builder
    {
    public class PartExplorerVM : ViewModelBase
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(PartExplorerVM));

        private readonly ConfigurationVM _configuration;
        public PartExplorerVM ()
            {
            }

        public PartExplorerVM (ConfigurationVM config)
            {
            _configuration = config;
            Task.Run((Action)ScanForParts);
            }

        private void ScanForParts ()
            {
            if (_configuration == null)
                return;

            string srcPath = _configuration?.Parent?.SrcPath;
            if (string.IsNullOrEmpty(srcPath))
                return;

            string strategies = _configuration?.BuildStrategy;
            if (string.IsNullOrEmpty(strategies))
                return;

            string strategiesDir = Path.Combine(srcPath, "BuildStrategies");
            if (!Directory.Exists(strategiesDir))
                return;

            string[] strats = strategies.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            try
                {
                var r = ReadStrategies(strategiesDir, strats);
                ReadImportedStrategies(strategiesDir, r, 0);
                var compiledStrategy = CompileStrategy(r.Values.Where(s => s != null).OrderBy(s => s.Priority).ToList());
                log.Info($"Read an overall of {r.Count} Build Strategies.");
                log.Info($"Compiled Strategy contains {compiledStrategy.PartStrategies.Count} PartStrategies, {compiledStrategy.LocalRepositories.Count} LocalRepositories.");
                var dt = compiledStrategy.DefaultTarget;
                if (dt == null)
                    {
                    log.Info("Compiled Strategy has no default target.");
                    return;
                    }

                log.Info($"Default Target '{dt.PartName}' in File '{dt.PartFile}' Repository '{dt.Repository}'");
                return;
                }
            catch (Exception e)
                {
                log.Error($"Error scanning for parts. {e.Message}", e);
                }
            }


        private IDictionary<string, BuildStrategy> ReadStrategies(string strategiesDirectory, IEnumerable<string> strats)
            {
            var result = new Dictionary<string, BuildStrategy>(StringComparer.OrdinalIgnoreCase);
            foreach(var s in strats)
                {
                result.Add(s, ReadBuildStrategy(strategiesDirectory, s, 0));
                }
            return result;
            }

        private BuildStrategy ReadBuildStrategy (string strategiesDirectory, string strategyName, int priority)
            {
            string fileName = FindStrategy(strategiesDirectory, strategyName);
            if (string.IsNullOrEmpty(fileName))
                return null;

            var xml = XDocument.Load(fileName);
            var strategyNode = xml.Elements().Where(e => string.Equals(e.Name.LocalName, "BuildStrategy",StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (strategyNode == null)
                return null;

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
                        result.DefaultPartOptions = new DefaultPartOptions()
                            {
                            BuildFromSource = node.Attribute("BuildFromSource")?.Value,
                            OnError = node.Attribute("OnError")?.Value
                            };
                        break;

                    case "PartStrategy":
                        result.PartStrategies.Add(new PartStrategy()
                            {
                            PartFile = node.Attribute("PartFile")?.Value,
                            PartName = node.Attribute("PartName")?.Value,
                            BuildFromSource = node.Attribute("BuildFromSource")?.Value
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
                        break;

                    default:
                        log.InfoFormat("Unknown Element encountered: {0} in file {1}", node.Name, fileName);
                        break;
                    }
                }

            return result;
            }

        private string FindStrategy (string strategiesDirectory, string strategyName)
            {
            var desiredFileName = $"{strategyName}.BuildStrategy.xml";
            var direct = Path.Combine(strategiesDirectory, desiredFileName);
            if (File.Exists(direct))
                return direct;

            var found = Directory.EnumerateFiles(strategiesDirectory, desiredFileName, SearchOption.AllDirectories).FirstOrDefault();
            return found;
            }

        internal class BuildStrategy
            {
            public string Name { get; set; }
            public IList<string> ImportStrategies { get; } = new List<string>();
            public DefaultPartOptions DefaultPartOptions { get; set; }
            public DefaultTarget DefaultTarget { get; set; }
            public IList<PartStrategy> PartStrategies { get; } = new List<PartStrategy>();
            public IList<LocalRepository> LocalRepositories { get; } = new List<LocalRepository>();
            public int Priority { get; set; }
            }

        internal class CompiledBuildStrategy
            {
            public DefaultPartOptions DefaultPartOptions { get; set; }
            public DefaultTarget DefaultTarget { get; set; }
            public List<PartStrategy> PartStrategies { get; } = new List<PartStrategy>();
            public List<LocalRepository> LocalRepositories { get; } = new List<LocalRepository>();
            }

        internal class DefaultPartOptions
            {
            public string BuildFromSource { get; set; }
            public string OnError { get; set; }
            }

        internal class DefaultTarget
            {
            public string Repository { get; set; }
            public string PartFile { get; set; }
            public string PartName { get; set; }
            public string Platform { get; set; }
            }

        internal class LocalRepository
            {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Directory { get; set; }
            }

        internal class PartStrategy
            {
            public string PartFile { get; set; }
            public string PartName { get; set; }
            public string BuildFromSource { get; set; }
            }

        private void ReadImportedStrategies (string strategiesDir, IDictionary<string, BuildStrategy> strats, int depth)
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

        private CompiledBuildStrategy CompileStrategy (List<BuildStrategy> list)
            {
            var result = new CompiledBuildStrategy();
            result.DefaultPartOptions = list.Select(s => s.DefaultPartOptions).FirstOrDefault(dpo => dpo != null);
            result.DefaultTarget = list.Select(s => s.DefaultTarget).FirstOrDefault(dt => dt != null);
            var partStrategies = list.SelectMany(s => s.PartStrategies);
            result.PartStrategies.AddRange(partStrategies);

            var localRepositories = list.SelectMany(s => s.LocalRepositories).Where(l => !string.IsNullOrEmpty(l.Directory));
            result.LocalRepositories.AddRange(localRepositories);
            return result;
            }
        }
    }
