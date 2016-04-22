using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using log4net;
using RSCoreLib;
using RSCoreLib.WPF;

namespace Builder
    {
    public class MainVM : ViewModelBase
        {
        public ProgressViewModel Progress { get; } = new ProgressViewModel();
        public ObservableCollection<MainVM> RootNodes => new ObservableCollection<MainVM>() {this}; //Stupid but seems necessary for the top level nodes which only contain the root node
        public string RootHeader { get; } = "Environments on " + Environment.MachineName;
        public ObservableCollection<SourceDirectoryVM> SourceDirectories { get; } = new ObservableCollection<SourceDirectoryVM>();
        private static readonly ILog log = LogManager.GetLogger(typeof(MainVM));
        public HistoryVM HistoryVM { get; }

        public MainVM (SettingsVM settings, ICollection<SourceDirectory> sourceDirectories)
            {
            Guard.NotNull(settings);
            HistoryVM = new HistoryVM(this);
            SettingsVM = settings;
            WireupCommands();

            if (sourceDirectories != null)
                {
                foreach (var src in sourceDirectories)
                    {
                    SourceDirectories.Add(new SourceDirectoryVM(this, src));
                    }
                }

            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(SourceDirectories, SourceDirectories);
            ThemeHelper.ApplyTheme(SettingsVM.Theme);
            }

        public MainVM()
            {
            SourceDirectories.Add(new SourceDirectoryVM() { AliasEditable = "Hello", Stream="**stream**" });
            SourceDirectories.Add(new SourceDirectoryVM() { AliasEditable = "Foo", Stream = "bar" });
            SourceDirectories.Add(new SourceDirectoryVM() { AliasEditable = "Batman", Stream = "Superman" });
            }

        private void WireupCommands ()
            {
            ShowSettingsCommand.Handler = ShowSettings;
            ShowHistoryCommand.Handler = ShowHistory;
            ShowAboutCommand.Handler = ShowAbout;
            ShowUICommand.Handler = ShowMainUI;
            HideUICommand.Handler = HideMainUI;
            ExitCommand.Handler = Exit;

            ScanForEnvironmentsCommand.Handler = p => RunOperation(ScanForEnvironments, "Scan", p);
            ScanForEnvironmentsCommand.CanExecuteHandler = (_) => !OperationIsRunning;

            CancelCommand.Handler = Cancel;
            CancelCommand.CanExecuteHandler = (_) => OperationIsRunning && _cancellationSource != null;
            CancelCommand.Enabled = true;

            TestCommand.Handler = p => RunOperation(Test, "TEST", p);
            TestCommand.CanExecuteHandler = (_) => !OperationIsRunning;

            AddSourceDirectoryCommand.Handler = AddSourceDirectory;
            }

        #region Settings,History
        public SettingsVM SettingsVM { get; private set; }

        public SimpleCommand ShowSettingsCommand { get; } = new SimpleCommand();
        private void ShowSettings (object obj)
            {
            if (SettingsVM == null)
                return;

            SettingsDialog dialog = new SettingsDialog();
            dialog.DataContext = SettingsVM.Copy();
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
                {
                dialog.Owner = mainWindow;
                }

            if (dialog.ShowDialog() == true)
                {
                var result = (SettingsVM)dialog.DataContext;
                if (result == null || !result.IsDirty)
                    return;

                SettingsVM = result;
                ThemeHelper.ApplyTheme(result.Theme);
                AppDataManager.SaveSettings(result.Model);
                }
            }

        private HistoryWindow _historyWindow = null;
        public SimpleCommand ShowHistoryCommand { get; } = new SimpleCommand();
        private void ShowHistory (object obj)
            {
            lock(this)
                {
                HistoryWindow window = _historyWindow;
                if (window != null && window.IsVisible)
                    {
                    if (window.WindowState == WindowState.Minimized)
                        window.WindowState = WindowState.Normal;

                    window.Activate();
                    return;
                    }

                window = new HistoryWindow();
                window.DataContext = HistoryVM;
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                    {
                    window.Owner = mainWindow;
                    }
                window.Show();
                _historyWindow = window;
                }
            }

        public SimpleCommand ShowAboutCommand { get; } = new SimpleCommand();
        private void ShowAbout (object obj)
            {
            AboutWindow dialog = new AboutWindow();
            dialog.DataContext = new AboutVM();
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
                {
                dialog.Owner = mainWindow;
                }

            dialog.ShowDialog();
            }
        #endregion

        #region Show UI / Exit
        /// <summary>
        /// Opens the Main UI
        /// </summary>
        public SimpleCommand ShowUICommand { get; } = new SimpleCommand();
        public void ShowMainUI(object obj)
            {
            lock (this)
                {
                if (Application.Current.MainWindow == null)
                    {
                    var m = new MainWindow();
                    m.DataContext = this;
                    Application.Current.MainWindow = m;
                    m.Show();
                    }
                else
                    {
                    Application.Current.MainWindow.Activate();
                    }
                }
            }

        public SimpleCommand HideUICommand { get; } = new SimpleCommand();
        public void HideMainUI (object obj)
            {
            lock (this)
                {
                if (Application.Current.MainWindow == null)
                    return;

                if(SettingsVM != null && SettingsVM.CloseToTray)
                    {
                    Application.Current.MainWindow.Close();
                    }
                }
            }

        public SimpleCommand ExitCommand { get; } = new SimpleCommand();
        public void Exit(object obj)
            {
            SaveEnvironmentsIfDirty();
            Application.Current.Shutdown(0);
            }
        #endregion

        #region Root Commands
        public SimpleCommand ScanForEnvironmentsCommand { get; } = new SimpleCommand();
        private async Task<OperationResult> ScanForEnvironments (CancellationToken cancellationToken, ProgressViewModel progress, object parameter)
            {
            progress.IsIndeterminate = true;
            var result = await SourceDirectoryScanner.ScanMachine(cancellationToken, progress);
            if (result == null || result.Count == 0)
                return OperationResult.Finished;

            return await Task.Run(() => UpdateSourceDirectoryViewModels(result));
            }

        private OperationResult UpdateSourceDirectoryViewModels (ICollection<SourceDirectory> updateSource)
            {
            bool save = false;
            lock (SourceDirectories)
                {
                foreach (var foundDirectory in updateSource)
                    {
                    SourceDirectoryVM vm = SourceDirectories.FirstOrDefault(e => PathHelper.PointsToSameDirectory(e.SrcPath, foundDirectory.SrcPath));
                    if(vm == null)
                        {
                        vm = new SourceDirectoryVM(this, foundDirectory);
                        SourceDirectories.Add(vm);
                        save = true;
                        continue;
                        }

                    if (vm.MergeConfigurationViewModels(foundDirectory.Configurations) == OperationResult.Success)
                        save = true;
                    }

                if(save)
                    {
                    EnvironmentIsDirty();
                    }
                }

            return save ? OperationResult.Success : OperationResult.Finished;
            }

        private bool _environmentIsDirty = false;
        public void EnvironmentIsDirty ()
            {
            _environmentIsDirty = true;
            }

        public void SaveEnvironmentsIfDirty()
            {
            if (!_environmentIsDirty)
                return;

            _environmentIsDirty = false;

            lock (SourceDirectories)
                {
                //rebuild the model tree
                AppDataManager.SaveEnvironments(SourceDirectories.Select(vm => vm.RegenerateModel()).ToList());
                }
            }

        public SimpleCommand TestCommand { get; } = new SimpleCommand();
        private static async Task<OperationResult> Test (CancellationToken cancellationToken, ProgressViewModel progress, object parameter)
            {
            for (int i = 0; i < 10; i++)
                {
                cancellationToken.ThrowIfCancellationRequested();
                progress.Value = i * 10;
                progress.ShortStatus = i.ToString();
                await Task.Delay(500);
                }

            Random success = new Random();
            return success.Next(0, 10) >= 5 ? OperationResult.Finished : OperationResult.Failed;
            }

        public SimpleCommand AddSourceDirectoryCommand { get; } = new SimpleCommand();
        private void AddSourceDirectory (object obj)
            {
            Window mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
                return;

            SourceDirectoryPropertiesDialog dialog = new SourceDirectoryPropertiesDialog();
            dialog.DataContext = new SourceDirectoryVM(this);
            dialog.Owner = mainWindow;
            if (dialog.ShowDialog() == true)
                {
                SourceDirectoryVM result = (SourceDirectoryVM)dialog.DataContext;

                SourceDirectories.Insert(0, result);
                result.IsSelected = true;
                EnvironmentIsDirty();
                }
            }
        #endregion

        #region Operation Execution management
        public bool OperationIsRunning => Progress != null ? Progress.IsActive : false;

        public void RunOperation (Func<CancellationToken, ProgressViewModel, object, Task<OperationResult>> operation, string prefix, object parameter)
            {
            RunOperationAsync(operation, prefix, parameter).SwallowAndLogExceptions();
            }

        public async Task RunOperationAsync (Func<CancellationToken, ProgressViewModel, object, Task<OperationResult>> operation, string prefix, object parameter)
            {
            lock (this)
                {
                if (OperationIsRunning)
                    return;

                _cancellationSource = new CancellationTokenSource();
                Progress.IsActive = true;
                CommandManager.InvalidateRequerySuggested();
                }

            CancellationToken cancellationToken = _cancellationSource.Token;
            try
                {
                Progress.StatusMessage = prefix + STATUS_STARTED_SUFFIX;
                var result = await operation(cancellationToken, Progress, parameter);

                if(result == OperationResult.Success)
                    {
                    Progress.StatusMessage = prefix + STATUS_SUCCESS_SUFFIX;
                    return;
                    }

                if (result == OperationResult.Finished)
                    {
                    Progress.StatusMessage = prefix + STATUS_FINISHED_SUFFIX;
                    return;
                    }

                if (result == OperationResult.Aborted)
                    {
                    Progress.StatusMessage = prefix + STATUS_CANCELLED_SUFFIX;
                    return;
                    }

                Progress.StatusMessage = STATUS_BUILD_PREFIX + STATUS_FAILED_SUFFIX;
                }
            catch (Exception e) when (e is OperationCanceledException ||
                (e is AggregateException && ((AggregateException)e).Flatten().InnerExceptions.Any(i => i is OperationCanceledException)))
                {
                Progress.StatusMessage = prefix + STATUS_CANCELLED_SUFFIX;
                }
            catch (Exception e)
                {
                if (cancellationToken.IsCancellationRequested)
                    {
                    Progress.StatusMessage = prefix + STATUS_CANCELLED_SUFFIX;
                    }
                else
                    {
                    Progress.StatusMessage = prefix + STATUS_EXCEPTION_SUFFIX;
                    log.Error("Exception during operation", e);
                    }
                }
            finally
                {
                lock (this)
                    {
                    Progress.IsActive = false;
                    _cancellationSource?.Dispose();
                    _cancellationSource = null;
                    CommandManager.InvalidateRequerySuggested();
                    }
                }
            }

        public SimpleCommand CancelCommand { get; } = new SimpleCommand(false);
        private CancellationTokenSource _cancellationSource = null;
        private void Cancel (object obj)
            {
            lock (this)
                {
                var source = _cancellationSource;
                if (source != null && !source.IsCancellationRequested)
                    {
                    Progress.StatusMessage = "Cancelling...";
                    Task.Run(() => source.Cancel());
                    }
                }
            }
        #endregion
        
        public const string STATUS_READY = "Ready";
        public const string STATUS_FAILED_SUFFIX = " failed";
        public const string STATUS_STARTED_SUFFIX = " started...";
        public const string STATUS_SUCCESS_SUFFIX = " succeeded";
        public const string STATUS_FINISHED_SUFFIX = " finished";
        public const string STATUS_EXCEPTION_SUFFIX = " threw exception";
        public const string STATUS_CANCELLED_SUFFIX = " cancelled";

        public const string STATUS_BUILD_PREFIX = "Build";
        public const string STATUS_REBUILD_PREFIX = "Rebuild";
        public const string STATUS_CLEAN_PREFIX = "Clean";
        public const string STATUS_PUSH_PREFIX = "Push";
        public const string STATUS_PULL_PREFIX = "Pull";
        }

    public enum OperationResult
        {
        Success,
        Failed,
        Finished,
        Aborted
        }
    }
