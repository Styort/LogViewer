using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LogViewer.Adapters;
using LogViewer.MVVM.Models;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class LogMessageEntryViewTests
    {
        [Test]
        public void View_ReturnsEachRowsOwnEntry_AndFollowsTheLiveList()
        {
            var rows = new List<LogMessage> { TestRows.Row("a"), TestRows.Row("b") };
            var view = new LogMessageEntryView(rows);

            Assert.That(view.Count, Is.EqualTo(2));
            Assert.That(view[0], Is.SameAs(rows[0].Entry));
            Assert.That(view.ToList(), Is.EqualTo(rows.Select(r => r.Entry).ToList()));

            rows.Add(TestRows.Row("c"));
            Assert.That(view.Count, Is.EqualTo(3));
            Assert.That(view[2], Is.SameAs(rows[2].Entry));
        }

        [Test]
        public void View_OverNullList_IsEmpty()
        {
            Assert.That(new LogMessageEntryView(null), Is.Empty);
        }
    }
}
