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

            var compiledStrategy = BuildStrategyScanner.LoadCompiledStrategy(srcPath, strategies);
            if (compiledStrategy == null)
                return;

            log.Info($"Compiled Strategy contains {compiledStrategy.PartStrategies.Count} PartStrategies, {compiledStrategy.LocalRepositories.Count} LocalRepositories.");
            var dt = compiledStrategy.DefaultTarget;
            if (dt == null)
                {
                log.Info("Compiled Strategy has no default target.");
                return;
                }

            log.Info($"Default Target '{dt.PartName}' in File '{dt.PartFile}' Repository '{dt.Repository}'");
            var partFiles = PartFileScanner.DiscoverParts(compiledStrategy, srcPath);
            }
        }
    }
