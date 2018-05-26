using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace KLib.WPF
    {
    /// <summary>
    /// Interaction logic for TreeGrid.xaml
    /// </summary>
    public partial class TreeGrid : UserControl
        {
        public TreeGrid ()
            {
            InitializeComponent();
            }
        }

    //It is not possible to inherit a xaml control. We will use composition here instead, to emulate our TreeGrid. Internally it will feature the
    //TreeListView class mentioned below.
    #region Control Override
    public class TreeListView : TreeView
        {
        protected override DependencyObject GetContainerForItemOverride ()
            {
            return new TreeListViewItem();
            }

        protected override bool IsItemItsOwnContainerOverride (object item)
            {
            return item is TreeListViewItem;
            }

        protected override void OnPreviewMouseRightButtonDown (MouseButtonEventArgs e)
            {
            TreeListViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
                {
                treeViewItem.Focus();
                e.Handled = true;
                }

            base.OnPreviewMouseRightButtonDown(e);
            }

        static TreeListViewItem VisualUpwardSearch (DependencyObject source)
            {
            while (source != null && !(source is TreeListViewItem))
                source = VisualTreeHelper.GetParent(source);

            return source as TreeListViewItem;
            }

        }

    public class TreeListViewItem : TreeViewItem
        {
        private int m_level = -1;

        public int Level
            {
            get
                {
                if (m_level == -1)
                    {
                    TreeListViewItem parent = ItemsControl.ItemsControlFromItemContainer(this) as TreeListViewItem;
                    m_level = (parent != null) ? parent.Level + 1 : 0;
                    }

                return m_level;
                }
            }

        protected override DependencyObject GetContainerForItemOverride ()
            {
            return new TreeListViewItem();
            }

        protected override bool IsItemItsOwnContainerOverride (object item)
            {
            return item is TreeListViewItem;
            }
        }

    #endregion

    #region ScrollSynchronizer used to sync grid headers and tree scroll viewers
    public class ScrollSynchronizer : DependencyObject
        {
        public static readonly DependencyProperty ScrollGroupProperty =
        DependencyProperty.RegisterAttached(
            "ScrollGroup",
            typeof(string),
            typeof(ScrollSynchronizer),
            new PropertyMetadata(new PropertyChangedCallback(
            OnScrollGroupChanged)));

        public static void SetScrollGroup (DependencyObject obj, string scrollGroup)
            {
            obj.SetValue(ScrollGroupProperty, scrollGroup);
            }

        public static string GetScrollGroup (DependencyObject obj)
            {
            return (string)obj.GetValue(ScrollGroupProperty);
            }

        private static Dictionary<ScrollViewer, string> scrollViewers = new Dictionary<ScrollViewer, string>();
        private static Dictionary<string, double> horizontalScrollOffsets = new Dictionary<string, double>();
        private static Dictionary<string, double> verticalScrollOffsets = new Dictionary<string, double>();

        private static void OnScrollGroupChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
            var scrollViewer = d as ScrollViewer;
            if (scrollViewer != null)
                {
                if (!string.IsNullOrEmpty((string)e.OldValue))
                    {
                    // Remove scrollviewer
                    if (scrollViewers.ContainsKey(scrollViewer))
                        {
                        scrollViewer.ScrollChanged -=
                  new ScrollChangedEventHandler(ScrollViewer_ScrollChanged);
                        scrollViewers.Remove(scrollViewer);
                        }
                    }

                if (!string.IsNullOrEmpty((string)e.NewValue))
                    {
                    // If group already exists, set scrollposition of 
                    // new scrollviewer to the scrollposition of the group
                    if (horizontalScrollOffsets.Keys.Contains((string)e.NewValue))
                        {
                        scrollViewer.ScrollToHorizontalOffset(
                                      horizontalScrollOffsets[(string)e.NewValue]);
                        }
                    else
                        {
                        horizontalScrollOffsets.Add((string)e.NewValue,
                                                scrollViewer.HorizontalOffset);
                        }

                    if (verticalScrollOffsets.Keys.Contains((string)e.NewValue))
                        {
                        scrollViewer.ScrollToVerticalOffset(verticalScrollOffsets[(string)e.NewValue]);
                        }
                    else
                        {
                        verticalScrollOffsets.Add((string)e.NewValue, scrollViewer.VerticalOffset);
                        }

                    // Add scrollviewer
                    scrollViewers.Add(scrollViewer, (string)e.NewValue);
                    scrollViewer.ScrollChanged +=
                new ScrollChangedEventHandler(ScrollViewer_ScrollChanged);
                    }
                }
            }

        private static void ScrollViewer_ScrollChanged (object sender, ScrollChangedEventArgs e)
            {
            if (e.VerticalChange != 0 || e.HorizontalChange != 0)
                {
                var changedScrollViewer = sender as ScrollViewer;
                Scroll(changedScrollViewer);
                }
            }

        private static void Scroll (ScrollViewer changedScrollViewer)
            {
            var group = scrollViewers[changedScrollViewer];
            verticalScrollOffsets[group] = changedScrollViewer.VerticalOffset;
            horizontalScrollOffsets[group] = changedScrollViewer.HorizontalOffset;

            foreach (var scrollViewer in scrollViewers.Where((s) => s.Value ==
                                     group && s.Key != changedScrollViewer))
                {
                if (scrollViewer.Key.VerticalOffset != changedScrollViewer.VerticalOffset)
                    {
                    scrollViewer.Key.ScrollToVerticalOffset(changedScrollViewer.VerticalOffset);
                    }

                if (scrollViewer.Key.HorizontalOffset != changedScrollViewer.HorizontalOffset)
                    {
                    scrollViewer.Key.ScrollToHorizontalOffset(changedScrollViewer.HorizontalOffset);
                    }
                }
            }
        }
    #endregion
    }
