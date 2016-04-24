using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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
            //BuildCommand.Handler = p => Parent.Parent.Parent.RunOperation(Build, "Build", p);
            UnpinCommand.Handler = Unpin;
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

        public SimpleCommand BuildCommand { get; } = new SimpleCommand(true);
        public bool IsProduct { get; internal set; }

        private bool Build (CancellationToken cancellationToken, ProgressViewModel progress, object parameter)
            {
            var parent = Parent;
            if (parent == null)
                return false;

            return false;
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
