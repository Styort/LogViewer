using System.Linq;
using LogViewer.Core.State;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class LoggerFilterStateTests
    {
        [Test]
        public void ExcludeChild_DoesNotExcludeParent()
        {
            var state = new LoggerFilterState();
            state.ExcludeSubtree("a.b.Child", new[] { "a.b.Child.Leaf" });

            Assert.That(state.ExcludedPaths, Does.Not.Contain("a.b"));
            Assert.That(state.ExcludedPaths, Does.Contain("a.b.Child"));
            Assert.That(state.ExcludedPaths, Does.Contain("a.b.Child.Leaf"));
        }

        [Test]
        public void ShowOnly_LeavesExactlyTheSubtree()
        {
            var state = new LoggerFilterState();
            var available = new[] { "root.keep", "root.keep.child", "root.other", "root.other.x" };

            state.ShowOnly("root.keep", available);

            Assert.That(state.ExcludedPaths.OrderBy(x => x).ToArray(),
                Is.EqualTo(new[] { "root.other", "root.other.x" }));
            Assert.That(state.ExcludedWithBufferPaths, Is.Empty);
        }

        [Test]
        public void DontReceive_AddsPathToBothSets()
        {
            var state = new LoggerFilterState();
            state.DontReceive("ip.App", new[] { "ip.App.Child" });

            Assert.That(state.ExcludedPaths, Does.Contain("ip.App"));
            Assert.That(state.ExcludedPaths, Does.Contain("ip.App.Child"));
            Assert.That(state.ExcludedWithBufferPaths, Does.Contain("ip.App"));
            Assert.That(state.ExcludedWithBufferPaths, Does.Contain("ip.App.Child"));
        }

        [Test]
        public void IncludeSubtree_RemovesExclusionFromWholeSubtree()
        {
            var state = new LoggerFilterState();
            state.ExcludeSubtree("ip.App", new[] { "ip.App.Child", "ip.App.Other" });

            state.IncludeSubtree("ip.App", new[] { "ip.App.Child", "ip.App.Other" });

            Assert.That(state.ExcludedPaths, Is.Empty);
        }

        [Test]
        public void ClearAll_OnRoot_ClearsDisplayAndBuffer()
        {
            var state = new LoggerFilterState();
            state.DontReceive("ip.App", new[] { "ip.App.Child" });

            state.ClearAll();

            Assert.That(state.ExcludedPaths, Is.Empty);
            Assert.That(state.ExcludedWithBufferPaths, Is.Empty);
        }

        [Test]
        public void ShowOnly_Root_ShowsEverything()
        {
            var state = new LoggerFilterState();
            state.ExcludeSubtree("a", null);
            state.ShowOnly("Root", new[] { "a", "b" });
            Assert.That(state.ExcludedPaths, Is.Empty);
        }

        [Test]
        public void ShowOnly_RemembersIncludeOnlyForFutureLoggers()
        {
            var state = new LoggerFilterState();
            state.ShowOnly("root.keep", new[] { "root.keep", "root.other" });

            Assert.That(state.IncludeOnlyPaths, Does.Contain("root.keep"));
            Assert.That(state.ShouldHideNewLogger("root.other.new"), Is.True);
            Assert.That(state.ShouldHideNewLogger("root.keep.child"), Is.False);
        }

        [Test]
        public void ToPortableLoggerKey_StripsFileAndIpPrefix()
        {
            Assert.That(LoggerFilterState.ToPortableLoggerKey(@"C:\Users\styor\Downloads\2026-09-18.txt.SecurityLog"),
                Is.EqualTo("SecurityLog"));
            Assert.That(LoggerFilterState.ToPortableLoggerKey(@"C:\Users\styor\Downloads\2026-09-18.txt.Terminal.TerminalLogicalStatusManager"),
                Is.EqualTo("Terminal.TerminalLogicalStatusManager"));
            Assert.That(LoggerFilterState.ToPortableLoggerKey("127.0.0.1.Payments"), Is.EqualTo("Payments"));
            Assert.That(LoggerFilterState.ToPortableLoggerKey("10.0.0.2.Payments.Api"), Is.EqualTo("Payments.Api"));
        }

        [Test]
        public void MatchesIncludeRoot_SameLoggersOnAnotherFile()
        {
            Assert.That(LoggerFilterState.MatchesIncludeRoot(
                @"D:\logs\2026-09-19.txt.SecurityLog.SecurityLogService", "SecurityLog"), Is.True);
            Assert.That(LoggerFilterState.MatchesIncludeRoot(
                @"D:\logs\2026-09-19.txt.App", "SecurityLog"), Is.False);
        }

        [Test]
        public void ReplaceDisplayExclusions_KeepsDontReceiveHidden()
        {
            var state = new LoggerFilterState();
            state.DontReceive("ip.Drop", null);
            state.ReplaceDisplayExclusions(new[] { "ip.Hide" });

            Assert.That(state.ExcludedPaths, Does.Contain("ip.Hide"));
            Assert.That(state.ExcludedPaths, Does.Contain("ip.Drop"));
            Assert.That(state.ExcludedWithBufferPaths, Does.Contain("ip.Drop"));
        }
    }
}
