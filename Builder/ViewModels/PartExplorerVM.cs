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
using RSCoreLib;
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

        private object _content = "Loading...";
        public object Content
            {
            get
                {
                return _content;
                }
            set
                {
                _content = value;
                OnPropertyChanged();
                }
            }

        public string Title => $"{_configuration?.Alias} on {_configuration?.Parent?.Stream}";

        public PartExplorerVM (ConfigurationVM config)
            {
            _configuration = config;
            //Task.Run((Action)ScanForParts);
            }

        public void Initialize ()
            {
            if (_configuration == null)
                return;

            string srcPath = _configuration?.Parent?.SrcPath;
            if (string.IsNullOrEmpty(srcPath) || !Directory.Exists(srcPath))
                {
                Content = "Source Dir does not exist.";
                return;
                }

            string strategies = _configuration?.BuildStrategy;
            if (string.IsNullOrEmpty(strategies))
                {
                Content = "No Build Strategies defined.";
                return;
                }

            Content = "Processing Build Strategies...";
            try
                {
                var compiledStrategy = BuildStrategyScanner.LoadCompiledStrategy(srcPath, strategies);
                if (compiledStrategy == null)
                    {
                    Content = "No Compiled Strategy returned."; //todo: user friendly text
                    return;
                    }

                log.Info($"Compiled Strategy contains {compiledStrategy.PartStrategies.Count} PartStrategies, {compiledStrategy.LocalRepositories.Count} LocalRepositories.");
                var dt = compiledStrategy.DefaultTarget;
                if (dt == null)
                    {
                    Content = "Strategy has no default target.";
                    return;
                    }

                log.Info($"Default Target '{dt.PartName}' in File '{dt.PartFile}' Repository '{dt.Repository}'");
                Content = "Processing Parts...";
                var partFiles = PartFileScanner.DiscoverParts(compiledStrategy, srcPath);
                }
            catch(UserfriendlyException ue)
                {
                Content = ue.Message;
                }
            catch(Exception e)
                {
                Content = "Could not load parts. " + e.Message;
                }
            }
        }
    }
