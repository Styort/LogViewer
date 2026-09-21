using System;
using System.IO;
using System.Linq;
using System.Text;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class SavedSessionTests
    {
        [Test]
        public void RoundTrip_PreservesEntriesBookmarksAndExcludedPaths()
        {
            var original = new SavedSessionDocument
            {
                Version = SavedSessionDocument.CurrentVersion,
                SearchText = "timeout",
                IsSearchActive = true,
                MatchCase = true,
                UseRegex = false,
                IsTimeIntervalActive = true,
                TimeRangeFrom = new DateTime(2026, 9, 20, 10, 0, 0),
                TimeRangeTo = new DateTime(2026, 9, 20, 11, 0, 0),
                ExcludedLoggerFullPaths = { "127.0.0.1.Other" },
                DontReceiveLoggerFullPaths = { "127.0.0.1.Noise" },
                Bookmarks = { new SavedSessionBookmark { Index = 0, Comment = "look here" } },
                Entries =
                {
                    SavedSessionMapper.ToDto(new LogEntry
                    {
                        Time = new DateTime(2026, 9, 20, 10, 15, 1),
                        Level = LogLevel.Error,
                        Logger = "App.Pay",
                        Thread = 12,
                        Message = "failed <tag>",
                        Address = "127.0.0.1",
                        ExecutableName = "svc",
                        ProcessID = 44,
                        ReceiverPort = 7071,
                        ReceiverTransport = ReceiverTransport.Tcp,
                        Throwable = "System.Exception: boom",
                        Properties = { { "requestId", "abc-1" } }
                    })
                }
            };

            SavedSessionDocument loaded;
            using (var stream = new MemoryStream())
            {
                SavedSessionXmlStore.Save(stream, original, gzip: false);
                stream.Position = 0;
                loaded = SavedSessionXmlStore.Load(stream);
            }

            Assert.That(loaded.Version, Is.EqualTo("1"));
            Assert.That(loaded.SearchText, Is.EqualTo("timeout"));
            Assert.That(loaded.ExcludedLoggerFullPaths, Is.EqualTo(new[] { "127.0.0.1.Other" }));
            Assert.That(loaded.DontReceiveLoggerFullPaths, Is.EqualTo(new[] { "127.0.0.1.Noise" }));
            Assert.That(loaded.Bookmarks.Count, Is.EqualTo(1));
            Assert.That(loaded.Bookmarks[0].Index, Is.EqualTo(0));
            Assert.That(loaded.Bookmarks[0].Comment, Is.EqualTo("look here"));

            var entry = SavedSessionMapper.ToEntries(loaded).Single();
            Assert.That(entry.Message, Is.EqualTo("failed <tag>"));
            Assert.That(entry.Throwable, Is.EqualTo("System.Exception: boom"));
            Assert.That(entry.Properties["requestId"], Is.EqualTo("abc-1"));
            Assert.That(entry.ProcessID, Is.EqualTo(44));
            Assert.That(entry.ReceiverTransport, Is.EqualTo(ReceiverTransport.Tcp));
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
        }

        [Test]
        public void GzipRoundTrip_LoadDetectsMagicBytes()
        {
            var original = new SavedSessionDocument
            {
                Version = "1",
                Entries = { new SavedSessionEntry { Logger = "A", Message = "m", Level = "Info" } }
            };

            using (var stream = new MemoryStream())
            {
                SavedSessionXmlStore.Save(stream, original, gzip: true);
                stream.Position = 0;
                Assert.That(stream.ReadByte(), Is.EqualTo(0x1F));
                stream.Position = 0;
                var loaded = SavedSessionXmlStore.Load(stream);
                Assert.That(loaded.Entries[0].Logger, Is.EqualTo("A"));
            }
        }

        [Test]
        public void UnknownVersion_ThrowsUnsupportedWithoutNre()
        {
            const string xml = @"<?xml version=""1.0""?><LogViewerSession Version=""2""><Entries/></LogViewerSession>";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                var ex = Assert.Throws<SavedSessionUnsupportedVersionException>(() => SavedSessionXmlStore.Load(stream));
                Assert.That(ex.Version, Is.EqualTo("2"));
            }
        }

        [Test]
        public void ParseError_ThrowsFormatExceptionNotNre()
        {
            const string xml = "<not-a-session>";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                Assert.Throws<SavedSessionFormatException>(() => SavedSessionXmlStore.Load(stream));
            }
        }

        [Test]
        public void MissingVersion_DefaultsToV1()
        {
            const string xml = @"<?xml version=""1.0""?><LogViewerSession><Entries><Entry Logger=""X"" Message=""hi""/></Entries></LogViewerSession>";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                var doc = SavedSessionXmlStore.Load(stream);
                Assert.That(doc.Version, Is.EqualTo("1"));
                Assert.That(doc.Entries[0].Logger, Is.EqualTo("X"));
            }
        }

        [Test]
        public void SparseEntry_DoesNotThrowNre()
        {
            const string xml = @"<?xml version=""1.0""?><LogViewerSession Version=""1""><Entries><Entry/></Entries><Bookmarks><Bookmark Index=""99""/></Bookmarks></LogViewerSession>";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                var doc = SavedSessionXmlStore.Load(stream);
                var entry = SavedSessionMapper.ToEntry(doc.Entries[0]);
                Assert.That(entry, Is.Not.Null);
                Assert.That(entry.Message, Is.EqualTo(string.Empty));
                Assert.That(entry.Properties, Is.Empty);
            }
        }
    }
}
