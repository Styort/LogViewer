using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class Log4jXmlFrameSplitterTests
    {
        private const string Event1 =
            "<log4j:event logger=\"A\" level=\"INFO\" timestamp=\"1\" thread=\"1\">" +
            "<log4j:message>one</log4j:message></log4j:event>";

        private const string Event2Partial =
            "<log4j:event logger=\"B\" level=\"INFO\" timestamp=\"1\" thread=\"1\">" +
            "<log4j:message>two";

        [Test]
        public void Append_CompleteThenPartial_YieldsOneFrameAndKeepsTail()
        {
            var splitter = new Log4jXmlFrameSplitter();

            var frames = splitter.Append(Event1 + Event2Partial);

            Assert.That(frames.Count, Is.EqualTo(1));
            Assert.That(frames[0], Is.EqualTo(Event1));
            Assert.That(splitter.RemainingText, Is.EqualTo(Event2Partial));
        }

        [Test]
        public void Append_SecondChunkCompletesPartial_YieldsRemainingFrame()
        {
            var splitter = new Log4jXmlFrameSplitter();
            splitter.Append(Event1 + Event2Partial);

            var frames = splitter.Append("</log4j:message></log4j:event>");

            Assert.That(frames.Count, Is.EqualTo(1));
            Assert.That(frames[0], Does.Contain("two"));
            Assert.That(splitter.RemainingText, Is.Empty);
        }

        [Test]
        public void Append_CustomPrefix_ExtractsMatchingEndTag()
        {
            var splitter = new Log4jXmlFrameSplitter();
            string xml = "<nlog:event logger=\"X\" level=\"INFO\" timestamp=\"1\" thread=\"1\">" +
                         "<nlog:message>ok</nlog:message></nlog:event>";

            var frames = splitter.Append(xml);

            Assert.That(frames.Count, Is.EqualTo(1));
            Assert.That(frames[0], Is.EqualTo(xml));
        }

        [Test]
        public void Append_GarbageAndBrokenFrame_DoesNotThrow()
        {
            var splitter = new Log4jXmlFrameSplitter();

            Assert.DoesNotThrow(() => splitter.Append("<<<<<<< not xml"));
            var frames = splitter.Append("<log4j:event>broken</log4j:event>");

            Assert.That(frames.Count, Is.EqualTo(1));
            Assert.That(frames[0], Is.EqualTo("<log4j:event>broken</log4j:event>"));
        }

        [Test]
        public void Append_Overflow_ClearsBuffer()
        {
            var splitter = new Log4jXmlFrameSplitter(64);
            splitter.Append(new string('x', 80));

            Assert.That(splitter.ConsumeOverflow(), Is.True);
            Assert.That(splitter.RemainingText, Is.Empty);
        }

        [Test]
        public void Append_SelfClosingEvent_DoesNotSwallowTheNextOne()
        {
            var splitter = new Log4jXmlFrameSplitter();
            const string selfClosing = "<log4j:event logger=\"A\" level=\"INFO\" timestamp=\"1\" thread=\"1\"/>";

            var frames = splitter.Append(selfClosing + Event1);

            Assert.That(frames, Is.EqualTo(new[] { selfClosing, Event1 }));
            Assert.That(splitter.RemainingText, Is.Empty);
        }

        [Test]
        public void Append_EndTagInsideCData_DoesNotCutTheEvent()
        {
            var splitter = new Log4jXmlFrameSplitter();
            string xml = "<log4j:event logger=\"A\" level=\"INFO\" timestamp=\"1\" thread=\"1\">" +
                         "<log4j:message><![CDATA[quoted </log4j:event> inside]]></log4j:message>" +
                         "<!-- and </log4j:event> in a comment --></log4j:event>";

            var frames = splitter.Append(xml + Event1);

            Assert.That(frames, Is.EqualTo(new[] { xml, Event1 }));
        }

        [Test]
        public void Append_GreaterThanInAttribute_ReadsTheWholeStartTag()
        {
            var splitter = new Log4jXmlFrameSplitter();
            string xml = "<log4j:event logger=\"a>b\" level=\"INFO\" timestamp=\"1\" thread=\"1\">" +
                         "<log4j:message>x</log4j:message></log4j:event>";

            Assert.That(splitter.Append(xml), Is.EqualTo(new[] { xml }));
        }

        [Test]
        public void Append_OneCharacterAtATime_YieldsTheSameFrames()
        {
            string selfClosing = "<log4j:event logger=\"S\" level=\"INFO\" timestamp=\"1\" thread=\"1\" />";
            string withCData = "<log4j:event logger=\"C\" level=\"INFO\" timestamp=\"1\" thread=\"1\">" +
                               "<log4j:message><![CDATA[a ]]]]><![CDATA[> </log4j:event>]]></log4j:message></log4j:event >";
            string stream = "garbage <" + Event1 + selfClosing + withCData + Event2Partial;

            var splitter = new Log4jXmlFrameSplitter();
            var frames = new List<string>();
            foreach (char c in stream)
                frames.AddRange(splitter.Append(c.ToString()));

            Assert.That(frames, Is.EqualTo(new[] { Event1, selfClosing, withCData }));
            Assert.That(splitter.RemainingText, Is.EqualTo(Event2Partial));
        }

        [Test]
        public void Append_LargeEventInSmallSegments_IsExtractedOnce()
        {
            string message = new string('x', 500 * 1024);
            string xml = "<log4j:event logger=\"A\" level=\"INFO\" timestamp=\"1\" thread=\"1\">" +
                         "<log4j:message>" + message + "</log4j:message></log4j:event>";

            var splitter = new Log4jXmlFrameSplitter();
            var frames = new List<string>();
            for (int i = 0; i < xml.Length; i += 4096)
                frames.AddRange(splitter.Append(xml.Substring(i, Math.Min(4096, xml.Length - i))));

            Assert.That(frames.Count, Is.EqualTo(1));
            Assert.That(frames[0], Is.EqualTo(xml));
            Assert.That(splitter.ConsumeOverflow(), Is.False);
        }
    }
}
