using System;
using LogViewer.Core.Domain;
using LogViewer.Helpers;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels.Log;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class BookmarksViewModelTests
    {
        [Test]
        public void AddAndRemoveBookmark_UpdatesCollection()
        {
            var state = new LogViewState();
            var log = new LogMessage { Message = "a", Time = DateTime.UtcNow };
            state.AllLogs.Add(log);
            state.Logs.Add(log);
            state.SelectedLog = log;
            var dialogs = new FakeDialogs { PromptCommentResult = true, CommentToReturn = "c" };
            var vm = new BookmarksViewModel(state, dialogs);

            vm.AddBookmarkCommand.Execute(log);
            Assert.That(vm.Bookmarks.Count, Is.EqualTo(1));
            Assert.That(log.HasBookmark, Is.True);

            vm.RemoveBookmarkCommand.Execute(log);
            Assert.That(vm.Bookmarks, Is.Empty);
            Assert.That(log.HasBookmark, Is.False);
        }

        [Test]
        public void SyncWithAllLogs_DropsMissingRows()
        {
            var state = new LogViewState();
            var log = new LogMessage { Message = "a", Time = DateTime.UtcNow };
            state.AllLogs.Add(log);
            state.Logs.Add(log);
            var vm = new BookmarksViewModel(state, new FakeDialogs());
            vm.AddBookmarkCommand.Execute(log);

            state.AllLogs = new AsyncObservableCollection<LogMessage>();
            Assert.That(vm.Bookmarks, Is.Empty);
        }

        [Test]
        public void QueueRestore_AppliesIndexAndSkipsOutOfRange()
        {
            var state = new LogViewState();
            var first = new LogMessage { Message = "a", Time = DateTime.UtcNow };
            var second = new LogMessage { Message = "b", Time = DateTime.UtcNow };
            state.AllLogs.Add(first);
            state.AllLogs.Add(second);
            var vm = new BookmarksViewModel(state, new FakeDialogs());

            vm.QueueRestore(new[]
            {
                new SavedSessionBookmark { Index = 1, Comment = "note" },
                new SavedSessionBookmark { Index = 99, Comment = "bad" }
            });

            Assert.That(vm.Bookmarks.Count, Is.EqualTo(1));
            Assert.That(vm.Bookmarks[0].Log, Is.SameAs(second));
            Assert.That(vm.Bookmarks[0].Comment, Is.EqualTo("note"));
            Assert.That(second.HasBookmark, Is.True);
            Assert.That(first.HasBookmark, Is.False);
        }

        [Test]
        public void NavigateToBookmark_FindsVisibleRowWithSameFields()
        {
            var state = new LogViewState();
            var original = new LogMessage { Message = "a", Time = DateTime.UtcNow, Logger = "App", Address = "127.0.0.1" };
            var visible = new LogMessage { Message = "a", Time = original.Time, Logger = "App", Address = "127.0.0.1" };
            state.AllLogs.Add(original);
            state.Logs.Add(visible);
            var dialogs = new FakeDialogs();
            var vm = new BookmarksViewModel(state, dialogs);
            vm.QueueRestore(new[] { new SavedSessionBookmark { Index = 0, Comment = "x" } });

            vm.NavigateToBookmark(vm.Bookmarks[0]);

            Assert.That(dialogs.LastInformation, Is.Null);
            Assert.That(state.SelectedLog, Is.SameAs(visible));
        }
    }
}
