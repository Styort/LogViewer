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
    }
}
