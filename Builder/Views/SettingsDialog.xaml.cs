using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Builder
    {
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog
        {
        public SettingsDialog ()
            {
            InitializeComponent();
            }

        private void Ok_Click (object sender, RoutedEventArgs e)
            {
            DialogResult = true;
            }

        private void BrowseAppData_Click (object sender, RoutedEventArgs e)
            {
            var directory = AppDataManager.GetAppDataPath();
            if(!Directory.Exists(directory))
                {
                MessageBox.Show(this, string.Format(CultureInfo.CurrentCulture, "The application data directory {0} does not exist. It has probably not been created, yet."));
                return;
                }

            Process.Start(directory);
            }
        }
    }
