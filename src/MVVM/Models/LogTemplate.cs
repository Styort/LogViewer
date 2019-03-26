using System.Collections.Generic;
using LogViewer.Enums;

namespace LogViewer.MVVM.Models
{
    public class LogTemplate
    {
        /// <summary>
        /// Элементы шаблона и их индекс в сообщении
        /// </summary>
        public Dictionary<eImportTemplateParameters, int> TemplateParameterses { get; } = new Dictionary<eImportTemplateParameters, int>();

        /// <summary>
        /// Разделитель
        /// </summary>
        public string Separator { get; set; } = ";";
    }
}
