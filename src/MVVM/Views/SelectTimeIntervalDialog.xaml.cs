using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LogViewer.MVVM.ViewModels;
using MaterialDesignThemes.Wpf;

namespace LogViewer.MVVM.Views
{
    /// <summary>
    /// Interaction logic for SelectTimeIntervalDialog.xaml
    /// </summary>
    public partial class SelectTimeIntervalDialog : Window
    {
        public DateTime SelectedDateFrom { get; set; } = DateTime.Now;
        public DateTime SelectedTimeFrom { get; set; } = DateTime.Now;
        public DateTime SelectedDateTo { get; set; } = DateTime.Now;
        public DateTime SelectedTimeTo { get; set; } = DateTime.Now;

        public SelectTimeIntervalDialog(DateTime? currentLogDateTime)
        {
            InitializeComponent();

            if (currentLogDateTime.HasValue)
            {
                SelectedDateFrom = currentLogDateTime.Value;
                SelectedTimeFrom = currentLogDateTime.Value;
                SelectedDateTo = currentLogDateTime.Value;
                SelectedTimeTo = currentLogDateTime.Value;
            }
            else
            {
                // обнуляем секунды и мс. для удобства работы с датой
                if (SelectedTimeFrom.Second != 0)
                    SelectedTimeFrom = SelectedTimeFrom.AddSeconds(-SelectedTimeFrom.Second);
                if (SelectedTimeFrom.Millisecond != 0)
                    SelectedTimeFrom = SelectedTimeFrom.AddMilliseconds(-SelectedTimeFrom.Millisecond);
                if (SelectedTimeTo.Second != 0)
                    SelectedTimeTo = SelectedTimeTo.AddSeconds(-SelectedTimeTo.Second);
                if (SelectedTimeTo.Millisecond != 0)
                    SelectedTimeTo = SelectedTimeTo.AddMilliseconds(-SelectedTimeTo.Millisecond);
            }
            
            SelectedDateFromTB.Text = SelectedDateFrom.ToString("d");
            SelectedTimeFromTB.Text = SelectedTimeFrom.ToString("HH:mm:ss.fff");
            SelectedDateToTB.Text = SelectedDateTo.ToString("d");
            SelectedTimeToTB.Text = SelectedTimeTo.ToString("HH:mm:ss.fff");
        }

        private void SelectedDateFromTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDateFromTB.Text))
                return;
            if (DateTime.TryParse(SelectedDateFromTB.Text, out DateTime date))
            {
                SelectedDateFrom = date;
            }
        }

        private void SelectedTimeFromTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedTimeFromTB.Text))
                return;
            if (DateTime.TryParse(SelectedTimeFromTB.Text, out DateTime date))
            {
                SelectedTimeFrom = date;
            }
        }

        private void SelectedDateToTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDateToTB.Text))
                return;
            if (DateTime.TryParse(SelectedDateToTB.Text, out DateTime date))
            {
                SelectedDateTo = date;
            }
        }

        private void SelectedTimeToTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedTimeToTB.Text))
                return;
            if (DateTime.TryParse(SelectedTimeToTB.Text, out DateTime date))
            {
                SelectedTimeTo = date;
            }
        }


        public void CalendarFromDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            CalendarFrom.SelectedDate = SelectedDateFrom;
        }

        public void CalendarFromDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (!Equals(eventArgs.Parameter, "1")) return;

            if (!CalendarFrom.SelectedDate.HasValue)
            {
                eventArgs.Cancel();
                return;
            }

            SelectedDateFrom = CalendarFrom.SelectedDate.Value;
            SelectedDateFromTB.Text = SelectedDateFrom.ToString("d");
        }

        public void CalendarToDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            CalendarTo.SelectedDate = SelectedDateTo;
        }

        public void CalendarToDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (!Equals(eventArgs.Parameter, "1")) return;

            if (!CalendarTo.SelectedDate.HasValue)
            {
                eventArgs.Cancel();
                return;
            }

            SelectedDateTo = CalendarTo.SelectedDate.Value;
            SelectedDateToTB.Text = SelectedDateTo.ToString("d");
        }


        public void ClockFromDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            ClockFrom.Time = SelectedTimeFrom;
        }

        public void ClockFromDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Equals(eventArgs.Parameter, "1"))
            {
                SelectedTimeFrom = ClockFrom.Time;
                SelectedTimeFromTB.Text = SelectedTimeFrom.ToString("HH:mm:ss.fff");
            }
        }

        public void ClockToDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            ClockTo.Time = SelectedTimeTo;
        }

        public void ClockToDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Equals(eventArgs.Parameter, "1"))
            {
                SelectedTimeTo = ClockTo.Time;
                SelectedTimeToTB.Text = SelectedTimeTo.ToString("HH:mm:ss.fff");
            }
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
