using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class TemplateLogParserTests
    {
        [Test]
        public void ParseHeaderLine_ReadsLevelDateLoggerAndMessage()
        {
            var layout = Layout();
            var parser = new TemplateLogParser();
            string line = "Error|2020-01-02 03:04:05.1234|My.App|hello world extra";

            LogEntry entry = parser.ParseHeaderLine(line, layout, @"C:\logs\app.log");

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Time, Is.EqualTo(new DateTime(2020, 1, 2, 3, 4, 5, 123).AddTicks(4000)));
            Assert.That(entry.Logger, Is.EqualTo("My.App"));
            Assert.That(entry.Message, Is.EqualTo("hello world extra"));
            Assert.That(entry.Address, Is.EqualTo(@"C:\logs\app.log"));
        }

        [Test]
        public void Read_AppendsContinuationLinesToMessage()
        {
            var layout = Layout();
            var parser = new TemplateLogParser();
            string text =
                "Error|2020-01-02 03:04:05.1234|My.App|failed" + Environment.NewLine +
                "  at Foo.Bar()" + Environment.NewLine +
                "Info|2020-01-02 03:04:06.0000|My.App|ok" + Environment.NewLine;

            var entries = new List<LogEntry>();
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                TemplateFileLogReader.Read(stream, Encoding.UTF8, parser, layout, "file.log",
                    entries.Add, CancellationToken.None, null);
            }

            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries[0].Message, Is.EqualTo("failed" + Environment.NewLine + "  at Foo.Bar()"));
            Assert.That(entries[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entries[1].Message, Is.EqualTo("ok"));
            Assert.That(entries[1].Level, Is.EqualTo(LogLevel.Info));
        }

        private static TemplateParseLayout Layout()
        {
            var dto = new LogTemplateDto { Separator = "|" };
            dto.TemplateParameterses[ImportTemplateParameters.level] = 0;
            dto.TemplateParameterses[ImportTemplateParameters.longdate] = 1;
            dto.TemplateParameterses[ImportTemplateParameters.logger] = 2;
            dto.TemplateParameterses[ImportTemplateParameters.message] = 3;
            return TemplateParseLayout.Create(dto);
        }
    }
}
