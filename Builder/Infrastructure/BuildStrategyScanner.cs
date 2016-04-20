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
        }

    public class DefaultPartOptions
        {
        public string BuildFromSource { get; set; }
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

    public class PartStrategy
        {
        public string PartFile { get; set; }
        public string PartName { get; set; }
        public string BuildFromSource { get; set; }
        }

    public class BuildStrategyScanner : ViewModelBase
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(BuildStrategyScanner));

        public static CompiledBuildStrategy LoadCompiledStrategy (string srcPath, string buildStrategies)
            {
            if (string.IsNullOrEmpty(srcPath))
                return null;

            if (string.IsNullOrEmpty(buildStrategies))
                return null;

            string strategiesDir = Path.Combine(srcPath, "BuildStrategies");
            if (!Directory.Exists(strategiesDir))
                return null;

            string[] strats = buildStrategies.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            try
                {
                var r = ReadStrategies(strategiesDir, strats);
                ReadImportedStrategies(strategiesDir, r, 0);
                var compiledStrategy = CompileStrategy(r.Values.Where(s => s != null).OrderBy(s => s.Priority).ToList());
                return compiledStrategy;
                }
            catch (Exception e)
                {
                log.Error($"Error scanning Build Strategies. {e.Message}", e);
                return null;
                }
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
            result.PartStrategies.AddRange(partStrategies);

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
