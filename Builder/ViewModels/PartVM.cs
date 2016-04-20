using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using RSCoreLib.WPF;
using static Builder.PartExplorerVM;

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

        private bool Build (CancellationToken cancellationToken, ProgressViewModel progress, object parameter)
            {
            var parent = this.Parent;
            if (parent == null)
                return false;

            return false;
            }

        private void ProcessLine (string l, StreamWriter sw, ProgressViewModel progress, ref int partCount, ref int partsHandled)
            {
            if (l.StartsWith("Starting part"))
                {
                if (partCount < 0)
                    {
                    var match = Regex.Match(l, @"^Starting\spart\s\(\d+\/(?<PartCount>\d+)\)");
                    int count;
                    if (match.Success && int.TryParse(match.Groups["PartCount"].Value, out count) && count > 0)
                        {
                        partCount = count;
                        }
                    }

                return;
                }

            if (l.StartsWith("Running parts"))
                {
                if (partCount <= 0)
                    return;

                var match = Regex.Match(l, @"^Running\sparts\s\((?<PartCount>\d+)\sremain");
                int count;
                if (match.Success && int.TryParse(match.Groups["PartCount"].Value, out count) && count > 0)
                    {
                    var percent = 1.0 - ((double)count / partCount);
                    progress.Value = (int)(percent * 100);
                    }

                return;
                }

            sw.WriteLine(l);
            }
        }
    }
