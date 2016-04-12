using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Builder
    {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
        {
        public MainWindow ()
            {
            InitializeComponent();
            }

        private void StatusBarItemMouseDoubleClick (object sender, MouseButtonEventArgs e)
            {

            }

        private void MetroWindow_Closed (object sender, EventArgs e)
            {
            var vm = DataContext as MainVM;
            if (vm == null)
                return;

            vm.SaveEnvironmentsIfDirty();

            if (vm.SettingsVM != null && !vm.SettingsVM.CloseToTray)
                Application.Current.Shutdown();
            }

        private void TreeViewItem_MouseRightButtonDown (object sender, MouseButtonEventArgs e)
            {
            TreeViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
                {
                //treeViewItem.Focus();
                treeViewItem.IsSelected = true;
                e.Handled = true;
                }
            }

        static TreeViewItem VisualUpwardSearch (DependencyObject source)
            {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);

            return source as TreeViewItem;
            }

        private void TreeView_SelectedItemChanged (object sender, RoutedPropertyChangedEventArgs<object> e)
            {
            MainTree.Focus();
            }
        }

    public class ContextMenuItemContainerStyleSelector : StyleSelector
        {
        public Style RootStyle { get; set; }
        public Style BuildEnvironmentStyle { get; set; }

        public override Style SelectStyle (object item, DependencyObject container)
            {
            if (item is SourceDirectoryVM)
                return BuildEnvironmentStyle;

            return RootStyle;
            }
        }
    }
