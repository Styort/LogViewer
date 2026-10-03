using System;
using System.IO;
using System.Text;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class AtomicFileTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "LogViewerAtomic_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }

        [Test]
        public void Write_CreatesMissingFolderAndFile()
        {
            string path = Path.Combine(_dir, "nested", "settings.xml");

            AtomicFile.Write(path, s => WriteText(s, "new"));

            Assert.That(File.ReadAllText(path), Is.EqualTo("new"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void Write_ReplacesExistingFile()
        {
            Directory.CreateDirectory(_dir);
            string path = Path.Combine(_dir, "settings.xml");
            File.WriteAllText(path, "old content that is longer");

            AtomicFile.Write(path, s => WriteText(s, "new"));

            Assert.That(File.ReadAllText(path), Is.EqualTo("new"));
            Assert.That(Directory.GetFiles(_dir), Has.Length.EqualTo(1));
        }

        [Test]
        public void Write_FailingSerializer_KeepsTheOldFileIntact()
        {
            Directory.CreateDirectory(_dir);
            string path = Path.Combine(_dir, "settings.xml");
            File.WriteAllText(path, "receivers");

            Assert.Throws<InvalidOperationException>(() => AtomicFile.Write(path, s =>
            {
                WriteText(s, "half");
                throw new InvalidOperationException("crash in the middle of saving");
            }));

            Assert.That(File.ReadAllText(path), Is.EqualTo("receivers"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        private static void WriteText(Stream stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
