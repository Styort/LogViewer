using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace LogViewer.MVVM.Models
{
    [Serializable]
    [DataContract]
    public class ReleaseNotes
    {
        /// <summary>
        /// Версия
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Список новых фич
        /// </summary>
        public List<string> NewFeatures { get; set; } = new List<string>();

        /// <summary>
        /// Список измененных фич
        /// </summary>
        public List<string> ChangedFeatures { get; set; } = new List<string>();

        /// <summary>
        /// Список исправленных багов
        /// </summary>
        public List<string> FixedBugs { get; set; } = new List<string>();

        /// <summary>
        /// Развернут ли элемент в списке.
        /// </summary>
        [XmlIgnore]
        public bool IsExpanded { get; set; } = false;
    }
}
