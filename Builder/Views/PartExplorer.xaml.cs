﻿using System;
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
    public partial class PartExplorer
        {
        public PartExplorer ()
            {
            InitializeComponent();
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
        }
    }
