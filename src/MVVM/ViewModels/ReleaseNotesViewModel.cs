using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LogViewer.Localization;
using LogViewer.MVVM.Models;
using NLog;

namespace LogViewer.MVVM.ViewModels
{
    public class ReleaseNotesViewModel : BaseViewModel
    {
        private const string FallbackLanguage = "ru";

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly object CacheLock = new object();
        private static List<RawReleaseNotes> cachedNotes;

        private List<ReleaseNotes> releaseNotesList = new List<ReleaseNotes>();

        /// <summary>
        /// Changelog for all versions.
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
            var language = TranslationSource.Instance.CurrentCulture?.TwoLetterISOLanguageName ?? FallbackLanguage;
            var notes = ProjectForLanguage(GetOrLoadReleaseNotes(), language);
            if (notes.Count > 0)
                notes[0].IsExpanded = true;

            ReleaseNotesList = notes;
        }

        private static List<RawReleaseNotes> GetOrLoadReleaseNotes()
        {
            lock (CacheLock)
            {
                if (cachedNotes != null)
                    return cachedNotes;

                cachedNotes = LoadFromFile();
                return cachedNotes;
            }
        }

        private static List<RawReleaseNotes> LoadFromFile()
        {
            var releaseNotesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReleaseNotes.xml");
            if (!File.Exists(releaseNotesPath))
                return new List<RawReleaseNotes>();

            try
            {
                var document = XDocument.Load(releaseNotesPath);
                var root = document.Root;
                if (root == null)
                    return new List<RawReleaseNotes>();

                return root.Elements("ReleaseNotes").Select(ParseReleaseNotes).ToList();
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while read Release Notes file.");
                return new List<RawReleaseNotes>();
            }
        }

        private static RawReleaseNotes ParseReleaseNotes(XElement element)
        {
            return new RawReleaseNotes
            {
                Version = (string)element.Element("Version"),
                NewFeatures = ReadItems(element.Element("NewFeatures")),
                ChangedFeatures = ReadItems(element.Element("ChangedFeatures")),
                FixedBugs = ReadItems(element.Element("FixedBugs"))
            };
        }

        private static List<Dictionary<string, string>> ReadItems(XElement parent)
        {
            var items = new List<Dictionary<string, string>>();
            if (parent == null)
                return items;

            foreach (var child in parent.Elements())
            {
                if (child.Name.LocalName == "string")
                {
                    items.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [FallbackLanguage] = child.Value
                    });
                    continue;
                }

                var translations = child.Elements()
                    .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                    .GroupBy(x => x.Name.LocalName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);

                if (translations.Count > 0)
                    items.Add(translations);
            }

            return items;
        }

        private static List<ReleaseNotes> ProjectForLanguage(IReadOnlyList<RawReleaseNotes> rawNotes, string language)
        {
            return rawNotes.Select(raw => new ReleaseNotes
            {
                Version = raw.Version,
                NewFeatures = ProjectItems(raw.NewFeatures, language),
                ChangedFeatures = ProjectItems(raw.ChangedFeatures, language),
                FixedBugs = ProjectItems(raw.FixedBugs, language)
            }).ToList();
        }

        private static List<string> ProjectItems(IEnumerable<Dictionary<string, string>> items, string language)
        {
            return items.Select(item => ResolveText(item, language))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
        }

        private static string ResolveText(Dictionary<string, string> translations, string language)
        {
            if (translations.TryGetValue(language, out var localized) && !string.IsNullOrWhiteSpace(localized))
                return localized;

            if (translations.TryGetValue(FallbackLanguage, out var fallback) && !string.IsNullOrWhiteSpace(fallback))
                return fallback;

            return translations.Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
        }

        private sealed class RawReleaseNotes
        {
            public string Version { get; set; }
            public List<Dictionary<string, string>> NewFeatures { get; set; } = new List<Dictionary<string, string>>();
            public List<Dictionary<string, string>> ChangedFeatures { get; set; } = new List<Dictionary<string, string>>();
            public List<Dictionary<string, string>> FixedBugs { get; set; } = new List<Dictionary<string, string>>();
        }
    }
}
