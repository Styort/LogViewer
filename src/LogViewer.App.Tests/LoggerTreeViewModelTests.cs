using System.Collections.Generic;
using System.Threading;
using LogViewer.Adapters;
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
                var receivers = new ReceiversViewModel(processing, adapter, coordinator, new Factories.UdpSourceFactory(settings), dialogs, settings.Receivers);
                var watch = new LogFileWatchService(processing);
                var import = new ImportViewModel(state, session, processing, new LogImportService(session), adapter, watch, dialogs, new FakeFiles(), receivers, m => "");
                var tree = new LoggerTreeViewModel(state, coordinator, session, processing, watch, new LoggerTreeBuilder(), new LoggerTreeMarker(state, settings), settings, receivers, import, () => { });
                return new TreeEnv { Tree = tree, Coordinator = coordinator, Settings = settings };
            }
        }
    }
}
