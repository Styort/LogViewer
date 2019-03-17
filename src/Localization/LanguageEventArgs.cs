using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogViewer.Localization
{
    /// <summary>
    /// Аргументы события смены языка
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
