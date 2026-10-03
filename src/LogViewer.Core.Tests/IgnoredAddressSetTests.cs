using System.Net;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class IgnoredAddressSetTests
    {
        [Test]
        public void Contains_MatchesWholeAddressOnly()
        {
            var set = new IgnoredAddressSet(new[] { "10.0.0.1" });

            Assert.That(set.Contains(IPAddress.Parse("10.0.0.1")), Is.True);
            Assert.That(set.Contains(IPAddress.Parse("10.0.0.10")), Is.False);
            Assert.That(set.Contains(IPAddress.Parse("10.0.0.19")), Is.False);
            Assert.That(set.Contains(IPAddress.Parse("110.0.0.1")), Is.False);
        }

        [Test]
        public void Contains_Ipv4MappedSender_MatchesIpv4Rule()
        {
            var set = new IgnoredAddressSet(new[] { "192.168.1.5" });

            Assert.That(set.Contains(IPAddress.Parse("::ffff:192.168.1.5")), Is.True);
        }

        [Test]
        public void EmptyAndBlankRules_IgnoreNothing()
        {
            var set = new IgnoredAddressSet(new[] { null, "", "  " });

            Assert.That(set.IsEmpty, Is.True);
            Assert.That(set.Contains(IPAddress.Loopback), Is.False);
            Assert.That(new IgnoredAddressSet(null).Contains(IPAddress.Loopback), Is.False);
        }

        [Test]
        public void UnparsableRule_IsComparedAsWholeText()
        {
            var set = new IgnoredAddressSet(new[] { "not-an-ip", " 127.0.0.1 " });

            Assert.That(set.Contains(IPAddress.Loopback), Is.True);
            Assert.That(set.Contains(IPAddress.Parse("127.0.0.10")), Is.False);
        }
    }
}
