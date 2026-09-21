using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LogViewer.Adapters;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.MVVM.TreeView;
using LogViewer.MVVM.ViewModels.Log;
using LogViewer.Services;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)] // LogMessage / Node pull WPF types (SolidColorBrush).
    public class LoggerTreeViewModelTests
    {
        [Test]
        public void CheckUncheckShowOnlyAndDontReceive_UpdateFilterState()
        {
            var env = TreeEnv.Create();
            env.Tree.SeedAvailableLoggers(new[] { "ip.App", "ip.App.Child", "ip.Other" });
            var app = Child(env.Tree.Loggers[0], "App", "ip.App");
            var child = Child(app, "Child", "ip.App.Child");
            var other = Child(env.Tree.Loggers[0], "Other", "ip.Other");
            env.Tree.Loggers[0].Children.Add(app);
            app.Children.Add(child);
            env.Tree.Loggers[0].Children.Add(other);

            app.IsChecked = false;
            env.Tree.TreeViewElementCheckCommand.Execute(app);
            Assert.That(env.Coordinator.Loggers.ExcludedPaths, Does.Contain("ip.App"));
            Assert.That(env.Coordinator.Loggers.ExcludedPaths, Does.Contain("ip.App.Child"));
            Assert.That(env.Coordinator.Loggers.ExcludedPaths, Does.Not.Contain("ip.Other"));

            app.IsChecked = true;
            env.Tree.TreeViewElementCheckCommand.Execute(app);
            Assert.That(env.Coordinator.Loggers.ExcludedPaths, Is.Empty);

            env.Tree.ShowOnlyThisLoggerCommand.Execute(app);
            Assert.That(env.Coordinator.Loggers.ExcludedPaths, Does.Contain("ip.Other"));
            Assert.That(env.Coordinator.Loggers.ExcludedPaths, Does.Not.Contain("ip.App"));

            env.Tree.DontReceiveThisLoggerCommand.Execute(app);
            Assert.That(env.Coordinator.Loggers.ExcludedWithBufferPaths, Does.Contain("ip.App"));
        }

        [Test]
        public void SyncCheckboxesFromExclusions_ChecksIncludedLeavesAndMixesParents()
        {
            var env = TreeEnv.Create();
            env.Tree.SeedAvailableLoggers(new[]
            {
                "file.BackgroundTasks.Ping",
                "file.BackgroundTasks.Ping.PingScheduledService",
                "file.Core",
                "file.Core.Navigator",
                "file.Other"
            });

            var file = Child(env.Tree.Loggers[0], "file", "file");
            var pingParent = Child(file, "Ping", "file.BackgroundTasks.Ping");
            var ping = Child(pingParent, "PingScheduledService", "file.BackgroundTasks.Ping.PingScheduledService");
            var core = Child(file, "Core", "file.Core");
            var navigator = Child(core, "Navigator", "file.Core.Navigator");
            var other = Child(file, "Other", "file.Other");
            env.Tree.Loggers[0].Children.Add(file);
            file.Children.Add(pingParent);
            pingParent.Children.Add(ping);
            file.Children.Add(core);
            core.Children.Add(navigator);
            file.Children.Add(other);

            env.Coordinator.ApplyPreset(new FilterPreset
            {
                ExcludedLoggerFullPaths = new List<string>
                {
                    "Root",
                    "file",
                    "file.BackgroundTasks.Ping",
                    "file.Core",
                    "file.Other"
                }
            }, System.DateTime.Now, env.Tree.AvailableLoggerPaths);

            env.Tree.SyncCheckboxesFromExclusions();

            Assert.That(ping.IsChecked, Is.True);
            Assert.That(navigator.IsChecked, Is.True);
            Assert.That(other.IsChecked, Is.False);
            Assert.That(file.IsChecked, Is.Null);
            Assert.That(env.Tree.Loggers[0].IsChecked, Is.Not.False);
        }

        [Test]
        public void CollectIncludedRoots_TakesCheckedSubtreesUnderMixedFile()
        {
            var env = TreeEnv.Create();
            const string file = @"C:\Users\styor\Downloads\2026-09-18.txt";
            var fileNode = Child(env.Tree.Loggers[0], file, file);
            var security = Child(fileNode, "SecurityLog", file + ".SecurityLog");
            var securitySvc = Child(security, "SecurityLogService", file + ".SecurityLog.SecurityLogService");
            var terminal = Child(fileNode, "Terminal", file + ".Terminal");
            var app = Child(fileNode, "App", file + ".App");
            env.Tree.Loggers[0].Children.Add(fileNode);
            fileNode.Children.Add(security);
            security.Children.Add(securitySvc);
            fileNode.Children.Add(terminal);
            fileNode.Children.Add(app);

            app.IsChecked = false;
            env.Tree.Loggers[0].IsChecked = null;
            fileNode.IsChecked = null;

            var roots = env.Tree.CollectIncludedRoots();
            Assert.That(roots.OrderBy(x => x).ToArray(),
                Is.EqualTo(new[] { file + ".SecurityLog", file + ".Terminal" }));
        }

        [Test]
        public void IgnoreIp_AddsActiveIgnoredAddress()
        {
            var env = TreeEnv.Create();
            var ipNode = Child(env.Tree.Loggers[0], "127.0.0.1", "127.0.0.1");
            ipNode.IsRoot = true;
            env.Tree.Loggers[0].Children.Add(ipNode);

            env.Tree.IgnoreThisIPCommand.Execute(ipNode);

            Assert.That(env.Settings.IgnoredIPs.Count, Is.EqualTo(1));
            Assert.That(env.Settings.IgnoredIPs[0].IP, Is.EqualTo("127.0.0.1"));
            Assert.That(env.Settings.IgnoredIPs[0].IsActive, Is.True);
        }

        [Test]
        public void CollectUncheckedLoggerPaths_ListsUncheckedNodes()
        {
            var env = TreeEnv.Create();
            var app = Child(env.Tree.Loggers[0], "App", "ip.App");
            var other = Child(env.Tree.Loggers[0], "Other", "ip.Other");
            env.Tree.Loggers[0].Children.Add(app);
            env.Tree.Loggers[0].Children.Add(other);
            app.IsChecked = false;
            env.Tree.Loggers[0].IsChecked = null;

            var uncheckedPaths = env.Tree.CollectUncheckedLoggerPaths();
            Assert.That(uncheckedPaths, Does.Contain("ip.App"));
            Assert.That(uncheckedPaths, Does.Not.Contain("ip.Other"));
        }

        [Test]
        public void QueueDisplayFilterRestore_SurvivesOnSessionCleared()
        {
            var env = TreeEnv.Create();
            env.Coordinator.Loggers.ExcludeSubtree("ip.Hide", null);
            env.Coordinator.Loggers.SetIncludeOnly(new[] { "ip.Keep" });
            env.Tree.QueueDisplayFilterRestore();

            env.Coordinator.Loggers.ClearDisplayExclusions();
            env.Tree.OnSessionCleared();

            Assert.That(env.Coordinator.Loggers.ExcludedPaths, Does.Contain("ip.Hide"));
            Assert.That(env.Coordinator.Loggers.IncludeOnlyPaths, Does.Contain("ip.Keep"));
        }

        private static Node Child(Node parent, string text, string logger)
        {
            return new Node(parent, text) { Logger = logger, IsChecked = true };
        }

        private sealed class TreeEnv
        {
            public LoggerTreeViewModel Tree;
            public FilterCoordinator Coordinator;
            public FakeAppSettings Settings;

            public static TreeEnv Create()
            {
                var session = new LogSession();
                var processing = new LogProcessingService(session, new LogFilter());
                var state = new LogViewState();
                var coordinator = new FilterCoordinator(processing, new LoggerFilterState());
                var settings = new FakeAppSettings();
                var dialogs = new FakeDialogs();
                var sync = new SynchronizationContext();
                var adapter = new CoreToUiAdapter(sync, processing);
                var receivers = new ReceiversViewModel(processing, adapter, coordinator, new Factories.LogSourceFactory(settings), dialogs, settings.Receivers);
                var watch = new LogFileWatchService(processing);
                var import = new ImportViewModel(state, session, processing, new LogImportService(session), adapter, watch, dialogs, new FakeFiles(), receivers, m => "");
                var tree = new LoggerTreeViewModel(state, coordinator, session, processing, watch, new LoggerTreeBuilder(), new LoggerTreeMarker(state, settings), settings, receivers, import, () => { });
                return new TreeEnv { Tree = tree, Coordinator = coordinator, Settings = settings };
            }
        }
    }
}
