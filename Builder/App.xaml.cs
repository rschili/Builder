using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using RSCoreLib;
using log4net;

[assembly: log4net.Config.XmlConfigurator(Watch = false, ConfigFile = "Log4Net.config")]
namespace Builder
    {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
        {
        private TaskbarIcon notifyIcon;
        private Mutex singleInstanceMutex;
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        internal static readonly DateTime LinkerTime = typeof(App).Assembly.GetLinkerTime();

        protected override void OnStartup (StartupEventArgs e)
            {
            base.OnStartup(e);

            bool isSingleInstance = false;
            singleInstanceMutex = new Mutex(true, "BuilderSingleInstance", out isSingleInstance);
            if (!isSingleInstance)
                {
                MessageBox.Show("Another instance of this application is already running.", "Builder"); //Modal
                if (singleInstanceMutex != null)
                    singleInstanceMutex.Dispose();

                Shutdown();
                return;
                }

            try
                {
                log.InfoFormat("Started new Session. Builder {0}, compiled on {1}",
                    typeof(App).Assembly.GetName().Version.ToString(), LinkerTime.ToString("yyyy-MM-dd HH:mm:ss"));

                Stopwatch sw = new Stopwatch();
                sw.Start();
                var settings = Task.Run(() => AppDataManager.LoadSettings());
                var environments = Task.Run(() => AppDataManager.LoadEnvironments());

                Task.WaitAll(settings, environments);

                SettingsVM svm = CreateSettingsVM(settings.Result);
                var viewModel = new MainVM(svm, environments.Result);
                svm.RefreshAutomaticTCCPathAsync();

                Exit += (sender, _) => GracefulShutdown();
                SessionEnding += (sender, _) => GracefulShutdown();

                notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
                notifyIcon.DataContext = viewModel;

                sw.Stop();
                log.InfoFormat("Startup took {0:0.0} seconds.", sw.Elapsed.TotalSeconds);

                if (settings.Result != null && settings.Result.StartInTray)
                    return;

                sw.Restart();
                viewModel.ShowUICommand.Execute();
                sw.Stop();
                log.InfoFormat("Opening Main UI took {0:0.0} seconds.", sw.Elapsed.TotalSeconds);
                }
            catch (Exception ex)
                {
                log.Error("Exception during startup.", ex);
                Shutdown();
                }
            }

        private SettingsVM CreateSettingsVM (Settings s)
            {
            if (s == null)
                s = new Settings();

            return new SettingsVM(s);
            }

        private void GracefulShutdown ()
            {
            lock (this)
                {
                if (notifyIcon != null)
                    {
                    notifyIcon.Dispose(); //the icon would clean up automatically, but this is cleaner
                    notifyIcon = null;
                    }

                if (singleInstanceMutex != null)
                    {
                    singleInstanceMutex.Dispose();
                    singleInstanceMutex = null;
                    }
                }
            }
        }
    }
