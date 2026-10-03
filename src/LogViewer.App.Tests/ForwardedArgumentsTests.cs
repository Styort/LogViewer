using LogViewer.Helpers;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    public class ForwardedArgumentsTests
    {
        [Test]
        public void PathsWithSpaces_SurviveTheRoundTrip()
        {
            var args = new[] { @"C:\Users\John Smith\logs\app 1.log", @"D:\a b\session.lvs" };

            var forwarded = UnsafeNative.SplitArguments(UnsafeNative.JoinArguments(args));

            Assert.That(forwarded, Is.EqualTo(args));
        }

        [Test]
        public void EmptyMessage_HasNoArguments()
        {
            Assert.That(UnsafeNative.SplitArguments(string.Empty), Is.Empty);
            Assert.That(UnsafeNative.SplitArguments(null), Is.Empty);
        }
    }
}
