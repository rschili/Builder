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
                log.Info($"Read an overall of {r.Count} Build Strategies.");
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
                result.Add(s, ReadBuildStrategy(strategiesDirectory, s));
                }
            return result;
            }

        private BuildStrategy ReadBuildStrategy (string strategiesDirectory, string strategyName)
            {
            string fileName = FindStrategy(strategiesDirectory, strategyName);
            if (string.IsNullOrEmpty(fileName))
                return null;

            var xml = XDocument.Load(fileName);
            var strategyNode = xml.Elements().Where(e => string.Equals(e.Name.LocalName, "BuildStrategy",StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (strategyNode == null)
                return null;

            var result = new BuildStrategy();
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
            public IList<string> ImportStrategies { get; } = new List<string>();
            public DefaultPartOptions DefaultPartOptions { get; set; }
            public DefaultTarget DefaultTarget { get; set; }
            public IList<PartStrategy> PartStrategies { get; } = new List<PartStrategy>();
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

            foreach (var s in missingStrats)
                {
                strats.Add(s, ReadBuildStrategy(strategiesDir, s));
                }

            ReadImportedStrategies(strategiesDir, strats, depth + 1);
            }
        }
    }
