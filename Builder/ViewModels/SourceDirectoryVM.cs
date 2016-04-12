using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using log4net;
using RSCoreLib;
using RSCoreLib.WPF;

namespace Builder
    {
    public class SourceDirectoryVM : ViewModelBase
        {
        public readonly MainVM Parent;
        private static readonly ILog log = LogManager.GetLogger(typeof(SourceDirectoryVM));
        public SourceDirectory Model { get; private set; }
        public ObservableCollection<ConfigurationVM> Configurations { get; } = new ObservableCollection<ConfigurationVM>();

        public SourceDirectoryVM (MainVM parent) : this(parent, new SourceDirectory())
            {
            }

        public SourceDirectoryVM (MainVM parent, SourceDirectory model)
            {
            Parent = parent;
            Model = model;
            WireupCommands();

            if (model.Configurations != null)
                {
                foreach (var conf in model.Configurations)
                    {
                    Configurations.Add(new ConfigurationVM(this, conf));
                    }
                }

            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Configurations, Configurations);
            }

        public SourceDirectoryVM ()
            {
            Model = new SourceDirectory();
            Model.Alias = "Foo";
            Model.SrcPath = @"C:\dev\something\";
            Model.Stream = "**Stream**";
            Model.IsExpanded = true;
            Configurations.Add(new ConfigurationVM());
            }

        #region Properties
        public string Alias
            {
            get
                {
                var alias = Model.Alias;
                if (string.IsNullOrEmpty(alias))
                    return Model.SrcPath;

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
                if (PathHelper.PointsToSameDirectory(value, Model.SrcPath))
                    Model.Alias = null;
                else
                    Model.Alias = value;

                IsDirty = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Alias));
                }
            }

        public string SrcPath
            {
            get
                {
                return Model.SrcPath;
                }

            set
                {
                if (!string.IsNullOrWhiteSpace(value))
                    value = PathHelper.EnsureTrailingDirectorySeparator(value);

                Model.SrcPath = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }

        public string Stream
            {
            get
                {
                return Model.Stream;
                }

            set
                {
                Model.Stream = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }

        public bool IsDirty { get; set; } = false;

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

        public bool IsExpanded
            {
            get { return Model.IsExpanded; }
            set
                {
                Model.IsExpanded = value;
                OnPropertyChanged();
                Parent?.EnvironmentIsDirty();
                }
            }

        public string ShellCommands
            {
            get { return Model.ShellCommands; }
            set
                {
                Model.ShellCommands = value;
                OnPropertyChanged();
                }
            }
        #endregion

        #region loose methods
        public SourceDirectoryVM Copy ()
            {
            return new SourceDirectoryVM(Parent, Model.Copy());
            }

        internal OperationResult MergeConfigurationViewModels (IList<Configuration> newConfigurations)
            {
            var result = OperationResult.Finished;
            lock (Configurations)
                {
                foreach (var c in newConfigurations)
                    {
                    ConfigurationVM vm = Configurations.FirstOrDefault(e => string.Equals(e.BuildStrategy, c.BuildStrategy, StringComparison.OrdinalIgnoreCase));
                    if (vm == null)
                        {
                        vm = new ConfigurationVM(this, c);
                        Configurations.Add(vm);
                        result = OperationResult.Success;
                        }
                    }
                }

            if (result == OperationResult.Success)
                RegenerateModel();

            return result;
            }

        internal SourceDirectory RegenerateModel ()
            {
            lock (Configurations)
                {
                Model.Configurations.Clear();
                foreach (var vm in Configurations)
                    Model.Configurations.Add(vm.Model);
                }

            return Model;
            }
        #endregion

        #region Commands
        private void WireupCommands ()
            {
            ShowPropertiesCommand.Handler = ShowProperties;
            Func<object, bool?> canExecuteHandler = (_) => !Parent?.OperationIsRunning;

            DeleteCommand.Handler = Delete;
            DeleteCommand.CanExecuteHandler = canExecuteHandler;

            MoveUpCommand.Handler = MoveUp;
            MoveDownCommand.Handler = MoveDown;

            NavigateToCommand.Handler = NavigateTo;

            BootstrapCommand.Handler = p => Parent?.RunOperation(Bootstrap, "Bootstrap", p);
            BootstrapCommand.CanExecuteHandler = canExecuteHandler;
            }

        public SimpleCommand ShowPropertiesCommand { get; } = new SimpleCommand();
        private void ShowProperties (object obj)
            {
            Window mainWindow = null;
            lock (Parent)
                {
                mainWindow = Application.Current.MainWindow;
                if (mainWindow == null)
                    return;

                if (Parent == null)
                    return;
                }

            SourceDirectoryPropertiesDialog dialog = new SourceDirectoryPropertiesDialog();
            dialog.DataContext = Copy();
            dialog.Owner = Application.Current.MainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (dialog.ShowDialog() == true)
                {
                SourceDirectoryVM result = (SourceDirectoryVM)dialog.DataContext;
                if (!result.IsDirty)
                    return;

                Model = result.Model;
                OnPropertyChanged(nameof(Alias));
                OnPropertyChanged(nameof(SrcPath));
                OnPropertyChanged(nameof(Stream));

                var collection = Parent.SourceDirectories;
                lock (collection)
                    {
                    Parent.EnvironmentIsDirty();
                    }
                }
            }

        public SimpleCommand NavigateToCommand { get; } = new SimpleCommand();
        private void NavigateTo (object obj)
            {
            if (!ShellHelper.OpenDirectoryInExplorer(SrcPath))
                Parent.Progress.StatusMessage = string.Format("'{0}' does not exist.", SrcPath);
            }

        public SimpleCommand DeleteCommand { get; } = new SimpleCommand();
        private void Delete (object obj)
            {
            if (Parent == null)
                return;

            var collection = Parent.SourceDirectories;

            if (MessageBox.Show(string.Format("Environment '{0}' will be removed. It will remain on disk.", Alias), "Confirmation", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                != MessageBoxResult.OK)
                return;

            lock (collection)
                {
                if (collection.Remove(this))
                    {
                    Parent.EnvironmentIsDirty();
                    }
                }
            }

        public SimpleCommand MoveUpCommand { get; } = new SimpleCommand();
        private void MoveUp (object parameter)
            {
            var collection = Parent.SourceDirectories;
            lock(collection)
                {
                var index = collection.IndexOf(this);
                if (index <= 0)
                    return;

                var newIndex = index - 1;
                collection.RemoveAt(index);
                collection.Insert(newIndex, this);
                IsSelected = true;
                Parent.EnvironmentIsDirty();
                }
            }

        public SimpleCommand MoveDownCommand { get; } = new SimpleCommand();
        private void MoveDown (object parameter)
            {
            var collection = Parent.SourceDirectories;
            lock (collection)
                {
                var index = collection.IndexOf(this);
                if (index < 0 || index >= (collection.Count - 1))
                    return;

                var newIndex = index + 1;
                collection.RemoveAt(index);
                collection.Insert(newIndex, this);
                IsSelected = true;
                Parent.EnvironmentIsDirty();
                }
            }

        public SimpleCommand BootstrapCommand { get; } = new SimpleCommand();
        private async Task<OperationResult> Bootstrap (CancellationToken cancellationToken, ProgressViewModel progress, object parameter)
            {
            progress.IsIndeterminate = true;
            if (string.IsNullOrWhiteSpace(SrcPath))
                {
                MessageBox.Show("There is no valid path specified for this source directory", "Builder", MessageBoxButton.OK, MessageBoxImage.None);
                return OperationResult.Failed;
                }

            if (string.IsNullOrWhiteSpace(Stream))
                {
                MessageBox.Show("There is no valid stream specified for this source directory", "Builder", MessageBoxButton.OK, MessageBoxImage.None);
                return OperationResult.Failed;
                }

            var path = Path.GetFullPath(SrcPath);
            if (!Directory.Exists(path))
                {
                if (MessageBox.Show(string.Format("This will bootstrap  stream '{0}' into directory '{1}'.", Stream, path), "Confirmation", MessageBoxButton.OKCancel, MessageBoxImage.Information)
                != MessageBoxResult.OK)
                    return OperationResult.Aborted;

                try
                    {
                    Directory.CreateDirectory(path);
                    }
                catch (Exception e)
                    {
                    MessageBox.Show(string.Format("Could not create '{0}'. {1}", path, e.Message, "Failed", MessageBoxButton.OK, MessageBoxImage.None));
                    return OperationResult.Failed;
                    }
                }
            else if (Directory.EnumerateFileSystemEntries(path).Any())
                {
                if (MessageBox.Show(string.Format("Directory '{0}' is not empty. All contents will be deleted before bootstrap. Please confirm.", path), "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                    != MessageBoxResult.OK)
                    return OperationResult.Aborted;

                try
                    {
                    await Task.Run(() =>
                    {
                        Directory.Delete(path, true);
                        Directory.CreateDirectory(path);
                    });
                    }
                catch(Exception e)
                    {
                    MessageBox.Show(string.Format("Could not clear '{0}'. {1}", path, e.Message, "Failed", MessageBoxButton.OK, MessageBoxImage.None));
                    return OperationResult.Failed;
                    }
                }

            if (cancellationToken.IsCancellationRequested)
                return OperationResult.Aborted;

            return await Task.Run(() => JobImplementations.Bootstrap(cancellationToken, progress, path, Stream));
            }
        #endregion
        }
    }
