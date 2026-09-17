using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LogViewer.MVVM.Models;
using NLog;

namespace LogViewer.MVVM.ViewModels
{
    public class ReleaseNotesViewModel : BaseViewModel
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly object CacheLock = new object();
        private static List<ReleaseNotes> cachedNotes;

        private List<ReleaseNotes> releaseNotesList = new List<ReleaseNotes>();

        /// <summary>
        /// Список изменений во всех версиях
        /// </summary>
        public List<ReleaseNotes> ReleaseNotesList
        {
            get => releaseNotesList;
            private set
            {
                releaseNotesList = value;
                OnPropertyChanged();
            }
        }

        public ReleaseNotesViewModel()
        {
            var notes = GetOrLoadReleaseNotes();
            if (notes.Count > 0)
                notes[0].IsExpanded = true;

            ReleaseNotesList = notes;
        }

        private static List<ReleaseNotes> GetOrLoadReleaseNotes()
        {
            lock (CacheLock)
            {
                if (cachedNotes != null)
                    return cachedNotes;

                cachedNotes = LoadFromFile();
                return cachedNotes;
            }
        }

        private static List<ReleaseNotes> LoadFromFile()
        {
            var releaseNotesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReleaseNotes.xml");
            if (!File.Exists(releaseNotesPath))
                return new List<ReleaseNotes>();

            try
            {
                var document = XDocument.Load(releaseNotesPath);
                var root = document.Root;
                if (root == null)
                    return new List<ReleaseNotes>();

                return root.Elements("ReleaseNotes").Select(ParseReleaseNotes).ToList();
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while read Release Notes file.");
                return new List<ReleaseNotes>();
            }
        }

        private static ReleaseNotes ParseReleaseNotes(XElement element)
        {
            return new ReleaseNotes
            {
                Version = (string)element.Element("Version"),
                NewFeatures = ReadStrings(element.Element("NewFeatures")),
                ChangedFeatures = ReadStrings(element.Element("ChangedFeatures")),
                FixedBugs = ReadStrings(element.Element("FixedBugs"))
            };
        }

        private static List<string> ReadStrings(XElement parent)
        {
            if (parent == null)
                return new List<string>();

            return parent.Elements("string").Select(x => x.Value).ToList();
        }
    }
}
