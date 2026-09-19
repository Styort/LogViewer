using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class FilterPresetTests
    {
        [Test]
        public void RoundTrip_PreservesDisplayFields()
        {
            var original = new FilterPresetDocument
            {
                Version = FilterPresetDocument.CurrentVersion,
                Presets =
                {
                    new FilterPreset
                    {
                        Name = "Payments errors",
                        MinLevel = "Error",
                        SearchText = "timeout|declined",
                        MatchCase = false,
                        MatchWholeWord = false,
                        UseRegex = true,
                        MatchLogLevel = true,
                        IsSearchActive = true,
                        IsRelativeTimeInterval = true,
                        RelativeMinutes = 15,
                        ExcludedLoggerFullPaths = new List<string> { "127.0.0.1.Other" },
                        IncludedLoggerFullPaths = new List<string> { "127.0.0.1.Payments" }
                    }
                }
            };

            FilterPresetDocument loaded;
            using (var stream = new MemoryStream())
            {
                FilterPresetXmlStore.Save(stream, original);
                stream.Position = 0;
                loaded = FilterPresetXmlStore.Load(stream);
            }

            Assert.That(loaded.Version, Is.EqualTo("1"));
            Assert.That(loaded.Presets.Count, Is.EqualTo(1));
            var preset = loaded.Presets[0];
            Assert.That(preset.Name, Is.EqualTo("Payments errors"));
            Assert.That(preset.MinLevel, Is.EqualTo("Error"));
            Assert.That(preset.SearchText, Is.EqualTo("timeout|declined"));
            Assert.That(preset.UseRegex, Is.True);
            Assert.That(preset.MatchLogLevel, Is.True);
            Assert.That(preset.IsRelativeTimeInterval, Is.True);
            Assert.That(preset.RelativeMinutes, Is.EqualTo(15));
            Assert.That(preset.ExcludedLoggerFullPaths, Is.EqualTo(new[] { "127.0.0.1.Other" }));
            Assert.That(preset.IncludedLoggerFullPaths, Is.EqualTo(new[] { "127.0.0.1.Payments" }));
        }

        [Test]
        public void OldFileWithoutVersion_DefaultsToV1()
        {
            const string xml = @"<?xml version=""1.0""?><FilterPresets><Preset><Name>x</Name></Preset></FilterPresets>";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                var doc = FilterPresetXmlStore.Load(stream);
                Assert.That(doc.Version, Is.EqualTo("1"));
                Assert.That(doc.Presets[0].Name, Is.EqualTo("x"));
            }
        }

        [Test]
        public void Apply_WritesFilterCriteriaFromXml()
        {
            const string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<FilterPresets Version=""1"">
  <Preset>
    <Name>Error + search timeout</Name>
    <MinLevel>Error</MinLevel>
    <SearchText>timeout</SearchText>
    <MatchCase>false</MatchCase>
    <MatchWholeWord>false</MatchWholeWord>
    <UseRegex>false</UseRegex>
    <MatchLogLevel>true</MatchLogLevel>
    <IsSearchActive>true</IsSearchActive>
    <IsTimeIntervalActive>false</IsTimeIntervalActive>
    <ExcludedLoggerFullPaths>
      <Path>ip.Other</Path>
    </ExcludedLoggerFullPaths>
  </Preset>
</FilterPresets>";

            FilterPreset preset;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
                preset = FilterPresetXmlStore.Load(stream).Presets[0];

            var session = new LogSession();
            session.FilterCriteria.ExcludedLoggerFullPathsWithBuffer.Add("ip.DontReceive");
            var excluded = FilterPresetMapper.ComputeExcluded(preset, new[] { "ip.Other", "ip.Payments" });

            session.SetFilterCriteria(new FilterCriteria
            {
                MinLevel = FilterPresetMapper.ParseMinLevel(preset.MinLevel),
                SearchText = preset.SearchText,
                MatchCase = preset.MatchCase,
                MatchWholeWord = preset.MatchWholeWord,
                UseRegex = preset.UseRegex,
                MatchLogLevel = FilterPresetMapper.ResolveMatchLogLevel(preset),
                IsSearchActive = preset.IsSearchActive,
                IsTimeIntervalActive = preset.IsTimeIntervalActive,
                ExcludedLoggerFullPaths = excluded,
                ExcludedLoggerFullPathsWithBuffer = new HashSet<string>(session.FilterCriteria.ExcludedLoggerFullPathsWithBuffer)
            });

            Assert.That(session.FilterCriteria.MinLevel, Is.EqualTo(LogLevel.Error));
            Assert.That(session.FilterCriteria.SearchText, Is.EqualTo("timeout"));
            Assert.That(session.FilterCriteria.IsSearchActive, Is.True);
            Assert.That(session.FilterCriteria.ExcludedLoggerFullPaths, Does.Contain("ip.Other"));
            Assert.That(session.FilterCriteria.ExcludedLoggerFullPathsWithBuffer, Does.Contain("ip.DontReceive"));
        }

        [Test]
        public void RelativeInterval_IsRecomputedOnApply()
        {
            var preset = new FilterPreset
            {
                IsRelativeTimeInterval = true,
                RelativeMinutes = 15
            };
            var now = new DateTime(2026, 9, 19, 12, 0, 0);
            bool active;
            DateTime from;
            DateTime to;
            FilterPresetMapper.ResolveTimeRange(preset, now, out active, out from, out to);
            Assert.That(active, Is.True);
            Assert.That(to, Is.EqualTo(now));
            Assert.That(from, Is.EqualTo(now.AddMinutes(-15)));
        }

        [Test]
        public void CaptureIncluded_DoesNotCollapseToFileSourceWhenChildrenAreMixed()
        {
            const string file = @"C:\Users\styor\Downloads\2026-09-18.txt";
            var known = new[]
            {
                file,
                file + ".Microservices",
                file + ".SecurityLog",
                file + ".SecurityLog.SecurityLogService",
                file + ".Terminal",
                file + ".Terminal.TerminalLogicalStatusManager",
                file + ".App"
            };
            var excluded = new[]
            {
                file + ".Microservices",
                file + ".App"
            };

            var included = FilterPresetMapper.CaptureIncluded(known, excluded);

            Assert.That(included.OrderBy(x => x).ToArray(),
                Is.EqualTo(new[]
                {
                    "SecurityLog",
                    "Terminal"
                }));
        }

        [Test]
        public void IncludeOnly_PortableLoggerName_MatchesAnotherFile()
        {
            var preset = new FilterPreset
            {
                IncludedLoggerFullPaths = new List<string>
                {
                    @"C:\Users\styor\Downloads\2026-09-18.txt.SecurityLog",
                    @"C:\Users\styor\Downloads\2026-09-18.txt.Terminal"
                }
            };
            const string other = @"D:\logs\2026-09-19.txt";
            var excluded = FilterPresetMapper.ComputeExcluded(preset, new[]
            {
                other + ".SecurityLog",
                other + ".SecurityLog.SecurityLogService",
                other + ".Terminal",
                other + ".App"
            });
            Assert.That(excluded.OrderBy(x => x).ToArray(), Is.EqualTo(new[] { other + ".App" }));
            Assert.That(FilterPresetMapper.ResolveIncluded(preset, null),
                Is.EqualTo(new[] { "SecurityLog", "Terminal" }));
        }

        [Test]
        public void IncludeOnly_HidesKnownLoggersOutsideSubtree()
        {
            var preset = new FilterPreset
            {
                IncludedLoggerFullPaths = new List<string> { "ip.Payments" }
            };
            var excluded = FilterPresetMapper.ComputeExcluded(preset, new[] { "ip.Payments", "ip.Payments.Api", "ip.Other" });
            Assert.That(excluded.OrderBy(x => x).ToArray(), Is.EqualTo(new[] { "ip.Other" }));
        }

        [Test]
        public void CaptureIncluded_KeepsOnlyVisibleRoots_NotEveryHiddenLogger()
        {
            var known = new[]
            {
                "file.BackgroundTasks.Ping",
                "file.BackgroundTasks.Ping.PingScheduledService",
                "file.Core",
                "file.Core.Navigator",
                "file.Other"
            };
            var excluded = new[]
            {
                "Root",
                "file",
                "file.BackgroundTasks.Ping",
                "file.Core",
                "file.Other"
            };

            var included = FilterPresetMapper.CaptureIncluded(known, excluded);

            Assert.That(included.OrderBy(x => x).ToArray(),
                Is.EqualTo(new[]
                {
                    "file.BackgroundTasks.Ping.PingScheduledService",
                    "file.Core.Navigator"
                }));
        }

        [Test]
        public void ResolveIncluded_FromLegacyExcludedDump_UsesKnownTree()
        {
            var preset = new FilterPreset
            {
                ExcludedLoggerFullPaths = new List<string> { "Root", "file.Other" },
                IncludedLoggerFullPaths = new List<string>()
            };
            var included = FilterPresetMapper.ResolveIncluded(preset, new[] { "file.Keep", "file.Other" });
            Assert.That(included, Is.EqualTo(new[] { "file.Keep" }));
        }
    }
}
