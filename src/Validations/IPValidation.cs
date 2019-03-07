using System.Globalization;
using System.Linq;
using System.Net;
using System.Windows.Controls;

namespace LogViewer.Validations
{
    public class IPValidation : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value is string str && ValidateIPv4(str))
            {
                return new ValidationResult(true, null);
            }
            return new ValidationResult(false, "Invalid IP Address!");
        }

        private bool ValidateIPv4(string ipString)
        {
            if (ipString.Count(c => c == '.') != 3) return false;
            IPAddress address;
            return IPAddress.TryParse(ipString, out address);
        }
    }
}
