using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace RSCoreLib.WPF
    {
    public class LevelToIndentConverter : IValueConverter
        {
        private const double INDENT_SIZE = 19.0;
        public object Convert (object value, Type type, object parameter, CultureInfo culture)
            {
            return new Thickness((int)value * INDENT_SIZE, 0, 0, 0);
            }

        public object ConvertBack (object value, Type type, object parameter, CultureInfo culture)
            {
            throw new NotSupportedException();
            }
        }

    public sealed class ObjectToVisibilityConverter : IValueConverter
        {
        public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
            {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
            }

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
            {
            throw new NotSupportedException();
            }
        }

    public sealed class NumberToVisibilityConverter : IValueConverter
        {
        public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
            {
            if (value == null)
                return Visibility.Collapsed;

            try
                {
                return System.Convert.ToInt32(value) != 0 ? Visibility.Visible : Visibility.Collapsed;
                }
            catch(Exception)
                {
                return Visibility.Collapsed;
                }
            }

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
            {
            throw new NotSupportedException();
            }
        }
    }
