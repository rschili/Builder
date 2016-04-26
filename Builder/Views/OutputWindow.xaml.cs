using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Builder
    {
    public partial class OutputWindow
        {
        public OutputWindow ()
            {
            InitializeComponent();
            _ts = TaskScheduler.FromCurrentSynchronizationContext();
            }

        TaskScheduler _ts = null;

        bool _active = false;
        private void MetroWindow_Loaded (object sender, RoutedEventArgs e)
            {
            _active = true;
            var vm = (DataContext as MainVM)?.HistoryVM;
            if (vm == null)
                return;

            vm.Output += OutputReceived;
            vm.OutputCleared += OutputCleared;
            }

        private void OutputCleared (object sender, EventArgs e)
            {
            Task.Factory.StartNew(() => Cls(), CancellationToken.None, TaskCreationOptions.None, _ts);
            }

        private void Cls ()
            {
            if (!_active)
                return;

            Opfer.Clear();
            _isAtEnd = true;
            }

        private void OutputReceived (object sender, string e)
            {
            Task.Factory.StartNew(() => AppendLine(e), CancellationToken.None, TaskCreationOptions.None, _ts);
            }

        private bool _isAtEnd = true;
        private void OnScrollChanged (object sender, ScrollChangedEventArgs e)
            {
            var scrollViewer = (ScrollViewer)sender;
            _isAtEnd = scrollViewer.VerticalOffset == scrollViewer.ScrollableHeight;
            }

        private void AppendLine (string line)
            {
            if (!_active)
                return;

            Opfer.AppendText(line);
            Opfer.AppendText(Environment.NewLine);

            if (_isAtEnd)
                ScrollV.ScrollToEnd();
            }

        private void MetroWindow_Closed (object sender, EventArgs e)
            {
            _active = false;

            var vm = (DataContext as MainVM)?.HistoryVM;
            if (vm == null)
                return;

            vm.Output -= OutputReceived;
            vm.OutputCleared -= OutputCleared;
            }
        }
    }
