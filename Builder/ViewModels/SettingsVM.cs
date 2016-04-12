using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using RSCoreLib;
using RSCoreLib.WPF;

namespace Builder
    {
    public class SettingsVM : ViewModelBase
        {
        public Settings Model { get; }

        public SettingsVM(Settings model)
            {
            Model = model;
            }

        public SettingsVM()
            {
            Model = new Settings();
            Model.CloseToTray = true;
            Model.StartInTray = false;
            Model.Theme = Theme.Version0_9;
            Model.TCCLeUsage = TCCLeUsage.Disabled;
            Model.ShellCommands = "set myvar=something";
            }


        public bool CloseToTray
            {
            get
                {
                return Model.CloseToTray;
                }
            set
                {
                Model.CloseToTray = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }

        public bool StartInTray
            {
            get
                {
                return Model.StartInTray;
                }
            set
                {
                Model.StartInTray = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }

        public Theme Theme
            {
            get
                {
                return Model.Theme;
                }
            set
                {
                Model.Theme = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }

        public TCCLeUsage TCCLeUsage
            {
            get
                {
                return Model.TCCLeUsage;
                }
            set
                {
                Model.TCCLeUsage = value;
                IsDirty = true;
                RefreshAutomaticTCCPathAsync();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TCCLePathEnabled));
                OnPropertyChanged(nameof(TCCLePath));
                }
            }

        internal void RefreshAutomaticTCCPathAsync ()
            {
            if (TCCLeUsage != TCCLeUsage.Automatic)
                return;

            Task.Run(() => RefreshAutomaticTCCPath()).SwallowAndLogExceptions();
            }

        private void RefreshAutomaticTCCPath()
            {
            if (File.Exists(TCCLePath))
                return;

            TCCLePath = ShellHelper.TryFindTCC();
            }

        public string TCCLePath
            {
            get
                {
                if (TCCLeUsage == TCCLeUsage.Disabled)
                    return "disabled";
                
                return Model.TCCLePath;
                }
            set
                {
                if (value == Model.TCCLePath)
                    return;

                Model.TCCLePath = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }

        public string TCCLePathInternal
            {
            get
                {
                if (TCCLeUsage == TCCLeUsage.Disabled)
                    return null;

                if (TCCLeUsage == TCCLeUsage.Automatic && string.IsNullOrEmpty(Model.TCCLePath))
                    return null;

                return Model.TCCLePath;
                }
            }

        public bool TCCLePathEnabled => TCCLeUsage == TCCLeUsage.Enabled;

        public string ShellCommands
            {
            get
                {
                return Model.ShellCommands;
                }
            set
                {
                Model.ShellCommands = value;
                IsDirty = true;
                OnPropertyChanged();
                }
            }

        public bool IsDirty { get; set; } = false;

        public SettingsVM Copy()
            {
            return new SettingsVM(Model.Copy());
            }
        }
    
    public static class TCCLeUsages
        {
        public static IEnumerable<TCCLeUsage> Values
            {
            get
                {
                return Enum.GetValues(typeof(TCCLeUsage)).Cast<TCCLeUsage>();
                }
            }
        }


    public sealed class TCCLeUsageToLabelConverter : IValueConverter
        {
        public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
            {
            if (!(value is TCCLeUsage))
                return string.Empty;

            TCCLeUsage t = (TCCLeUsage)value;
            if (t == TCCLeUsage.Automatic)
                return "Auto detect";

            if (t == TCCLeUsage.Disabled)
                return "Disabled";

            return "Enabled";
            }

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
            {
            throw new NotSupportedException();
            }
        }
    }
