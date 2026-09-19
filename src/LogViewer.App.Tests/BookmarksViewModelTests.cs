using System;
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
    }
}
