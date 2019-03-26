using System.Collections.Generic;
using LogViewer.Enums;

namespace LogViewer.MVVM.Models
{
    /// <summary>
    /// Хранит в себе конфигурацию шаблона лога, через которую будет импортироваться лог
    /// </summary>
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
