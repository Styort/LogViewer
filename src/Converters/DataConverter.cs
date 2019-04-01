using LogViewer.MVVM.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace LogViewer.Converters
{
    public class DataConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value is DateTime dateValue)
            {
                var dataFormat = Settings.Instance.DataFormat;
                if (string.IsNullOrEmpty(dataFormat))
                    return dateValue;
                return dateValue.ToString(Settings.Instance.DataFormat);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
