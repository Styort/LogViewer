using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace LogViewer.MVVM
{
    public class PortNumberValidation : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value is string str && IsPort(str))
            {
                return new ValidationResult(true, null);
            }
            return new ValidationResult(false, "Invalid port number!");
        }

        private bool IsPort(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            Regex numeric = new Regex(@"^[0-9]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            if (numeric.IsMatch(value))
            {
                try
                {
                    if (Convert.ToInt32(value) < 65536)
                        return true;
                }
                catch (OverflowException)
                {
                }
            }

            return false;
        }
    }
}
