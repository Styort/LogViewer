using System.Collections.Generic;

namespace LogViewer.Core.Domain
{
    /// <summary>
    /// Minimal template DTO for file log parsing. No UI.
    /// </summary>
    public class LogTemplateDto
    {
        public Dictionary<ImportTemplateParameters, int> TemplateParameterses { get; } = new Dictionary<ImportTemplateParameters, int>();
        public string Separator { get; set; } = ";";
        public string Encoding { get; set; } = "UTF-8";
    }
}
