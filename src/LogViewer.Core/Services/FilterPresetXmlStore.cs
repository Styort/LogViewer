using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>Load/save <see cref="FilterPresetDocument"/>. Version defaults to 1 when the attribute is missing.</summary>
    public static class FilterPresetXmlStore
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(FilterPresetDocument));

        public static FilterPresetDocument Load(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            var doc = (FilterPresetDocument)Serializer.Deserialize(stream);
            if (doc == null)
                doc = new FilterPresetDocument();
            if (doc.Presets == null)
                doc.Presets = new System.Collections.Generic.List<FilterPreset>();
            if (string.IsNullOrEmpty(doc.Version))
                doc.Version = FilterPresetDocument.CurrentVersion;
            return doc;
        }

        public static void Save(Stream stream, FilterPresetDocument document)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrEmpty(document.Version))
                document.Version = FilterPresetDocument.CurrentVersion;
            if (document.Presets == null)
                document.Presets = new System.Collections.Generic.List<FilterPreset>();

            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                CloseOutput = false
            };
            using (var writer = XmlWriter.Create(stream, settings))
                Serializer.Serialize(writer, document);
        }

        /// <summary>Missing or unreadable file → empty document (app still starts).</summary>
        public static FilterPresetDocument LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new FilterPresetDocument { Version = FilterPresetDocument.CurrentVersion };

            try
            {
                using (var fs = File.OpenRead(path))
                    return Load(fs);
            }
            catch (InvalidOperationException)
            {
                return new FilterPresetDocument { Version = FilterPresetDocument.CurrentVersion };
            }
            catch (XmlException)
            {
                return new FilterPresetDocument { Version = FilterPresetDocument.CurrentVersion };
            }
        }

        public static void SaveFile(string path, FilterPresetDocument document)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required", nameof(path));
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var fs = File.Create(path))
                Save(fs, document);
        }

        /// <summary>
        /// Prefer <c>%Documents%\LogViewer\filter_presets.xml</c>. If only a copy next to the exe exists
        /// (same fallback as template_import_settings.xml), load that; new saves still go to Documents.
        /// </summary>
        public static string ResolveDefaultPath()
        {
            string documents = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LogViewer",
                "filter_presets.xml");
            if (File.Exists(documents))
                return documents;
            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "filter_presets.xml");
            if (File.Exists(local))
                return local;
            return documents;
        }
    }
}
