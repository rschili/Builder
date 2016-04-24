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

        private object _content = new CallbackMessage("Loading...");
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
                Content = new CallbackMessage("Source Dir does not exist.");
                return;
                }

            string strategies = _configuration?.BuildStrategy;
            if (string.IsNullOrEmpty(strategies))
                {
                Content = new CallbackMessage("No Build Strategies defined.");
                return;
                }

            Content = new CallbackMessage("Processing Build Strategies...");
            try
                {
                token.ThrowIfCancellationRequested();
                var compiledStrategy = BuildStrategyScanner.LoadCompiledStrategy(srcPath, strategies, token);
                log.Info($"Compiled Strategy contains {compiledStrategy.PartStrategies.Count} PartStrategies, {compiledStrategy.LocalRepositories.Count} LocalRepositories.");
                var dt = compiledStrategy.DefaultTarget;
                if (dt == null)
                    {
                    Content = new CallbackMessage("Strategy has no default target.");
                    return;
                    }

                if (string.IsNullOrEmpty(dt.PartFile) || string.IsNullOrEmpty(dt.Repository))
                    {
                    Content = new CallbackMessage("Default Target requires a PartFile and Repository attribute.");
                    return;
                    }

                log.Info($"Default Target '{dt.PartName}' in File '{dt.PartFile}' Repository '{dt.Repository}'");
                InitializePartVMs(compiledStrategy, srcPath, token);
                }
            catch (UserfriendlyException ue)
                {
                Content = new CallbackMessage(ue.Message);
                }
            catch(Exception e)
                {
                Content = new CallbackMessage("Could not load parts. " + e.Message);
                }
            }

        private void InitializePartVMs (CompiledBuildStrategy buildStrategy, string srcPath, CancellationToken token)
            {
            Content = new CallbackMessage("Processing Parts...");
            var defaultPartFile = PartFileScanner.LoadPartFile(buildStrategy.DefaultTarget.PartFile, buildStrategy.DefaultTarget.Repository, 
                buildStrategy.LocalRepositories, srcPath);

            BuildFromSource bfs = buildStrategy.GetBuildFromSource(buildStrategy.DefaultTarget.PartFile);
            var products = defaultPartFile.Products.Select(p =>
            {
                return new { VM = new PartExplorerElementVM(_configuration) { Name = p.Name, PartType = PartType.Product, FromSource = bfs } , Product = p };
            }).ToList();
            var productVMsByName = products.ToDictionary(a => a.Product.Name, a => a.VM, StringComparer.OrdinalIgnoreCase);
            
            var parts = defaultPartFile.Parts.Select(p =>
            {
                return new { VM = new PartExplorerElementVM(_configuration) { Name = p.Name, MakeFile = p.MakeFile, FromSource = bfs }, Part = p };
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
                part.VM.PartType = DeterminePartType(part.VM, defaultPartFile.Directory, srcPath);
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
                    var bfs = buildStrategy.GetBuildFromSource(key.Name);
                    if(bfs == BuildFromSource.Never)
                        {
                        externalPartFile = new ExternalPartFile() { File = null, Key = key, BuildFromSourceFlag = bfs };
                        }
                    else
                        {
                        var epf = PartFileScanner.LoadPartFile(key.Name, key.Repository,
                        buildStrategy.LocalRepositories, srcPath);

                        if (epf == null)
                            throw new UserfriendlyException($"Failed to load part file {key.Name} in repository {key.Repository}");
                        externalPartFile = new ExternalPartFile() { File = epf, Key = key, BuildFromSourceFlag = bfs };
                        externalPartFile.RelativePath = $"{key.Repository}/{key.Name}";
                        }
                    
                    externalPartFiles.Add(key, externalPartFile);
                    }

                PartExplorerElementVM subPartVM = LoadExternalSubpartVM(externalSubparts, sp, externalPartFile, srcPath);

                currentPart.Item1.AddChild(subPartVM);
                }
            }

        private PartExplorerElementVM LoadExternalSubpartVM (Queue<Tuple<PartExplorerElementVM, SubPart>> externalSubparts, SubPart sp, ExternalPartFile externalPartFile, string srcDir)
            {
            PartExplorerElementVM subPartVM;
            if (externalPartFile.LoadedParts.TryGetValue(sp.Name, out subPartVM))
                return subPartVM;

            Part newSubPart = null;
            if (externalPartFile.BuildFromSourceFlag == BuildFromSource.Never)
                {
                subPartVM = new PartExplorerElementVM(_configuration)
                    {
                    Name = sp.Name,
                    Repository = externalPartFile.Key.Repository,
                    PartFile = externalPartFile.Key.Name,
                    RelativePath = externalPartFile.RelativePath,
                    FromSource = externalPartFile.BuildFromSourceFlag,
                    PartType = PartType.Reference
                    };
                }
            else
                {
                newSubPart = externalPartFile.File.Parts.FirstOrDefault(p => string.Equals(p.Name, sp.Name, StringComparison.OrdinalIgnoreCase));
                if (newSubPart == null)
                    {
                    throw new UserfriendlyException($"Failed to find part {sp.Name} in partfile {sp.PartFile}");
                    }

                subPartVM = new PartExplorerElementVM(_configuration)
                    {
                    Name = sp.Name,
                    Repository = externalPartFile.Key.Repository,
                    PartFile = externalPartFile.Key.Name,
                    RelativePath = externalPartFile.RelativePath,
                    MakeFile = newSubPart.MakeFile,
                    FromSource = externalPartFile.BuildFromSourceFlag,
                    };

                subPartVM.PartType = DeterminePartType(subPartVM, externalPartFile.File.Directory, srcDir);
                }

            externalPartFile.LoadedParts.Add(sp.Name, subPartVM);

            if(newSubPart != null)
                AddChildVMs(externalSubparts, externalPartFile, subPartVM, newSubPart, srcDir);

            return subPartVM;
            }

        private PartType DeterminePartType (PartExplorerElementVM subPartVM, string repositorySrcDir, string srcDir)
            {
            if (string.IsNullOrWhiteSpace(subPartVM.MakeFile))
                return PartType.Group;

            var makeFile = subPartVM.MakeFile;
            if (!makeFile.EndsWith(".mke", StringComparison.OrdinalIgnoreCase))
                return PartType.Unknown;

            string fullPath;
            if(makeFile.StartsWith("${SrcRoot}", StringComparison.OrdinalIgnoreCase))
                {
                fullPath = Path.Combine(srcDir, makeFile.Substring(10));
                }
            else
                {
                fullPath = Path.Combine(repositorySrcDir, makeFile);
                }

            return MakeFileScanner.GuessPartTypeFromMakeFile(fullPath);
            }

        private void AddChildVMs (Queue<Tuple<PartExplorerElementVM, SubPart>> externalSubparts, ExternalPartFile externalPartFile, PartExplorerElementVM currentVM, Part currentPart, string srcDir)
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
                    vm = LoadExternalSubpartVM(externalSubparts, subPart, externalPartFile, srcDir);
                    }

                currentVM.AddChild(vm);
                }
            }

        internal class ExternalPartFile
            {
            public IDictionary<string, PartExplorerElementVM> LoadedParts = new Dictionary<string, PartExplorerElementVM>(StringComparer.OrdinalIgnoreCase);
            public PartFile File;
            public PartFileKey Key;
            public BuildFromSource BuildFromSourceFlag = BuildFromSource.Never;
            public string RelativePath;
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
        Product,
        Reference
        }

    public class PartExplorerElementVM : ViewModelBase
        {
        private ConfigurationVM _configuration;

        public PartExplorerElementVM (ConfigurationVM _configuration)
            {
            this._configuration = _configuration;
            WireupCommands();
            }

        public bool HasParent { get; internal set; } = false;
        public PartType PartType { get; internal set; } = PartType.Unknown;
        public BuildFromSource FromSource { get; internal set; } = BuildFromSource.Never;
        public string Name { get; internal set; }
        public string MakeFile { get; set; } = null;
        public IList<PartExplorerElementVM> Children { get; } = new List<PartExplorerElementVM>();
        public string Repository { get; internal set; }
        public string PartFile { get; internal set; }
        public bool Disabled => FromSource == BuildFromSource.Never;
        
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

        private void WireupCommands ()
            {
            PinCommand.Handler = Pin;
            }

        public SimpleCommand PinCommand { get; } = new SimpleCommand();
        private void Pin (object parameter)
            {
            var c = _configuration;
            if (c == null)
                return;

            /*
            var collection = Parent.Configurations;
            lock (collection)
                {
                var index = collection.IndexOf(this);
                if (index <= 0)
                    return;

                var newIndex = index - 1;
                collection.RemoveAt(index);
                collection.Insert(newIndex, this);
                IsSelected = true;
                Parent.Parent.EnvironmentIsDirty();
                }*/
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
                    return "../Images/vs/emptypart16.png";
                case PartType.CSharp:
                    return "../Images/vs/cs16.png";
                case PartType.Cpp:
                    return "../Images/vs/cpp16.png";
                case PartType.Test:
                    return "../Images/vs/testproject16.png";
                case PartType.Product:
                    return "../Images/vs/product16.png";
                case PartType.Reference:
                    return "../Images/vs/reference16.png";
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
                case PartType.Reference:
                    return "Reference";
                default:
                    return "Uncategorized Part";
                }
            }

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
            {
            throw new NotSupportedException();
            }
        }

    public class CallbackMessage
        {
        private string _message = "";
        public CallbackMessage(string message)
            {
            if (!string.IsNullOrEmpty(message))
                _message = message;
            }

        public override string ToString ()
            {
            return _message;
            }
        }
    }
