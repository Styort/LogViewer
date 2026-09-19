using System.Collections.Generic;
using LogViewer.Enums;

namespace LogViewer.MVVM.Models
{
    /// <summary>
    /// Holds the log template used when importing a file.
    /// </summary>
    public class LogTemplate
    {
        /// <summary>
        /// Template fields and their index in the message.
        /// </summary>
        public Dictionary<eImportTemplateParameters, int> TemplateParameterses { get; } = new Dictionary<eImportTemplateParameters, int>();

        /// <summary>
        /// Separator.
        /// </summary>
        public string Separator { get; set; } = ";";

        /// <summary>
        /// Encoding.
        /// </summary>
        public string Encoding { get; set; } = "UTF-8";
    }
}
