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
        /// Version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// New features.
        /// </summary>
        public List<string> NewFeatures { get; set; } = new List<string>();

        /// <summary>
        /// Changed features.
        /// </summary>
        public List<string> ChangedFeatures { get; set; } = new List<string>();

        /// <summary>
        /// Fixed bugs.
        /// </summary>
        public List<string> FixedBugs { get; set; } = new List<string>();

        /// <summary>
        /// Whether the item is expanded in the list.
        /// </summary>
        [XmlIgnore]
        public bool IsExpanded { get; set; } = false;
    }
}
