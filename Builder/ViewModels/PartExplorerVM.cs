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
                log.Error($"Failed to load parts. {e.Message}.", e);
                }
            }

        private void InitializePartVMs (CompiledBuildStrategy buildStrategy, string srcPath, CancellationToken token)
            {
            Content = new CallbackMessage("Processing Parts...");
            var defaultPartFile = PartFileScanner.LoadPartFile(buildStrategy.DefaultTarget.PartFile, buildStrategy.DefaultTarget.Repository, 
                buildStrategy.LocalRepositories, srcPath);

            var products = defaultPartFile.Products.Select(p =>
            {
                return new { VM = new PartExplorerElementVM(_configuration) { Name = p.Name, PartType = PartType.Product,
                    FromSource = buildStrategy.GetBuildFromSource(buildStrategy.DefaultTarget.PartFile, p.Name)
                    } , Product = p };
            }).ToList();
            var productVMsByName = new Dictionary<string, PartExplorerElementVM>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in products)
                productVMsByName[a.Product.Name] = a.VM;
            
            var parts = defaultPartFile.Parts.Select(p =>
            {
                return new { VM = new PartExplorerElementVM(_configuration) { Name = p.Name, MakeFile = p.MakeFile,
                    FromSource = buildStrategy.GetBuildFromSource(buildStrategy.DefaultTarget.PartFile, p.Name)
                    }, Part = p };
            }).ToList();
            var partVMsByName = new Dictionary<string, PartExplorerElementVM>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in parts)
                partVMsByName[a.Part.Name] = a.VM;
            
            token.ThrowIfCancellationRequested();

            Queue<Tuple<PartExplorerElementVM, SubPart>> externalSubparts = new Queue<Tuple<PartExplorerElementVM, SubPart>>();
            foreach (var product in products)
                {
                foreach(var subPart in product.Product.SubParts)
                    {
                    PartExplorerElementVM vm;
                    if(subPart.IsProduct)
                        {
                        if (productVMsByName.TryGetValue(subPart.Name, out vm) && vm != null)
                            {
                            product.VM.AddChild(vm);
                            continue;
                            }
                        }
                    else if (partVMsByName.TryGetValue(subPart.Name, out vm) && vm != null)
                        {
                        product.VM.AddChild(vm);
                        continue;
                        }

                    externalSubparts.Enqueue(new Tuple<PartExplorerElementVM, SubPart>(product.VM, subPart));
                    }
                }

            foreach (var part in parts)
                {
                part.VM.PartType = DeterminePartType(part.VM, false, defaultPartFile.Directory, srcPath);
                foreach (var subPart in part.Part.SubParts)
                    {
                    PartExplorerElementVM vm;
                    if (subPart.IsProduct)
                        {
                        if (productVMsByName.TryGetValue(subPart.Name, out vm) && vm != null)
                            {
                            part.VM.AddChild(vm);
                            continue;
                            }
                        }
                    else if (partVMsByName.TryGetValue(subPart.Name, out vm) && vm != null)
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

                var bfs = buildStrategy.GetBuildFromSource(sp.PartFile, sp.Name);
                if (bfs == BuildFromSource.Never)
                    {
                    var earlyResult = new PartExplorerElementVM(_configuration)
                        {
                        Name = sp.Name,
                        Repository = sp.Repository,
                        PartFile = sp.PartFile,
                        FromSource = BuildFromSource.Never,
                        PartType = PartType.Reference
                        };

                    currentPart.Item1.AddChild(earlyResult);
                    continue;
                    }

                var key = new PartFileKey() { Name = sp.PartFile, Repository = sp.Repository };
                ExternalPartFile externalPartFile;
                if (!externalPartFiles.TryGetValue(key, out externalPartFile))
                    {
                    try
                        {
                        var epf = PartFileScanner.LoadPartFile(key.Name, key.Repository,
                        buildStrategy.LocalRepositories, srcPath);

                        if (epf == null)
                            throw new UserfriendlyException();
                        externalPartFile = new ExternalPartFile() { File = epf, Key = key };
                        }
                    catch (Exception e)
                        {
                        log.Error($"Part Explorer Tree will not be complete! Failed to load part file {key.Name} in repository {key.Repository}.", e);
                        //we don't consider this a failure, just act as if that part file was set to "never build"
                        externalPartFile = new ExternalPartFile() { File = null, Key = key };
                        }
                    
                    externalPartFiles.Add(key, externalPartFile);
                    }

                PartExplorerElementVM subPartVM = LoadExternalSubpartVM(externalSubparts, sp, externalPartFile, srcPath, bfs, buildStrategy);
                currentPart.Item1.AddChild(subPartVM);
                }
            }

        private PartExplorerElementVM LoadExternalSubpartVM (Queue<Tuple<PartExplorerElementVM, SubPart>> externalSubparts, SubPart sp, ExternalPartFile externalPartFile, string srcDir, BuildFromSource bfs, CompiledBuildStrategy strategy)
            {
            PartExplorerElementVM subPartVM;
            if (externalPartFile.LoadedParts.TryGetValue(sp.Name, out subPartVM))
                return subPartVM;

            Part newSubPart = null;
            if(sp.IsProduct)
                newSubPart = externalPartFile.File.Products.FirstOrDefault(p => string.Equals(p.Name, sp.Name, StringComparison.OrdinalIgnoreCase));
            else
                newSubPart = externalPartFile.File.Parts.FirstOrDefault(p => string.Equals(p.Name, sp.Name, StringComparison.OrdinalIgnoreCase));

            if (newSubPart == null)
                {
                log.Warn($"Failed to find part {sp.Name} in partfile {sp.PartFile}");
                newSubPart = new Part() { Name = sp.Name }; //use a dummy instead
                }

            subPartVM = new PartExplorerElementVM(_configuration)
                {
                Name = sp.Name,
                Repository = externalPartFile.Key.Repository,
                PartFile = externalPartFile.Key.Name,
                MakeFile = newSubPart.MakeFile,
                FromSource =bfs,
                };

            subPartVM.PartType = DeterminePartType(subPartVM, sp.IsProduct, externalPartFile.File.Directory, srcDir);

            externalPartFile.LoadedParts.Add(sp.Name, subPartVM);

            if(newSubPart != null)
                AddChildVMs(externalSubparts, externalPartFile, subPartVM, newSubPart, srcDir, strategy);

            return subPartVM;
            }

        private PartType DeterminePartType (PartExplorerElementVM subPartVM, bool isProduct, string repositorySrcDir, string srcDir)
            {
            if (isProduct)
                return PartType.Product;

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

        private void AddChildVMs (Queue<Tuple<PartExplorerElementVM, SubPart>> externalSubparts, ExternalPartFile externalPartFile, PartExplorerElementVM currentVM, 
            Part currentPart, string srcDir, CompiledBuildStrategy buildStrategy)
            {
            foreach (var subPart in currentPart.SubParts)
                {
                if (!string.IsNullOrEmpty(subPart.Repository) || !string.IsNullOrEmpty(subPart.PartFile))
                    {
                    externalSubparts.Enqueue(new Tuple<PartExplorerElementVM, SubPart>(currentVM, subPart));
                    continue;
                    }

                PartExplorerElementVM vm;
                if (!externalPartFile.LoadedParts.TryGetValue(subPart.Name, out vm))
                    {
                    var bfs = buildStrategy.GetBuildFromSource(subPart.PartFile ?? externalPartFile.Key.Name, subPart.Name);
                    if (bfs == BuildFromSource.Never)
                        {
                        vm = new PartExplorerElementVM(_configuration)
                            {
                            Name = subPart.Name,
                            Repository = subPart.Repository,
                            PartFile = subPart.PartFile,
                            FromSource = BuildFromSource.Never,
                            PartType = PartType.Reference
                            };
                        }
                    else
                        {
                        vm = LoadExternalSubpartVM(externalSubparts, subPart, externalPartFile, srcDir, bfs, buildStrategy);
                        }
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

        public string RelativePath
            {
            get
                {
                if (Repository != null || PartFile != null)
                    return $"{Repository}/{PartFile}";

                return null;
                }
            }  

        internal void AddChild (PartExplorerElementVM vm)
            {
            Children.Add(vm);
            vm.HasParent = true;
            }

        private void WireupCommands ()
            {
            PinCommand.Handler = Pin;
            BuildCommand.Handler = Build;
            BuildCommand.CanExecuteHandler = p => _configuration?.BuildCommand.CanExecute(p);

            RebuildCommand.Handler = Rebuild;
            BuildCommand.CanExecuteHandler = p => _configuration?.RebuildCommand.CanExecute(p);

            CleanCommand.Handler = Clean;
            BuildCommand.CanExecuteHandler = p => _configuration?.CleanCommand.CanExecute(p);
            }

        public SimpleCommand PinCommand { get; } = new SimpleCommand();
        private void Pin (object parameter)
            {
            var c = _configuration;
            if (c == null)
                return;

            PinnedPart pinnedPart = GenerateModel();
            c.AddPartCommand.Execute(pinnedPart);
            }

        private PinnedPart GenerateModel ()
            {
            return new PinnedPart()
                {
                Name = Name,
                Repository = Repository,
                PartFile = PartFile,
                PartType = PartType
                };
            }

        public SimpleCommand BuildCommand { get; } = new SimpleCommand(true);
        private void Build (object parameter)
            {
            _configuration?.BuildCommand?.Execute(GenerateModel());
            }

        public SimpleCommand RebuildCommand { get; } = new SimpleCommand(true);
        private void Rebuild (object parameter)
            {
            _configuration?.RebuildCommand?.Execute(GenerateModel());
            }

        public SimpleCommand CleanCommand { get; } = new SimpleCommand(true);
        private void Clean (object parameter)
            {
            _configuration?.CleanCommand?.Execute(GenerateModel());
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
