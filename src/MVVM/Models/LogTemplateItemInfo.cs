﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using LogViewer.Enums;

namespace LogViewer.MVVM.Models
{
    /// <summary>
    /// Информация о параметре шаблона
    /// </summary>
    [Serializable]
    [DataContract]
    public class LogTemplateItemInfo
    {
        /// <summary>
        /// Параметр
        /// </summary>
        public eImportTemplateParameters Parameter { get; set; }

        /// <summary>
        /// Группа параметра
        /// </summary>
        public string Group { get; set; }
    }
}
