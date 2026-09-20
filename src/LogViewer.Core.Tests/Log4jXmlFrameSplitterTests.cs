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
    }
}
