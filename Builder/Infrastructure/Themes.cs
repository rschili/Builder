using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MahApps.Metro;

namespace Builder
    {
    public enum Theme
        {
        Random,
        Alpha,
        Version0_9,
        Version1_0,
        Version1_3,
        Version1_4,
        Latest
        }

    public static class ThemeHelper
        {
        public static IEnumerable<Theme> Themes
            {
            get
                {
                return Enum.GetValues(typeof(Theme)).Cast<Theme>();
                }
            }

        private static Theme AppliedTheme = Theme.Random;

        public static void ApplyTheme (Theme theme)
            {
            //There could be a threading overlap here, but nothing that would break the application.
            if (AppliedTheme != Theme.Random && theme == AppliedTheme)
                return;

            AppliedTheme = theme;

            if(theme == Theme.Random)
                {
                Random rnd = new Random();
                int value = rnd.Next((int)Theme.Version0_9, (int)Theme.Latest);
                theme = (Theme)value;
                }

            switch (theme)
                {
                case Theme.Alpha:
                    ThemeManager.ChangeAppStyle(Application.Current,
                                    ThemeManager.GetAccent("Cobalt"),
                                    ThemeManager.GetAppTheme("BaseLight"));
                    break;

                case Theme.Version0_9:
                    ThemeManager.ChangeAppStyle(Application.Current,
                                    ThemeManager.GetAccent("Lime"),
                                    ThemeManager.GetAppTheme("BaseDark"));
                    break;

                case Theme.Version1_0:
                    ThemeManager.ChangeAppStyle(Application.Current,
                                    ThemeManager.GetAccent("Olive"),
                                    ThemeManager.GetAppTheme("BaseLight"));
                    break;

                case Theme.Version1_3:
                    ThemeManager.ChangeAppStyle(Application.Current,
                                    ThemeManager.GetAccent("Emerald"),
                                    ThemeManager.GetAppTheme("BaseLight"));
                    break;

                case Theme.Version1_4:
                case Theme.Latest:
                    ThemeManager.ChangeAppStyle(Application.Current,
                                    ThemeManager.GetAccent("Olive"),
                                    ThemeManager.GetAppTheme("BaseDark"));
                    break;
                }
            }
        }

    public sealed class ThemeToBrushConverter : IValueConverter
        {
        public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
            {
            if (!(value is Theme))
                return Brushes.Transparent;

            Theme t = (Theme)value;

            //Use http://htmlcolorcodes.com/color-picker/ and https://github.com/MahApps/MahApps.Metro/blob/develop/MahApps.Metro/Styles/Accents/Lime.xaml
            //Take AccentBaseColor.
            if (t == Theme.Alpha)
                return Brushes.Blue;

            if (t == Theme.Version0_9)
                return new SolidColorBrush(Color.FromRgb(164,196,0));

            if (t == Theme.Version1_0)
                return new SolidColorBrush(Color.FromRgb(109, 135, 100));

            if (t == Theme.Version1_3)
                return new SolidColorBrush(Color.FromRgb(0, 138, 0));

            if (t == Theme.Version1_4 || t == Theme.Latest)
                return new SolidColorBrush(Color.FromRgb(109, 135, 100));

            return Brushes.Transparent;
            }

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
            {
            throw new NotSupportedException();
            }
        }

    public sealed class ThemeToLabelConverter : IValueConverter
        {
        public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
            {
            if (!(value is Theme))
                return string.Empty;

            Theme t = (Theme)value;
            if (t == Theme.Random)
                return "Random";

            if (t == Theme.Alpha)
                return "Alpha (Cobalt)";

            if (t == Theme.Version0_9)
                return "Version 0.9 (Lime Dark)";

            if (t == Theme.Version1_0)
                return "Version 1.0 (Olive Light)";

            if (t == Theme.Version1_3)
                return "Version 1.3 (Emerald Light)";

            if (t == Theme.Version1_4)
                return "Version 1.4 (Olive Dark)";

            return "Latest";
            }

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
            {
            throw new NotSupportedException();
            }
        }
    }
