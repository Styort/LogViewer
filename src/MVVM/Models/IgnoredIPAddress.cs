using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Models
{
    public class IgnoredIPAddress : BaseViewModel
    {
        private string ip;
        private bool isActive = true;

        public string IP
        {
            get => ip;
            set
            {
                ip = value;
                OnPropertyChanged();
            }
        }

        public bool IsActive
        {
            get => isActive;
            set
            {
                isActive = value;
                OnPropertyChanged();
            }
        }
    }
}
