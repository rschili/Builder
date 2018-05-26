using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shell;

namespace RSCoreLib.WPF
    {
    public class ProgressViewModel : ViewModelBase, IProgress<int>
        {
        public bool IsIndeterminate
            {
            get { return progressState == TaskbarItemProgressState.Indeterminate; }
            set
                {
                if(value)
                    ProgressState = TaskbarItemProgressState.Indeterminate;
                else
                    ProgressState = TaskbarItemProgressState.None;
                }
            }

        private TaskbarItemProgressState progressState = TaskbarItemProgressState.None;
        public TaskbarItemProgressState ProgressState
            {
            get
                {
                return progressState;
                }
            set
                {
                progressState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsIndeterminate));
                OnPropertyChanged(nameof(IsActive));
                ShortStatus = null;
                }
            }

        private int value = 0;
        public int Value
            {
            get
                {
                return value;
                }
            set
                {
                this.value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FractionValue));

                if (progressState != TaskbarItemProgressState.Normal)
                    ProgressState = TaskbarItemProgressState.Normal;
                }
            }

        public double FractionValue
            {
            get
                {
                return value / 100.0;
                }
            set
                {
                Report((int)(value * 100));
                }
            }

        public bool IsActive
            {
            get { return progressState != TaskbarItemProgressState.None; }
            set
                {
                if (IsActive == value)
                    return;

                if(value)
                    {
                    this.value = 0;
                    ProgressState = TaskbarItemProgressState.Normal;
                    OnPropertyChanged(nameof(Value));
                    OnPropertyChanged(nameof(FractionValue));
                    }
                else
                    {
                    ProgressState = TaskbarItemProgressState.None;
                    }
                }
            }

        public void Report (int value)
            {
            if (value > 100)
                value = 100;
            else if (value < 0)
                value = 0;

            Value = value;
            }

        public IDisposable ReportIndeterminate(string statusMessage = null)
            {
            StatusMessage = statusMessage;
            return new IndeterminateProgressHolder(this);
            }

        private string statusMessage = null;
        public string StatusMessage
            {
            get { return statusMessage; }
            set
                {
                statusMessage = value;
                OnPropertyChanged();
                }
            }

        private string shortStatus = null;
        public string ShortStatus
            {
            get { return shortStatus; }
            set
                {
                shortStatus = value;
                OnPropertyChanged();
                }
            }

        private string statusImage = null;
        public string StatusImage
            {
            get { return statusImage; }
            set
                {
                statusImage = value;
                OnPropertyChanged();
                }
            }
        }

    internal class IndeterminateProgressHolder : IDisposable
        {
        private ProgressViewModel vm;
        internal IndeterminateProgressHolder(ProgressViewModel vm)
            {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));

            this.vm = vm;
            vm.IsIndeterminate = true;
            }

        public void Dispose ()
            {
            vm.IsActive = false;
            }
        }
    }
