using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using LogViewer.Enums;

namespace LogViewer.MVVM.Models
{
    /// <summary>
    /// Template parameter info.
    /// </summary>
    [Serializable]
    [DataContract]
    public class LogTemplateItemInfo
    {
        /// <summary>
        /// Parameter.
        /// </summary>
        public eImportTemplateParameters Parameter { get; set; }

        /// <summary>
        /// Parameter group.
        /// </summary>
        public string Group { get; set; }
    }
}
