using System;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class XmlLogParserTests
    {
        private readonly XmlLogParser _parser = new XmlLogParser();

        [Test]
        public void Parse_ReadsPropertiesAndThrowable_WithoutAppendingStackToMessage()
        {
            string xml =
                "<log4j:event logger=\"My.App\" level=\"ERROR\" timestamp=\"1234567890000\" thread=\"7\">" +
                "<log4j:message>failed</log4j:message>" +
                "<log4j:throwable>System.Exception: boom</log4j:throwable>" +
                "<log4j:properties>" +
                "<log4j:data name=\"log4japp\" value=\"MyApp.exe\" />" +
                "<log4j:data name=\"user\" value=\"alice\" />" +
                "<log4j:data name=\"\" value=\"ignored\" />" +
                "<log4j:data name=\"thread\" value=\"worker\" />" +
                "</log4j:properties>" +
                "</log4j:event>";

            LogEntry entry = _parser.Parse(xml);

            Assert.That(entry.Message, Is.EqualTo("failed"));
            Assert.That(entry.Throwable, Is.EqualTo("System.Exception: boom"));
            Assert.That(entry.ExecutableName, Is.EqualTo("MyApp"));
            Assert.That(entry.Properties, Is.Not.Null);
            Assert.That(entry.Properties["user"], Is.EqualTo("alice"));
            Assert.That(entry.Properties["thread"], Is.EqualTo("worker"));
            Assert.That(entry.Properties["log4japp"], Is.EqualTo("MyApp.exe"));
            Assert.That(entry.Properties.ContainsKey(""), Is.False);
            Assert.That(entry.Thread, Is.EqualTo(7));
            Assert.That(entry.Logger, Is.EqualTo("My.App"));
        }

        [Test]
        public void Parse_EventWithoutProperties_HasEmptyDictionary()
        {
            string xml =
                "<log4j:event logger=\"A\" level=\"INFO\" timestamp=\"1234567890000\" thread=\"1\">" +
                "<log4j:message>ok</log4j:message>" +
                "</log4j:event>";

            LogEntry entry = _parser.Parse(xml);

            Assert.That(entry.Properties, Is.Not.Null);
            Assert.That(entry.Properties, Is.Empty);
            Assert.That(entry.Throwable, Is.Null.Or.Empty);
            Assert.That(entry.Message, Is.EqualTo("ok"));
        }

        [Test]
        public void ExportText_JoinsMessageAndThrowable()
        {
            string text = LogExportText.JoinMessageAndThrowable("failed", "System.Exception: boom");
            Assert.That(text, Is.EqualTo("failed" + Environment.NewLine + "System.Exception: boom"));
        }
    }
}
