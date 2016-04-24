using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using RSCoreLib.WPF;

namespace Builder
    {
    public class PartVM : ViewModelBase
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(PartVM));
        private readonly ConfigurationVM Parent;
        internal PinnedPart Model { get; private set; }

        public PartVM (ConfigurationVM parent) : this(parent, new PinnedPart())
            {
            }

        internal PartVM (ConfigurationVM parent, PinnedPart model)
            {
            Parent = parent;
            Model = model;
            //BuildCommand.Handler = p => Parent.Parent.Parent.RunOperation(Build, "Build", p);
            UnpinCommand.Handler = Unpin;
            }

        #region Properties
        public string Name
            {
            get { return Model.Name; }
            set
                {
                Model.Name = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }
        
        public bool IsDirty { get; set; } = false;
        
        #endregion

        public PartVM Copy ()
            {
            return null;
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
    }
