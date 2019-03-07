using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace LogViewer.Converters
{
    public class LoggerDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value is string strValue && !string.IsNullOrEmpty(strValue))
            {
                var valueArr = strValue.Split('.');
                if (valueArr.Length > 0)
                    return valueArr.Last();
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
