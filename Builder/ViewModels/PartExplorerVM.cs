using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
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

        public IList<PartElementContainerVM> Items { get; } = new List<PartElementContainerVM>();

        public PartExplorerVM (ConfigurationVM config)
            {
            _configuration = config;
            //Task.Run((Action)ScanForParts);
            }

        public void Initialize (CancellationToken token)
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
                token.ThrowIfCancellationRequested();
                var compiledStrategy = BuildStrategyScanner.LoadCompiledStrategy(srcPath, strategies, token);
                log.Info($"Compiled Strategy contains {compiledStrategy.PartStrategies.Count} PartStrategies, {compiledStrategy.LocalRepositories.Count} LocalRepositories.");
                var dt = compiledStrategy.DefaultTarget;
                if (dt == null)
                    {
                    Content = "Strategy has no default target.";
                    return;
                    }

                if (string.IsNullOrEmpty(dt.PartFile) || string.IsNullOrEmpty(dt.Repository))
                    {
                    Content = "Default Target requires a PartFile and Repository attribute.";
                    return;
                    }

                log.Info($"Default Target '{dt.PartName}' in File '{dt.PartFile}' Repository '{dt.Repository}'");
                InitializePartVMs(compiledStrategy, srcPath, token);
                }
            catch (UserfriendlyException ue)
                {
                Content = ue.Message;
                }
            catch(Exception e)
                {
                Content = "Could not load parts. " + e.Message;
                }
            }

        private void InitializePartVMs (CompiledBuildStrategy buildStrategy, string srcPath, CancellationToken token)
            {
            Content = "Processing Parts...";
            var defaultPartFile = PartFileScanner.LoadPartFile(buildStrategy.DefaultTarget.PartFile, buildStrategy.DefaultTarget.Repository, 
                buildStrategy.LocalRepositories, srcPath);

            var products = defaultPartFile.Products.Select(p =>
            {
                return new { VM = new PartExplorerElementVM(_configuration) { Name = p.Name, IsProduct = true }, Product = p };
            }).ToList();
            var productVMsByName = products.ToDictionary(a => a.Product.Name, a => a.VM, StringComparer.OrdinalIgnoreCase);

            var parts = defaultPartFile.Parts.Select(p =>
            {
                return new { VM = new PartExplorerElementVM(_configuration) { Name = p.Name, IsProduct = false, MakeFile = p.MakeFile }, Part = p };
            }).ToList();
            var partVMsByName = parts.ToDictionary(a => a.Part.Name, a => a.VM, StringComparer.OrdinalIgnoreCase);
            token.ThrowIfCancellationRequested();

            Queue<Tuple<PartExplorerElementVM, SubPart>> externalSubparts = new Queue<Tuple<PartExplorerElementVM, SubPart>>();
            foreach (var product in products)
                {
                foreach (var subProductName in product.Product.SubProducts)
                    {
                    PartExplorerElementVM vm;
                    if (productVMsByName.TryGetValue(subProductName, out vm) && vm != null)
                        {
                        product.VM.AddChild(vm);
                        continue;
                        }

                    log.Warn($"Product {product.Product.Name} has a subproduct {subProductName} which could not be found.");
                    }

                foreach(var subPart in product.Product.SubParts)
                    {
                    PartExplorerElementVM vm;
                    if (partVMsByName.TryGetValue(subPart.Name, out vm) && vm != null)
                        {
                        product.VM.AddChild(vm);
                        continue;
                        }

                    externalSubparts.Enqueue(new Tuple<PartExplorerElementVM, SubPart>(product.VM, subPart));
                    }
                }

            foreach (var part in parts)
                {
                foreach (var subPart in part.Part.SubParts)
                    {
                    PartExplorerElementVM vm;
                    if (string.IsNullOrEmpty(subPart.Repository) && string.IsNullOrEmpty(subPart.PartFile) &&
                        partVMsByName.TryGetValue(subPart.Name, out vm) && vm != null)
                        {
                        part.VM.AddChild(vm);
                        continue;
                        }

                    externalSubparts.Enqueue(new Tuple<PartExplorerElementVM, SubPart>(part.VM, subPart));
                    }
                }

            var all = products.Select(p => p.VM).Concat(parts.Select(p => p.VM)).ToList();
            var defaultPart = all.FirstOrDefault(e => string.Equals(e.Name, buildStrategy.DefaultTarget.PartName));
            if(defaultPart != null)
                {
                var container = new PartElementContainerVM() { Title = "Default Target" };
                container.Items.Add(defaultPart);
                container.IsExpanded = true;
                Items.Add(container);
                }

            var roots = all.Where(p => !p.HasParent);
            var wop = new PartElementContainerVM() { Title = "Roots" };
            foreach(var e in roots)
                {
                wop.Items.Add(e);
                }
            Items.Add(wop);

            var flatContainer = new PartElementContainerVM() { Title = "All" };
            foreach (var e in all)
                {
                flatContainer.Items.Add(e);
                }
            Items.Add(flatContainer);

            LoadExternalSubparts(externalSubparts, buildStrategy, srcPath, token);
            Content = this;//using this view model as both, the container and the content
            }

        private void LoadExternalSubparts (Queue<Tuple<PartExplorerElementVM, SubPart>> externalSubparts, CompiledBuildStrategy buildStrategy, string srcPath, CancellationToken token)
            {
            var externalPartFiles = new Dictionary<PartFileKey, ExternalPartFile>();
            while (externalSubparts.Count > 0)
                {
                var currentPart = externalSubparts.Dequeue();
                var sp = currentPart.Item2;
                var key = new PartFileKey() { Name = sp.PartFile, Repository = sp.Repository };

                ExternalPartFile externalPartFile;
                if (!externalPartFiles.TryGetValue(key, out externalPartFile))
                    {
                    token.ThrowIfCancellationRequested();
                    var epf = PartFileScanner.LoadPartFile(key.Name, key.Repository,
                        buildStrategy.LocalRepositories, srcPath);

                    if (epf == null)
                        throw new UserfriendlyException($"Failed to load part file {key.Name} in repository {key.Repository}");

                    externalPartFile = new ExternalPartFile() { File = epf, Key = key };
                    externalPartFiles.Add(key, externalPartFile);
                    }

                PartExplorerElementVM subPartVM = LoadExternalSubpartVM(externalSubparts, sp, externalPartFile);

                currentPart.Item1.AddChild(subPartVM);
                }
            }

        private PartExplorerElementVM LoadExternalSubpartVM (Queue<Tuple<PartExplorerElementVM, SubPart>> externalSubparts, SubPart sp, ExternalPartFile externalPartFile)
            {
            PartExplorerElementVM subPartVM;
            if (!externalPartFile.LoadedParts.TryGetValue(sp.Name, out subPartVM))
                {
                var loadedSubPart = externalPartFile.File.Parts.FirstOrDefault(p => string.Equals(p.Name, sp.Name, StringComparison.OrdinalIgnoreCase));
                if (loadedSubPart == null)
                    {
                    throw new UserfriendlyException($"Failed to find part {sp.Name} in partfile {sp.PartFile}");
                    }

                subPartVM = new PartExplorerElementVM(_configuration)
                    {
                    Name = sp.Name,
                    Repository = externalPartFile.Key.Repository,
                    PartFile = externalPartFile.Key.Name,
                    RelativePath = externalPartFile.File.RelativePath,
                    MakeFile = loadedSubPart.MakeFile
                    };

                externalPartFile.LoadedParts.Add(sp.Name, subPartVM);

                AddChildVMs(externalSubparts, externalPartFile, subPartVM, loadedSubPart);
                }

            return subPartVM;
            }

        private void AddChildVMs (Queue<Tuple<PartExplorerElementVM, SubPart>> externalSubparts, ExternalPartFile externalPartFile, PartExplorerElementVM currentVM, Part currentPart)
            {
            foreach (var subPart in currentPart.SubParts)
                {
                if (!string.IsNullOrEmpty(subPart.Repository) || !string.IsNullOrEmpty(subPart.PartFile))
                    {
                    externalSubparts.Enqueue(new Tuple<PartExplorerElementVM, SubPart>(currentVM, subPart));
                    continue;
                    }

                PartExplorerElementVM vm;
                if(!externalPartFile.LoadedParts.TryGetValue(subPart.Name, out vm))
                    {
                    vm = LoadExternalSubpartVM(externalSubparts, subPart, externalPartFile);
                    }

                currentVM.AddChild(vm);
                }
            }

        internal class ExternalPartFile
            {
            public IDictionary<string, PartExplorerElementVM> LoadedParts = new Dictionary<string, PartExplorerElementVM>(StringComparer.OrdinalIgnoreCase);
            public PartFile File;
            public PartFileKey Key;
            }

        public class PartFileKey
            {
            public string Name;
            public string Repository;

            public override int GetHashCode ()
                {
                var nameHash = Name?.GetHashCode() ?? 0;
                var repositoryHash = Repository?.GetHashCode() ?? 0;
                return nameHash ^ repositoryHash;
                }

            public override bool Equals (object obj)
                {
                var other = obj as PartFileKey;
                if (other == null)
                    return false;

                return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Repository, other.Repository, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

    public class PartElementContainerVM
        {
        public List<PartExplorerElementVM> Items { get; } = new List<PartExplorerElementVM>();
        public string Title { get; set; }
        public bool IsExpanded { get; set; } = false;
        }

    public enum PartType
        {
        Unknown,
        Group,
        CSharp,
        Cpp,
        Test,
        Product
        }

    public class PartExplorerElementVM : ViewModelBase
        {
        private ConfigurationVM _configuration;

        public PartExplorerElementVM (ConfigurationVM _configuration)
            {
            this._configuration = _configuration;
            }

        public bool IsProduct { get; internal set; }
        public bool HasParent { get; internal set; } = false;
        public PartType PartType => ResolvePartType();
        public string Name { get; internal set; }
        public string MakeFile { get; set; } = null;
        public IList<PartExplorerElementVM> Children { get; } = new List<PartExplorerElementVM>();
        public string Repository { get; internal set; }
        public string PartFile { get; internal set; }
        
        public string DisplayLabel
            {
            get
                {
                if (string.IsNullOrEmpty(RelativePath))
                    return Name;

                return $"{Name} ({RelativePath})";
                }
            }

        public string RelativePath { get; internal set; }

        internal void AddChild (PartExplorerElementVM vm)
            {
            Children.Add(vm);
            vm.HasParent = true;
            }

        private PartType ResolvePartType ()
            {
            if (IsProduct)
                return PartType.Product;

            if (string.IsNullOrEmpty(MakeFile))
                return PartType.Group;

            if (Name.Contains("Test"))
                return PartType.Test;

            return PartType.Unknown;
            }
        }

    public sealed class PartTypeToIconConverter : IValueConverter
        {
        public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
            {
            if (!(value is PartType))
                return string.Empty;

            PartType pt = (PartType)value;
            switch (pt)
                {
                case PartType.Group:
                    return "../Images/vs/group16.png";
                /*case PartType.CSharp:
                    return "../Images/vs/cs16.png";
                case PartType.Cpp:
                    return "../Images/vs/cpp16.png";*/
                case PartType.Test:
                    return "../Images/vs/test16.png";
                case PartType.Product:
                    return "../Images/app16.png";
                default:
                    return "../Images/vs/part16.png";
                }
            }

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
            {
            throw new NotSupportedException();
            }
        }

    public sealed class PartTypeToLabelConverter : IValueConverter
        {
        public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
            {
            if (!(value is PartType))
                return string.Empty;

            PartType pt = (PartType)value;
            switch (pt)
                {
                case PartType.Group:
                    return "Group Part";
                case PartType.CSharp:
                    return "C# Part";
                case PartType.Cpp:
                    return "C++ Part";
                case PartType.Test:
                    return "Test Part";
                case PartType.Product:
                    return "Product";
                default:
                    return "Uncategorized Part.";
                }
            }

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
            {
            throw new NotSupportedException();
            }
        }
    }
