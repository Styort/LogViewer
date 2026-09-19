using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.Enums;
using LogViewer.MVVM.ViewModels.Log;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class FilterCoordinatorTests
    {
        [Test]
        public void SetMinLevel_WritesExpectedCriteria()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var coordinator = new FilterCoordinator(processing, new LoggerFilterState());

            coordinator.SetMinLevel(eLogLevel.Error);

            Assert.That(session.FilterCriteria.MinLevel, Is.EqualTo(LogLevel.Error));
        }

        [Test]
        public void InvalidRegex_DoesNotActivateSearch()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var coordinator = new FilterCoordinator(processing, new LoggerFilterState());

            coordinator.SetSearch("(", false, false, true, true, true);

            Assert.That(coordinator.IsPatternInvalid, Is.True);
            Assert.That(session.FilterCriteria.IsSearchActive, Is.False);
            Assert.That(session.FilterCriteria.IsSearchPatternInvalid, Is.True);
        }

        [Test]
        public void SetTimeInterval_ActivatesRange()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var coordinator = new FilterCoordinator(processing, new LoggerFilterState());
            var from = new DateTime(2020, 1, 1);
            var to = new DateTime(2020, 1, 2);

            coordinator.SetTimeInterval(from, to);

            Assert.That(session.FilterCriteria.IsTimeIntervalActive, Is.True);
            Assert.That(session.FilterCriteria.TimeRangeFrom, Is.EqualTo(from));
            Assert.That(session.FilterCriteria.TimeRangeTo, Is.EqualTo(to));
        }
    }
}
