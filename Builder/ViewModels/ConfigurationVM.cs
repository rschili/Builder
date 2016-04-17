using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using log4net;
using RSCoreLib.WPF;

namespace Builder
    {
    public class ConfigurationVM : ViewModelBase
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConfigurationVM));
        public readonly SourceDirectoryVM Parent;
        public Configuration Model { get; private set; }
        public ObservableCollection<PartVM> PinnedParts { get; } = new ObservableCollection<PartVM>();

        public ConfigurationVM (SourceDirectoryVM parent) : this(parent, new Configuration())
            {
            }

        public ConfigurationVM (SourceDirectoryVM parent, Configuration model)
            {
            Parent = parent;
            Model = model;
            WireupCommands();

            if (model.PinnedParts != null)
                {
                foreach (var part in model.PinnedParts)
                    {
                    PinnedParts.Add(new PartVM(this, part));
                    }
                }

            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(PinnedParts, PinnedParts);
            }

        public ConfigurationVM()
            {
            Model = new Configuration();
            Model.Alias = "Foo";
            Model.OutPath = "C:\\Out\\";
            Model.BuildStrategy = "buildstrat";
            PinnedParts.Add(new PartVM(this) { Name = "Part1" });
            PinnedParts.Add(new PartVM(this) { Name = "Part2" });
            }
        

        #region Properties
        public string Alias
            {
            get
                {
                var alias = Model.Alias;
                if (string.IsNullOrEmpty(alias))
                    return Model.BuildStrategy;

                return alias;
                }
            }

        public string AliasEditable
            {
            get
                {
                return Model.Alias;
                }
            set
                {
                if (string.IsNullOrWhiteSpace(value) || string.Equals(value, Model.BuildStrategy, StringComparison.OrdinalIgnoreCase))
                    Model.Alias = null;
                else
                    Model.Alias = value;

                IsDirty = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Alias));
                }
            }
        
        public string OutPath
            {
            get
                {
                return Model.OutPath;
                }

            set
                {
                Model.OutPath = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }
        
        public string BuildStrategy
            {
            get
                {
                return Model.BuildStrategy;
                }

            set
                {
                Model.BuildStrategy = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }

        public bool Release
            {
            get
                {
                return Model.Release;
                }

            set
                {
                Model.Release = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }
        
        private uint _outgoing = 0;
        public uint Outgoing
            {
            get
                {
                return _outgoing;
                }

            set
                {
                _outgoing = value;
                OnPropertyChanged();
                }
            }

        private uint _incoming = 0;
        public uint Incoming
            {
            get
                {
                return _incoming;
                }

            set
                {
                _incoming = value;
                OnPropertyChanged();
                }
            }

        private uint _localChanges = 0;
        public uint LocalChanges
            {
            get
                {
                return _localChanges;
                }

            set
                {
                _localChanges = value;
                OnPropertyChanged();
                }
            }

        private bool _isSelected = false;
        public bool IsSelected
            {
            get { return _isSelected; }
            set
                {
                _isSelected = value;
                OnPropertyChanged();
                }
            }

        public string ShellCommands
            {
            get { return Model.ShellCommands; }
            set
                {
                Model.ShellCommands = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }

        public bool IsDirty { get; set; } = false;
        
        #endregion

        public ConfigurationVM Copy ()
            {
            return new ConfigurationVM(Parent, Model.Copy());
            }

        private void WireupCommands ()
            {
            Func<object, bool?> canExecuteHandler = (_) => !Parent?.Parent?.OperationIsRunning;

            ShowPropertiesCommand.Handler = ShowProperties;

            BuildCommand.Handler = p => Parent.Parent.RunOperation(Build, "Build", p);
            BuildCommand.CanExecuteHandler = canExecuteHandler;

            RebuildCommand.Handler = p => Parent.Parent.RunOperation(Rebuild, "Rebuild " + this.Alias, p);
            RebuildCommand.CanExecuteHandler = canExecuteHandler;

            CleanCommand.Handler = p => Parent.Parent.RunOperation(Clean, "Clean", p);
            CleanCommand.CanExecuteHandler = canExecuteHandler;

            DeleteCommand.Handler = Delete;
            OpenShellCommand.Handler = OpenShell;
            MoveUpCommand.Handler = MoveUp;
            MoveDownCommand.Handler = MoveDown;
            NavigateToCommand.Handler = NavigateTo;
            }

        public SimpleCommand ShowPropertiesCommand { get; } = new SimpleCommand(true);

        private void ShowProperties (object obj)
            {
            if (Parent == null)
                return;

            Window mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
                return;

            ConfigurationPropertiesDialog dialog = new ConfigurationPropertiesDialog();
            dialog.DataContext = Copy();
            dialog.Owner = mainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            using (Parent.Parent.Progress.ReportIndeterminate())
                {
                if (dialog.ShowDialog() == true)
                    {
                    ConfigurationVM result = (ConfigurationVM)dialog.DataContext;
                    if (!result.IsDirty)
                        return;

                    var oldModel = Model;
                    Model = result.Model;
                    OnPropertyChanged(nameof(Alias));
                    OnPropertyChanged(nameof(OutPath));
                    OnPropertyChanged(nameof(BuildStrategy));
                    OnPropertyChanged(nameof(Release));

                    Parent.Parent.EnvironmentIsDirty();
                    }
                }
            }

        public SimpleCommand DeleteCommand { get; } = new SimpleCommand(true);
        private void Delete (object obj)
            {
            if (Parent == null)
                return;

            var collection = Parent.Configurations;

            if (obj == null) //dummy parameter is used to indicate "delete without asking"
                {
                if (MessageBox.Show($"Environment '{Alias}' will be removed. It will remain on disk.", "Confirmation", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                    != MessageBoxResult.OK)
                    return;
                }

            lock (collection)
                {
                if (collection.Remove(this))
                    {
                    Parent.Parent.EnvironmentIsDirty();
                    }
                }
            }

        public SimpleCommand NavigateToCommand { get; } = new SimpleCommand(true);
        private void NavigateTo (object obj)
            {
            if (!ShellHelper.OpenDirectoryInExplorer(OutPath))
                Parent.Parent.Progress.StatusMessage = $"'{OutPath}' does not exist.";
            }

        public SimpleCommand BuildCommand { get; } = new SimpleCommand(true);
        private async Task<OperationResult> Build (CancellationToken cancellationToken, ProgressViewModel progress, object parameter)
            {
            var parent = Parent;
            if (parent == null)
                return OperationResult.Failed;

            progress.IsIndeterminate = true;
            return await Task.Run(() => JobImplementations.Build(cancellationToken, progress, this));
            }

        public SimpleCommand RebuildCommand { get; } = new SimpleCommand(true);
        private async Task<OperationResult> Rebuild (CancellationToken cancellationToken, ProgressViewModel progress, object parameter)
            {
            var parent = Parent;
            if (parent == null)
                return OperationResult.Failed;

            progress.IsIndeterminate = true;
            return await Task.Run(() => JobImplementations.Rebuild(cancellationToken, progress, this));
            }

        public SimpleCommand CleanCommand { get; } = new SimpleCommand(true);
        private async Task<OperationResult> Clean (CancellationToken cancellationToken, ProgressViewModel progress, object parameter)
            {
            var parent = Parent;
            if (parent == null)
                return OperationResult.Failed;

            progress.IsIndeterminate = true;
            return await Task.Run(() => JobImplementations.Clean(cancellationToken, progress, this));
            }

        public SimpleCommand OpenShellCommand { get; } = new SimpleCommand(true);

        private void OpenShell (object parameter)
            {
            Task.Run(() =>
            {
                var bat = ShellHelper.GetSetupEnvScript(Parent.SrcPath, OutPath, BuildStrategy, Alias);
                ShellHelper.OpenNewEnvironmentShell(bat, Parent.SrcPath, Parent.Parent.SettingsVM.TCCLePathInternal);
            });
            }

        public SimpleCommand MoveUpCommand { get; } = new SimpleCommand(true);
        private void MoveUp (object parameter)
            {
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
                }
            }

        public SimpleCommand MoveDownCommand { get; } = new SimpleCommand(true);
        private void MoveDown (object parameter)
            {
            var collection = Parent.Configurations;
            lock (collection)
                {
                var index = collection.IndexOf(this);
                if (index < 0 || index >= (collection.Count - 1))
                    return;

                var newIndex = index + 1;
                collection.RemoveAt(index);
                collection.Insert(newIndex, this);
                IsSelected = true;
                Parent.Parent.EnvironmentIsDirty();
                }
            }
        }
    }
