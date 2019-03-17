using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace LogViewer.Localization
{
    /// <summary>
    /// Расширенный биндинг - используется для биндинга локализованных с учётом культуры значений
    /// </summary>
    public class LocBinding : Binding
    {
        /// <summary>
        /// Создаёт экземпляр расширенного биндинга
        /// </summary>
        /// <param name="name">Свойство вьюмодели для биндинга</param>
        public LocBinding(string name) : base("[" + name + "]")
        {
            this.Source = TranslationSource.Instance;
        }
    }
}
