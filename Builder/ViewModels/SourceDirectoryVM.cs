﻿using System;
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
                IsDirty = true;
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
                    Model.Configurations.Add(vm.RegenerateModel());
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
            MoveUpCommand.CanExecuteHandler = CanMoveUp;
            MoveDownCommand.Handler = MoveDown;
            MoveDownCommand.CanExecuteHandler = CanMoveDown;

            NavigateToCommand.Handler = NavigateTo;

            BootstrapCommand.Handler = p => Parent?.RunOperation(Bootstrap, "Bootstrap", p);
            BootstrapCommand.CanExecuteHandler = canExecuteHandler;

            AddConfigurationCommand.Handler = AddConfiguration;
            }

        public SimpleCommand AddConfigurationCommand { get; } = new SimpleCommand();
        private void AddConfiguration (object obj)
            {
            Window mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
                return;

            ConfigurationPropertiesDialog dialog = new ConfigurationPropertiesDialog();
            dialog.DataContext = new ConfigurationVM(this);
            dialog.Owner = mainWindow;
            if (dialog.ShowDialog() == true)
                {
                ConfigurationVM result = (ConfigurationVM)dialog.DataContext;

                Configurations.Insert(0, result);
                if (!IsExpanded)
                    IsExpanded = true;

                result.IsSelected = true;
                Parent.EnvironmentIsDirty();
                }
            }

        public SimpleCommand ShowPropertiesCommand { get; } = new SimpleCommand();
        private void ShowProperties (object obj)
            {
            if (Parent == null)
                return;

            Window mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
                return;

            SourceDirectoryPropertiesDialog dialog = new SourceDirectoryPropertiesDialog();
            dialog.DataContext = Copy();
            dialog.Owner = mainWindow;
            if (dialog.ShowDialog() == true)
                {
                SourceDirectoryVM result = (SourceDirectoryVM)dialog.DataContext;
                if (!result.IsDirty)
                    return;

                Model = result.Model;
                OnPropertyChanged(nameof(Alias));
                OnPropertyChanged(nameof(SrcPath));
                OnPropertyChanged(nameof(Stream));

                Parent.EnvironmentIsDirty();
                }
            }

        public SimpleCommand NavigateToCommand { get; } = new SimpleCommand();
        private void NavigateTo (object obj)
            {
            if (!ShellHelper.OpenDirectoryInExplorer(SrcPath))
                Parent.Progress.StatusMessage = $"'{SrcPath}' does not exist.";
            }

        public SimpleCommand DeleteCommand { get; } = new SimpleCommand();
        private void Delete (object obj)
            {
            if (Parent == null)
                return;

            var collection = Parent.SourceDirectories;

            if (MessageBox.Show($"Environment '{Alias}' will be removed. It will remain on disk.", "Confirmation", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
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
        private bool? CanMoveUp (object arg)
            {
            var collection = Parent.SourceDirectories;
            lock (collection)
                {
                var index = collection.IndexOf(this);
                return index > 0;
                }
            }

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
        private bool? CanMoveDown (object arg)
            {
            var collection = Parent.SourceDirectories;
            lock (collection)
                {
                var index = collection.IndexOf(this);
                return index < collection.Count - 1;
                }
            }
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
                if (MessageBox.Show($"This will bootstrap  stream '{Stream}' into directory '{path}'.", "Confirmation", MessageBoxButton.OKCancel, MessageBoxImage.Information)
                != MessageBoxResult.OK)
                    return OperationResult.Aborted;

                try
                    {
                    Directory.CreateDirectory(path);
                    }
                catch (Exception e)
                    {
                    MessageBox.Show($"Could not create '{path}'. {e.Message}", "Failed", MessageBoxButton.OK, MessageBoxImage.None);
                    return OperationResult.Failed;
                    }
                }
            else if (Directory.EnumerateFileSystemEntries(path).Any())
                {
                if (MessageBox.Show($"Directory '{path}' is not empty. All contents will be deleted before bootstrap. Please confirm.", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
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
                    MessageBox.Show($"Could not clear '{path}'. {e.Message}", "Failed", MessageBoxButton.OK, MessageBoxImage.None);
                    return OperationResult.Failed;
                    }
                }

            if (cancellationToken.IsCancellationRequested)
                return OperationResult.Aborted;

            return await Task.Run(() => JobImplementations.Bootstrap(cancellationToken, progress, path, Stream, Parent?.HistoryVM));
            }
        #endregion
        }
    }
