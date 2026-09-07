using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Enums;
using LogViewer.MVVM.Models;

namespace LogViewer.Adapters
{
    /// <summary>
    /// Converts UI LogTemplate to Core LogTemplateDto for import and file tailing.
    /// </summary>
    public static class LogTemplateAdapter
    {
        /// <summary>
        /// Converts UI template to Core DTO. Enum values are aligned between eImportTemplateParameters and ImportTemplateParameters.
        /// </summary>
        public static LogTemplateDto ToDto(LogTemplate template)
        {
            if (template == null) return null;
            var dto = new LogTemplateDto
            {
                Separator = template.Separator ?? ";",
                Encoding = template.Encoding ?? "UTF-8"
            };
            foreach (var kv in template.TemplateParameterses)
                dto.TemplateParameterses[(ImportTemplateParameters)(int)kv.Key] = kv.Value;
            return dto;
        }
    }
}
