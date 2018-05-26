using System.Windows;
using System.Windows.Controls;

namespace RSCoreLib.WPF
    {
    public partial class WatermarkTextBox : UserControl
        {
        public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register("Watermark", typeof(string), typeof(WatermarkTextBox), new PropertyMetadata(string.Empty));

        public string Watermark
            {
            get { return (string)GetValue(WatermarkProperty); }
            set { SetValue(WatermarkProperty, value); }
            }

        public string Text
            {
            get { return (string)GetValue(TextBox.TextProperty); }
            set { SetValue(TextBox.TextProperty, value); }
            }

        public WatermarkTextBox ()
            {
            InitializeComponent();
            RootGrid.DataContext = this;
            }
        }
    }
