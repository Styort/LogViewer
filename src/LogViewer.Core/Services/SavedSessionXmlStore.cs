using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Load/save <see cref="SavedSessionDocument"/> as XML or gzip-wrapped XML.
    /// Gzip is used when the path ends with <c>.gz</c> on save, or when the file starts with the gzip magic on open.
    /// </summary>
    public static class SavedSessionXmlStore
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(SavedSessionDocument));

        public static SavedSessionDocument Load(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            try
            {
                using (var buffer = CopyToMemory(stream))
                using (var xml = UnwrapGzipIfNeeded(buffer))
                {
                    AssertSupportedVersion(xml);
                    xml.Position = 0;
                    var doc = (SavedSessionDocument)Serializer.Deserialize(xml);
                    if (doc == null)
                        throw new SavedSessionFormatException("Session XML deserialized to null.");
                    Normalize(doc);
                    return doc;
                }
            }
            catch (SavedSessionFormatException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                throw new SavedSessionFormatException(ex.Message, ex);
            }
            catch (XmlException ex)
            {
                throw new SavedSessionFormatException(ex.Message, ex);
            }
        }

        public static void Save(Stream stream, SavedSessionDocument document, bool gzip)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            Normalize(document);

            Stream target = stream;
            GZipStream gzipStream = null;
            if (gzip)
            {
                gzipStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true);
                target = gzipStream;
            }

            try
            {
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    Encoding = Encoding.UTF8,
                    CloseOutput = false
                };
                using (var writer = XmlWriter.Create(target, settings))
                    Serializer.Serialize(writer, document);
            }
            finally
            {
                gzipStream?.Dispose();
            }
        }

        public static SavedSessionDocument LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required", nameof(path));
            using (var fs = File.OpenRead(path))
                return Load(fs);
        }

        public static void SaveFile(string path, SavedSessionDocument document)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required", nameof(path));
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            bool gzip = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
            using (var fs = File.Create(path))
                Save(fs, document, gzip);
        }

        private static void Normalize(SavedSessionDocument document)
        {
            if (string.IsNullOrEmpty(document.Version))
                document.Version = SavedSessionDocument.CurrentVersion;
            if (document.Entries == null)
                document.Entries = new System.Collections.Generic.List<SavedSessionEntry>();
            if (document.Bookmarks == null)
                document.Bookmarks = new System.Collections.Generic.List<SavedSessionBookmark>();
            if (document.ExcludedLoggerFullPaths == null)
                document.ExcludedLoggerFullPaths = new System.Collections.Generic.List<string>();
            if (document.DontReceiveLoggerFullPaths == null)
                document.DontReceiveLoggerFullPaths = new System.Collections.Generic.List<string>();
            if (document.IncludedLoggerFullPaths == null)
                document.IncludedLoggerFullPaths = new System.Collections.Generic.List<string>();
            if (document.SearchText == null)
                document.SearchText = string.Empty;
            if (document.MinLevel == null)
                document.MinLevel = string.Empty;
        }

        /// <summary>
        /// Read Version before deserialize so a future schema is not silently treated as v1.
        /// Unknown version → <see cref="SavedSessionUnsupportedVersionException"/> (message, not a crash).
        /// </summary>
        private static void AssertSupportedVersion(Stream xml)
        {
            xml.Position = 0;
            var settings = new XmlReaderSettings
            {
                CloseInput = false,
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true
            };
            using (var reader = XmlReader.Create(xml, settings))
            {
                if (!reader.Read())
                    throw new SavedSessionFormatException("Session XML is empty.");
                while (!reader.EOF && reader.NodeType != XmlNodeType.Element)
                    reader.Read();
                if (reader.NodeType != XmlNodeType.Element)
                    throw new SavedSessionFormatException("Session XML has no root element.");
                if (!string.Equals(reader.Name, "LogViewerSession", StringComparison.Ordinal))
                    throw new SavedSessionFormatException("Root element must be LogViewerSession.");

                string version = reader.GetAttribute("Version");
                if (string.IsNullOrEmpty(version))
                    version = SavedSessionDocument.CurrentVersion;
                if (!string.Equals(version, SavedSessionDocument.CurrentVersion, StringComparison.Ordinal))
                    throw new SavedSessionUnsupportedVersionException(version);
            }
        }

        private static MemoryStream CopyToMemory(Stream stream)
        {
            var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            return copy;
        }

        private static MemoryStream UnwrapGzipIfNeeded(MemoryStream buffer)
        {
            if (!IsGzip(buffer))
                return buffer;

            buffer.Position = 0;
            var xml = new MemoryStream();
            using (var gzip = new GZipStream(buffer, CompressionMode.Decompress, leaveOpen: true))
                gzip.CopyTo(xml);
            xml.Position = 0;
            return xml;
        }

        private static bool IsGzip(MemoryStream buffer)
        {
            if (buffer == null || buffer.Length < 2)
                return false;
            long pos = buffer.Position;
            int b1 = buffer.ReadByte();
            int b2 = buffer.ReadByte();
            buffer.Position = pos;
            return b1 == 0x1F && b2 == 0x8B;
        }
    }
}
