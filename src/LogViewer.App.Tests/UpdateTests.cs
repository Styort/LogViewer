using System;
using System.IO;
using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using LogViewer.MVVM.Views;
using LogViewer.Services.Updates;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    public class AppDirectoryAccessTests
    {
        private string _dir;
        private FileSystemAccessRule _denyRule;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "LogViewerWriteTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (_denyRule != null)
            {
                var security = Directory.GetAccessControl(_dir);
                security.RemoveAccessRule(_denyRule);
                Directory.SetAccessControl(_dir, security);
                _denyRule = null;
            }
            Directory.Delete(_dir, true);
        }

        [Test]
        public void WritableFolder_IsWritable_AndLeavesNoProbeFile()
        {
            Assert.That(AppDirectoryAccess.IsWritable(_dir), Is.True);
            Assert.That(Directory.GetFileSystemEntries(_dir), Is.Empty);
        }

        [Test]
        public void MissingFolder_IsNotWritable()
        {
            Assert.That(AppDirectoryAccess.IsWritable(Path.Combine(_dir, "missing")), Is.False);
        }

        [Test]
        public void FolderDenyingFileCreation_IsNotWritable()
        {
            _denyRule = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,
                FileSystemRights.CreateFiles | FileSystemRights.WriteData, AccessControlType.Deny);
            var security = Directory.GetAccessControl(_dir);
            security.AddAccessRule(_denyRule);
            Directory.SetAccessControl(_dir, security);

            Assert.That(AppDirectoryAccess.IsWritable(_dir), Is.False);
        }
    }

    /// <summary>
    /// XAML is only parsed at run time; a broken binding there surfaces as a silently missing update dialog.
    /// </summary>
    [TestFixture]
    public class NewUpdateAvailableDialogTests
    {
        [Test]
        public void Dialog_Loads()
        {
            // Styles.xaml needs the App.xaml theme dictionaries at Application level, and a process-wide
            // Application changes how other view-model tests dispatch. A throwaway AppDomain keeps it contained.
            var domain = AppDomain.CreateDomain(nameof(NewUpdateAvailableDialogTests), null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var probe = (DialogProbe)domain.CreateInstanceAndUnwrap(
                    typeof(DialogProbe).Assembly.FullName, typeof(DialogProbe).FullName);
                Assert.That(probe.LoadDialog(), Is.Null);
            }
            finally
            {
                try
                {
                    AppDomain.Unload(domain);
                }
                catch (CannotUnloadAppDomainException)
                {
                    // A lingering WPF dispatcher thread can block the unload; the result is already known.
                }
            }
        }

        public sealed class DialogProbe : MarshalByRefObject
        {
            private static readonly string[] ThemeDictionaries =
            {
                "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml",
                "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml",
                "pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Primary/MaterialDesignColor.Indigo.xaml",
                "pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Accent/MaterialDesignColor.Lime.xaml"
            };

            /// <summary>null on success, otherwise the exception text.</summary>
            public string LoadDialog()
            {
                string error = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                        foreach (var source in ThemeDictionaries)
                            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source) });

                        var dialog = new NewUpdateAvailableDialog(new UpdateCheckResult
                        {
                            IsUpdateAvailable = true,
                            InstalledVersion = new Version(1, 2, 8, 9),
                            AvailableVersion = "1.2.9.0",
                            ChangelogUrl = "https://github.com/Styort/LogViewer/releases/tag/v1.2.9.0"
                        }, saveSession: null);
                        if (dialog.Choice != UpdateChoice.Dismissed)
                            error = "Choice must default to Dismissed, was " + dialog.Choice;
                        dialog.Close();
                        app.Dispatcher.InvokeShutdown();
                    }
                    catch (Exception e)
                    {
                        error = e.ToString();
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
                return error;
            }
        }
    }

    [TestFixture]
    public class ReleaseNotesGateTests
    {
        private static readonly Version Current = new Version(1, 2, 9, 0);

        [Test]
        public void FreshInstall_DoesNotShow_ButStoresVersion()
        {
            Assert.That(ReleaseNotesGate.ShouldShow(Current, null), Is.False);
            Assert.That(ReleaseNotesGate.NeedsStore(Current, null), Is.True);
        }

        [Test]
        public void AfterUpdate_ShowsOnce()
        {
            Assert.That(ReleaseNotesGate.ShouldShow(Current, "1.2.8.9"), Is.True);
            Assert.That(ReleaseNotesGate.NeedsStore(Current, "1.2.8.9"), Is.True);

            // The caller stores Current; the next start must not show the notes again.
            var stored = Current.ToString();
            Assert.That(ReleaseNotesGate.ShouldShow(Current, stored), Is.False);
            Assert.That(ReleaseNotesGate.NeedsStore(Current, stored), Is.False);
        }

        [Test]
        public void OlderBuildStarted_DoesNotShow()
        {
            Assert.That(ReleaseNotesGate.ShouldShow(Current, "1.3.0.0"), Is.False);
        }

        [Test]
        public void GarbageStoredValue_DoesNotShow()
        {
            Assert.That(ReleaseNotesGate.ShouldShow(Current, "not a version"), Is.False);
        }
    }

    [TestFixture]
    public class UpdateCheckHandlerTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 3, 12, 0, 0);

        private FakeDialogs _dialogs;
        private FakePrompt _prompt;
        private FakeInstaller _installer;
        private FakeUpdateState _state;
        private UpdateCheckHandler _handler;

        [SetUp]
        public void SetUp()
        {
            _dialogs = new FakeDialogs();
            _prompt = new FakePrompt();
            _installer = new FakeInstaller();
            _state = new FakeUpdateState();
            _handler = new UpdateCheckHandler(_prompt, _dialogs, _installer, _state, () => Now);
        }

        private static UpdateCheckResult Available(string version = "1.2.9.0") => new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            InstalledVersion = new Version(1, 2, 8, 9),
            AvailableVersion = version,
            ChangelogUrl = "https://github.com/Styort/LogViewer/releases/tag/v" + version
        };

        private static UpdateCheckResult UpToDate() => new UpdateCheckResult
        {
            IsUpdateAvailable = false,
            InstalledVersion = new Version(1, 2, 9, 0),
            AvailableVersion = "1.2.9.0"
        };

        private static UpdateCheckResult Failed() => new UpdateCheckResult { Error = new WebException("offline") };

        [Test]
        public void BackgroundCheck_ErrorAndUpToDate_AreSilent()
        {
            _handler.Handle(Failed(), manual: false);
            _handler.Handle(UpToDate(), manual: false);

            Assert.That(_dialogs.LastError, Is.Null);
            Assert.That(_dialogs.LastInformation, Is.Null);
            Assert.That(_prompt.AskCount, Is.Zero);
        }

        [Test]
        public void ManualCheck_ReportsError()
        {
            _handler.Handle(Failed(), manual: true);

            Assert.That(_dialogs.LastError, Does.Contain("offline"));
            Assert.That(_prompt.AskCount, Is.Zero);
        }

        [Test]
        public void ManualCheck_ReportsUpToDate()
        {
            _handler.Handle(UpToDate(), manual: true);

            Assert.That(_dialogs.LastInformation, Is.Not.Null.And.Not.Empty);
            Assert.That(_prompt.AskCount, Is.Zero);
        }

        [Test]
        public void Skip_StoresVersion_AndHidesItFromBackgroundChecks()
        {
            _prompt.Choice = UpdateChoice.Skip;
            _handler.Handle(Available(), manual: false);

            Assert.That(_state.SkippedVersion, Is.EqualTo(new Version(1, 2, 9, 0)));

            _handler.Handle(Available(), manual: false);
            Assert.That(_prompt.AskCount, Is.EqualTo(1));
        }

        [Test]
        public void SkippedVersion_IsStillOfferedByManualCheck()
        {
            _state.SkippedVersion = new Version(1, 2, 9, 0);

            _handler.Handle(Available(), manual: true);

            Assert.That(_prompt.AskCount, Is.EqualTo(1));
        }

        [Test]
        public void NewerThanSkipped_IsOffered()
        {
            _state.SkippedVersion = new Version(1, 2, 9, 0);

            _handler.Handle(Available("1.2.9.1"), manual: false);

            Assert.That(_prompt.AskCount, Is.EqualTo(1));
        }

        [Test]
        public void RemindLater_PostponesBackgroundChecksUntilTheDelayPasses()
        {
            _prompt.Choice = UpdateChoice.RemindLater;
            _handler.Handle(Available(), manual: false);

            Assert.That(_state.RemindLaterAt, Is.EqualTo(Now + UpdateCheckHandler.RemindLaterDelay));
            Assert.That(_state.SkippedVersion, Is.Null);

            _handler.Handle(Available(), manual: false);
            Assert.That(_prompt.AskCount, Is.EqualTo(1));

            var later = new UpdateCheckHandler(_prompt, _dialogs, _installer, _state,
                () => Now + UpdateCheckHandler.RemindLaterDelay + TimeSpan.FromMinutes(1));
            later.Handle(Available(), manual: false);
            Assert.That(_prompt.AskCount, Is.EqualTo(2));
        }

        [Test]
        public void Dismissed_StoresNothing()
        {
            _prompt.Choice = UpdateChoice.Dismissed;

            _handler.Handle(Available(), manual: false);

            Assert.That(_state.SkippedVersion, Is.Null);
            Assert.That(_state.RemindLaterAt, Is.Null);
            Assert.That(_installer.DownloadCount, Is.Zero);
        }

        [Test]
        public void Update_ExitsOnlyAfterSuccessfulDownload()
        {
            _prompt.Choice = UpdateChoice.Update;

            _installer.DownloadResult = false;
            _handler.Handle(Available(), manual: false);
            Assert.That(_installer.DownloadCount, Is.EqualTo(1));
            Assert.That(_installer.Exited, Is.False);

            _installer.DownloadResult = true;
            _handler.Handle(Available(), manual: false);
            Assert.That(_installer.DownloadCount, Is.EqualTo(2));
            Assert.That(_installer.Exited, Is.True);
        }

        private sealed class FakePrompt : IUpdatePrompt
        {
            public UpdateChoice Choice = UpdateChoice.Dismissed;
            public int AskCount;

            public UpdateChoice Ask(UpdateCheckResult update)
            {
                AskCount++;
                return Choice;
            }
        }

        private sealed class FakeInstaller : IUpdateInstaller
        {
            public bool DownloadResult;
            public int DownloadCount;
            public bool Exited;

            public bool Download(UpdateCheckResult update)
            {
                DownloadCount++;
                return DownloadResult;
            }

            public void ExitForUpdate() => Exited = true;
        }

        private sealed class FakeUpdateState : IUpdateStateStore
        {
            public Version SkippedVersion { get; set; }
            public DateTime? RemindLaterAt { get; set; }
        }
    }
}
