using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using log4net;
using RSCoreLib.WPF;

namespace Builder
    {
    public class PartVM : ViewModelBase
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(PartVM));
        private readonly ConfigurationVM Parent;
        internal PinnedPart Model { get; private set; }

        internal PartVM (ConfigurationVM parent, PinnedPart model)
            {
            Parent = parent;
            Model = model;
            WireupCommands();
            }

        public PartVM ()
            {
            Model = new PinnedPart()
                {
                Alias = "Alias",
                Name = "Name",
                PartFile = "PartFile",
                Repository = "Repo",
                PartType = PartType.CSharp
                };
            }

        #region Properties
        public string Alias
            {
            get
                {
                var alias = Model.Alias;
                if (string.IsNullOrEmpty(alias))
                    return Model.Name;

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
                if (string.IsNullOrWhiteSpace(value) || string.Equals(value, Model.Name, StringComparison.OrdinalIgnoreCase))
                    Model.Alias = null;
                else
                    Model.Alias = value;

                IsDirty = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Alias));
                }
            }

        public string Name
            {
            get { return Model.Name; }
            set
                {
                Model.Name = value;
                IsDirty = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Alias));
                OnPropertyChanged(nameof(AliasEditable));
                }
            }

        public string Repository
            {
            get { return Model.Repository; }
            set
                {
                if (string.IsNullOrEmpty(value))
                    Model.Repository = null;
                else
                    Model.Repository = value;

                IsDirty = true;
                OnPropertyChanged();
                }
            }

        public string PartFile
            {
            get { return Model.PartFile; }
            set
                {
                if (string.IsNullOrEmpty(value))
                    Model.PartFile = null;
                else
                    Model.PartFile = value;

                IsDirty = true;
                OnPropertyChanged();
                }
            }

        public PartType PartType
            {
            get { return Model.PartType; }
            set
                {
                Model.PartType = value;

                IsDirty = true;
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

        public bool IsDirty { get; set; } = false;
        #endregion

        public PartVM Copy ()
            {
            return new PartVM(Parent, Model.Copy());
            }

        private void WireupCommands ()
            {
            UnpinCommand.Handler = Unpin;

            MoveUpCommand.Handler = MoveUp;
            MoveUpCommand.CanExecuteHandler = CanMoveUp;
            MoveDownCommand.Handler = MoveDown;
            MoveDownCommand.CanExecuteHandler = CanMoveDown;

            BuildCommand.Handler = Build;
            BuildCommand.CanExecuteHandler = p => Parent?.BuildCommand.CanExecute(p);

            RebuildCommand.Handler = Rebuild;
            BuildCommand.CanExecuteHandler = p => Parent?.RebuildCommand.CanExecute(p);

            CleanCommand.Handler = Clean;
            BuildCommand.CanExecuteHandler = p => Parent?.CleanCommand.CanExecute(p);

            ShowPropertiesCommand.Handler = ShowProperties;
            }

        public SimpleCommand UnpinCommand { get; } = new SimpleCommand(true);
        private void Unpin (object obj)
            {
            if (Parent == null)
                return;

            var collection = Parent.PinnedParts;

            lock (collection)
                {
                if (collection.Remove(this))
                    {
                    Parent.Parent.Parent.EnvironmentIsDirty();
                    }
                }
            }

        public SimpleCommand MoveUpCommand { get; } = new SimpleCommand();
        private bool? CanMoveUp (object arg)
            {
            var collection = Parent.PinnedParts;
            lock (collection)
                {
                var index = collection.IndexOf(this);
                return index > 0;
                }
            }
        private void MoveUp (object parameter)
            {
            var collection = Parent.PinnedParts;
            lock (collection)
                {
                var index = collection.IndexOf(this);
                if (index <= 0)
                    return;

                var newIndex = index - 1;
                collection.RemoveAt(index);
                collection.Insert(newIndex, this);
                IsSelected = true;
                Parent?.Parent?.Parent?.EnvironmentIsDirty();
                }
            }

        public SimpleCommand MoveDownCommand { get; } = new SimpleCommand();
        private bool? CanMoveDown (object arg)
            {
            var collection = Parent.PinnedParts;
            lock (collection)
                {
                var index = collection.IndexOf(this);
                return index < collection.Count - 1;
                }
            }

        private void MoveDown (object parameter)
            {
            var collection = Parent.PinnedParts;
            lock (collection)
                {
                var index = collection.IndexOf(this);
                if (index < 0 || index >= (collection.Count - 1))
                    return;

                var newIndex = index + 1;
                collection.RemoveAt(index);
                collection.Insert(newIndex, this);
                IsSelected = true;
                Parent?.Parent?.Parent?.EnvironmentIsDirty();
                }
            }

        public SimpleCommand BuildCommand { get; } = new SimpleCommand(true);
        private void Build (object parameter)
            {
            Parent?.BuildCommand?.Execute(Model);
            }

        public SimpleCommand RebuildCommand { get; } = new SimpleCommand(true);
        private void Rebuild (object parameter)
            {
            Parent?.RebuildCommand?.Execute(Model);
            }

        public SimpleCommand CleanCommand { get; } = new SimpleCommand(true);
        private void Clean (object parameter)
            {
            Parent?.CleanCommand?.Execute(Model);
            }

        public SimpleCommand ShowPropertiesCommand { get; } = new SimpleCommand(true);
        private void ShowProperties (object obj)
            {
            if (Parent == null)
                return;

            Window mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
                return;

            PartPropertiesDialog dialog = new PartPropertiesDialog();
            dialog.DataContext = Copy();
            dialog.Owner = mainWindow;
            if (dialog.ShowDialog() == true)
                {
                PartVM result = (PartVM)dialog.DataContext;
                if (!result.IsDirty)
                    return;

                var oldModel = Model;
                Model = result.Model;
                OnPropertyChanged(nameof(Alias));
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(PartType));
                OnPropertyChanged(nameof(Repository));
                OnPropertyChanged(nameof(PartFile));

                Parent?.Parent?.Parent?.EnvironmentIsDirty();
                }
            }
        }

    public static class PartTypeHelper
        {
        public static IEnumerable<PartType> PartTypes
            {
            get
                {
                return Enum.GetValues(typeof(PartType)).Cast<PartType>();
                }
            }
        }
    }
