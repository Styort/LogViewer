using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class MessageFingerprintTests
    {
        [Test]
        public void Normalize_ReplacesGuidAndLongNumbers()
        {
            var a = MessageFingerprint.Normalize(
                "Timeout id=11111111-2222-3333-4444-555555555555 seq=847001");
            var b = MessageFingerprint.Normalize(
                "Timeout id=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee seq=999999");

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Does.Contain(MessageFingerprint.Placeholder));
            Assert.That(a, Does.Not.Contain("11111111"));
        }

        [Test]
        public void Normalize_ReplacesIsoTimestamp()
        {
            var a = MessageFingerprint.Normalize("failed at 2024-01-15T12:34:56.123Z");
            var b = MessageFingerprint.Normalize("failed at 2026-09-19T09:00:00Z");
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void Normalize_KeepsShortHttpCodes()
        {
            var a = MessageFingerprint.Normalize("HTTP 404 not found");
            var b = MessageFingerprint.Normalize("HTTP 500 not found");
            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void BuildKey_DifferentLevel_DifferentKey()
        {
            var error = MessageFingerprint.BuildKey(LogLevel.Error, "App", "boom", null);
            var warn = MessageFingerprint.BuildKey(LogLevel.Warn, "App", "boom", null);
            Assert.That(error, Is.Not.EqualTo(warn));
        }

        [Test]
        public void BuildKey_DifferentLogger_DifferentKey()
        {
            var a = MessageFingerprint.BuildKey(LogLevel.Error, "A", "boom", null);
            var b = MessageFingerprint.BuildKey(LogLevel.Error, "B", "boom", null);
            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void BuildKey_EmptyThrowable_DiffersFromSameMessageWithException()
        {
            var without = MessageFingerprint.BuildKey(LogLevel.Error, "App", "boom", null);
            var with = MessageFingerprint.BuildKey(LogLevel.Error, "App", "boom", "System.Exception: boom");
            Assert.That(without, Is.Not.EqualTo(with));
        }

        [Test]
        public void BuildKey_IgnoresStackFrames()
        {
            const string stackA =
                "System.TimeoutException: timed out\r\n   at Foo.Bar() in C:\\src\\Foo.cs:line 42";
            const string stackB =
                "System.TimeoutException: timed out\r\n   at Foo.Bar() in D:\\other\\Foo.cs:line 99";
            var a = MessageFingerprint.BuildKey(LogLevel.Error, "App", "x", stackA);
            var b = MessageFingerprint.BuildKey(LogLevel.Error, "App", "x", stackB);
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void Headline_UsesExceptionType()
        {
            Assert.That(
                MessageFingerprint.Headline("ignored", "System.TimeoutException: timed out\nat x"),
                Is.EqualTo("System.TimeoutException"));
        }
    }
}
