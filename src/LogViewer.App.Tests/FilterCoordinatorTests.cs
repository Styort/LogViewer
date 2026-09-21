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

        [Test]
        public void ApplyPreset_WritesCriteriaAndKeepsDontReceive()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var loggers = new LoggerFilterState();
            loggers.ExcludeFromBuffer("ip.Drop");
            var coordinator = new FilterCoordinator(processing, loggers);

            coordinator.ApplyPreset(new FilterPreset
            {
                Name = "Error + search timeout",
                MinLevel = "Error",
                SearchText = "timeout",
                UseRegex = false,
                MatchLogLevel = true,
                IsSearchActive = true,
                ExcludedLoggerFullPaths = new List<string> { "ip.Other" }
            }, DateTime.Now, new[] { "ip.Other", "ip.Payments" });

            Assert.That(session.FilterCriteria.MinLevel, Is.EqualTo(LogLevel.Error));
            Assert.That(session.FilterCriteria.SearchText, Is.EqualTo("timeout"));
            Assert.That(session.FilterCriteria.IsSearchActive, Is.True);
            Assert.That(session.FilterCriteria.ExcludedLoggerFullPaths, Does.Contain("ip.Other"));
            Assert.That(session.FilterCriteria.ExcludedLoggerFullPathsWithBuffer, Does.Contain("ip.Drop"));
        }

        [Test]
        public void ApplyPreset_InvalidRegex_SetsIndicatorAndDoesNotActivateSearch()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var coordinator = new FilterCoordinator(processing, new LoggerFilterState());

            coordinator.ApplyPreset(new FilterPreset
            {
                SearchText = "(",
                UseRegex = true,
                IsSearchActive = true
            }, DateTime.Now, Array.Empty<string>());

            Assert.That(coordinator.IsPatternInvalid, Is.True);
            Assert.That(session.FilterCriteria.IsSearchActive, Is.False);
            Assert.That(session.FilterCriteria.IsSearchPatternInvalid, Is.True);
            Assert.That(session.FilterCriteria.SearchText, Is.EqualTo("("));
        }

        [Test]
        public void CapturePreset_StoresIncludedLoggersNotExcludedDump()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var coordinator = new FilterCoordinator(processing, new LoggerFilterState());

            coordinator.ApplyPreset(new FilterPreset
            {
                Name = "keep",
                IncludedLoggerFullPaths = new List<string> { "ip.Payments" }
            }, DateTime.Now, new[] { "ip.Other", "ip.Payments" });

            var captured = coordinator.CapturePreset("keep", new[] { "ip.Other", "ip.Payments" });
            Assert.That(captured.IncludedLoggerFullPaths, Is.EqualTo(new[] { "Payments" }));
            Assert.That(captured.ExcludedLoggerFullPaths, Is.Empty);
            Assert.That(session.FilterCriteria.IncludedLoggerFullPaths, Does.Contain("Payments"));
            Assert.That(session.FilterCriteria.ExcludedLoggerFullPaths, Does.Contain("ip.Other"));
        }

        [Test]
        public void ApplySessionFilter_ReplacesDontReceive()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var loggers = new LoggerFilterState();
            loggers.ExcludeFromBuffer("ip.Old");
            var coordinator = new FilterCoordinator(processing, loggers);

            coordinator.ApplySessionFilter(new SavedSessionDocument
            {
                MinLevel = "Warn",
                SearchText = "boom",
                IsSearchActive = true,
                ExcludedLoggerFullPaths = new List<string> { "ip.Hide" },
                DontReceiveLoggerFullPaths = new List<string> { "ip.Drop" }
            });

            Assert.That(session.FilterCriteria.MinLevel, Is.EqualTo(LogLevel.Warn));
            Assert.That(session.FilterCriteria.SearchText, Is.EqualTo("boom"));
            Assert.That(session.FilterCriteria.ExcludedLoggerFullPathsWithBuffer, Does.Contain("ip.Drop"));
            Assert.That(session.FilterCriteria.ExcludedLoggerFullPathsWithBuffer, Does.Not.Contain("ip.Old"));
            Assert.That(session.FilterCriteria.ExcludedLoggerFullPaths, Does.Contain("ip.Hide"));
        }

        [Test]
        public void ApplySessionFilter_ComputesExcludedFromIncludedRoots()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var coordinator = new FilterCoordinator(processing, new LoggerFilterState());
            var known = new[] { "127.0.0.1.Pay", "127.0.0.1.Other" };

            coordinator.ApplySessionFilter(new SavedSessionDocument
            {
                IncludedLoggerFullPaths = new List<string> { "Pay" },
                ExcludedLoggerFullPaths = new List<string>()
            }, known, applyDontReceive: false);

            Assert.That(session.FilterCriteria.ExcludedLoggerFullPaths, Does.Contain("127.0.0.1.Other"));
            Assert.That(session.FilterCriteria.ExcludedLoggerFullPaths, Does.Not.Contain("127.0.0.1.Pay"));
            Assert.That(session.FilterCriteria.IncludedLoggerFullPaths, Does.Contain("Pay"));
        }

        [Test]
        public void CaptureSessionFilter_UsesTreeIncludedRoots()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var coordinator = new FilterCoordinator(processing, new LoggerFilterState());
            var document = new SavedSessionDocument();

            coordinator.CaptureSessionFilter(document, new[] { "Pay", "Core.Navigator" });

            Assert.That(document.IncludedLoggerFullPaths, Is.EqualTo(new[] { "Pay", "Core.Navigator" }));
        }
    }
}
