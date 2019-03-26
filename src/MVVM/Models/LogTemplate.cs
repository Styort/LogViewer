using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public char Separator { get; set; } = ';';
    }
}
