using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        public void Parse_EventWithoutLoggerLevelAndTimestamp_DoesNotThrow()
        {
            const string xml = "<log4j:event><log4j:message>x</log4j:message></log4j:event>";

            LogEntry entry = _parser.Parse(xml);

            Assert.That(entry.Logger, Is.Null, "the parser reports what is on the wire; the live factory normalizes");
            Assert.That(entry.Level, Is.EqualTo(XmlLogParser.DefaultLevel));
            Assert.That(entry.Message, Is.EqualTo("x"));
        }

        [TestCase("WARN", LogLevel.Warn)]
        [TestCase("warning", LogLevel.Warn)]
        [TestCase("Error", LogLevel.Error)]
        [TestCase("FATAL", LogLevel.Fatal)]
        [TestCase("trace", LogLevel.Trace)]
        [TestCase("VERBOSE", XmlLogParser.DefaultLevel)]
        [TestCase("5", XmlLogParser.DefaultLevel)]
        [TestCase("", XmlLogParser.DefaultLevel)]
        public void Parse_LevelAttribute_MapsKnownNamesAndFallsBackForUnknown(string level, LogLevel expected)
        {
            string xml = "<log4j:event logger=\"A\" level=\"" + level + "\" timestamp=\"1\"><log4j:message>m</log4j:message></log4j:event>";

            Assert.That(_parser.Parse(xml).Level, Is.EqualTo(expected));
        }

        [Test]
        public void Parse_EventWithOwnNamespaceDeclarations_IsParsed()
        {
            // NLog's NLogViewer target declares the prefixes on every event.
            const string xml =
                "<log4j:event xmlns:log4j=\"http://jakarta.apache.org/log4j/\" xmlns:nlog=\"http://nlog-project.org\" " +
                "logger=\"Ns.Logger\" level=\"INFO\" timestamp=\"1234567890000\" thread=\"3\">" +
                "<log4j:message>hello</log4j:message>" +
                "<nlog:locationInfo assembly=\"App\" />" +
                "</log4j:event>";

            LogEntry entry = _parser.Parse(xml);

            Assert.That(entry.Logger, Is.EqualTo("Ns.Logger"));
            Assert.That(entry.Message, Is.EqualTo("hello"));
        }

        [Test]
        public void Parse_DoctypeInFragment_IsRejected()
        {
            const string xml =
                "<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///c:/windows/win.ini\">]>" +
                "<log4j:event logger=\"A\" level=\"INFO\" timestamp=\"1\"><log4j:message>&e;</log4j:message></log4j:event>";

            Assert.That(() => _parser.Parse(xml), Throws.InstanceOf<Exception>());
        }

        /// <summary>
        /// One parser instance is shared by every UDP/TCP receive thread. A shared XmlParserContext used to
        /// lose namespace scopes under contention ("'log4j' is an undeclared prefix").
        /// </summary>
        [Test]
        public void Parse_SharedInstanceAcrossThreads_HasNoErrors()
        {
            var parser = new XmlLogParser();
            const int threads = 8;
            const int eventsPerThread = 5000;
            var errors = new List<Exception>();
            int wrongResults = 0;
            using (var start = new ManualResetEventSlim(false))
            {
                var tasks = Enumerable.Range(0, threads).Select(t => Task.Factory.StartNew(() =>
                {
                    start.Wait();
                    for (int i = 0; i < eventsPerThread; i++)
                    {
                        string logger = "T" + t + ".L" + (i % 50);
                        string xml =
                            "<log4j:event xmlns:log4j=\"http://jakarta.apache.org/log4j/\" logger=\"" + logger +
                            "\" level=\"INFO\" timestamp=\"1\" thread=\"" + t + "\"><log4j:message>m" + i +
                            "</log4j:message><log4j:properties><log4j:data name=\"k" + i + "\" value=\"v\" /></log4j:properties></log4j:event>";
                        try
                        {
                            var entry = parser.Parse(xml);
                            if (entry.Logger != logger || entry.Message != "m" + i)
                                Interlocked.Increment(ref wrongResults);
                        }
                        catch (Exception ex)
                        {
                            lock (errors)
                                errors.Add(ex);
                        }
                    }
                }, TaskCreationOptions.LongRunning)).ToArray();

                start.Set();
                Task.WaitAll(tasks);
            }

            Assert.That(errors, Is.Empty, errors.Count > 0 ? errors[0].ToString() : null);
            Assert.That(wrongResults, Is.Zero);
        }

        [Test]
        public void ExportText_JoinsMessageAndThrowable()
        {
            string text = LogExportText.JoinMessageAndThrowable("failed", "System.Exception: boom");
            Assert.That(text, Is.EqualTo("failed" + Environment.NewLine + "System.Exception: boom"));
        }
    }
}
