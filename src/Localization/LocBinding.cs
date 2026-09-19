using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace LogViewer.Localization
{
    /// <summary>
    /// Binding that resolves culture-aware localized values.
    /// </summary>
    public class LocBinding : Binding
    {
        /// <summary>
        /// Creates a localization binding.
        /// </summary>
        /// <param name="name">Resource key to bind.</param>
        public LocBinding(string name) : base("[" + name + "]")
        {
            this.Source = TranslationSource.Instance;
        }
    }
}
