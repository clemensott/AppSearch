using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace AppSearch
{
    class ToDirectoryNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Path.GetDirectoryName((string)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
