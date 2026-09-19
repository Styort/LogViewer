using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogViewer.Localization
{
    /// <summary>
    /// Event args for a language change.
    /// </summary>
    public class LanguageEventArgs : EventArgs
    {
        public CultureInfo CultureInfo { get; private set; }

        public LanguageEventArgs(CultureInfo cultureInfo)
        {
            CultureInfo = cultureInfo;
        }
    }
}
