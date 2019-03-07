using System;
using System.Globalization;
using System.Windows.Data;

namespace LogViewer.Converters
{
    public class MaxLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrEmpty(str) && int.TryParse((string)parameter, out int maxLength))
            {
                var indexNewLine = str.IndexOf(Environment.NewLine, StringComparison.Ordinal);
                if (indexNewLine != -1 && indexNewLine < maxLength)
                {
                    return str.Substring(0, indexNewLine) + "...";
                }

                var indexN = str.IndexOf('\n');
                if (indexN != -1 && indexN < maxLength)
                {
                    return str.Substring(0, indexN) + "...";
                }

                if (str.Length > maxLength)
                {
                    return str.Substring(0, maxLength) + "...";
                }
                return str;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }


    }
}
