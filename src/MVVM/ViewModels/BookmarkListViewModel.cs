using System;
using System.Collections.ObjectModel;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;

namespace LogViewer.MVVM.ViewModels
{
    public class BookmarkListViewModel : BaseViewModel
    {
        private LogBookmark selectedBookmark;
        private RelayCommand goToCommand;
        private RelayCommand editCommentCommand;
        private RelayCommand removeCommand;

        public BookmarkListViewModel(ObservableCollection<LogBookmark> bookmarks)
        {
            Bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));
        }

        public event EventHandler<LogBookmark> NavigateToLog;
        public event EventHandler<LogBookmark> EditCommentRequested;
        public event EventHandler<LogBookmark> RemoveRequested;

        public ObservableCollection<LogBookmark> Bookmarks { get; }

        public LogBookmark SelectedBookmark
        {
            get => selectedBookmark;
            set
            {
                selectedBookmark = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand GoToCommand => goToCommand ?? (goToCommand = new RelayCommand(GoTo, CanOperate));
        public RelayCommand EditCommentCommand => editCommentCommand ?? (editCommentCommand = new RelayCommand(EditComment, CanOperate));
        public RelayCommand RemoveCommand => removeCommand ?? (removeCommand = new RelayCommand(Remove, CanOperate));

        public void GoToItem(LogBookmark bookmark)
        {
            if (bookmark == null)
                return;
            NavigateToLog?.Invoke(this, bookmark);
        }

        private bool CanOperate(object obj)
        {
            return (obj as LogBookmark ?? SelectedBookmark) != null;
        }

        private void GoTo(object obj)
        {
            GoToItem(obj as LogBookmark ?? SelectedBookmark);
        }

        private void EditComment(object obj)
        {
            var bookmark = obj as LogBookmark ?? SelectedBookmark;
            if (bookmark == null)
                return;
            EditCommentRequested?.Invoke(this, bookmark);
        }

        private void Remove(object obj)
        {
            var bookmark = obj as LogBookmark ?? SelectedBookmark;
            if (bookmark == null)
                return;
            RemoveRequested?.Invoke(this, bookmark);
        }
    }
}
