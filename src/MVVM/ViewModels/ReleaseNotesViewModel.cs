using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using LogViewer.MVVM.Models;
using NLog;

namespace LogViewer.MVVM.ViewModels
{
    public class ReleaseNotesViewModel
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Список изменений во всех версиях
        /// </summary>
        public List<ReleaseNotes> ReleaseNotesList { get; set; } = new List<ReleaseNotes>();

        public ReleaseNotesViewModel()
        {
            var releaseNotesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReleaseNotes.xml");

            if (File.Exists(releaseNotesPath))
            {
                try
                {
                    XmlSerializer ser = new XmlSerializer(ReleaseNotesList.GetType());
                    using (var fs = new FileStream(releaseNotesPath, FileMode.Open))
                    {
                        ReleaseNotesList = (List<ReleaseNotes>) ser.Deserialize(fs);
                    }
                }
                catch (Exception e)
                {
                    logger.Warn(e, "An error occurred while read Release Notes file.");
                }
            }

            if (ReleaseNotesList.Any())
                ReleaseNotesList.First().IsExpanded = true;
        }
    }
}
