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
        private DateTime selectedDateFrom = DateTime.Now;
        private DateTime selectedTimeFrom = DateTime.Now;
        private DateTime selectedDateTo = DateTime.Now;
        private DateTime selectedTimeTo = DateTime.Now;
        public DateTime DateTimeFrom => selectedDateFrom.Date + selectedTimeFrom.TimeOfDay;
        public DateTime DateTimeTo => selectedDateTo.Date + selectedTimeTo.TimeOfDay;

        public SelectTimeIntervalDialog(DateTime? currentLogDateTime)
        {
            InitializeComponent();

            if (currentLogDateTime.HasValue)
            {
                selectedDateFrom = currentLogDateTime.Value;
                selectedTimeFrom = currentLogDateTime.Value;
                selectedDateTo = currentLogDateTime.Value;
                selectedTimeTo = currentLogDateTime.Value;
            }
            else
            {
                // обнуляем секунды и мс. для удобства работы с датой
                if (selectedTimeFrom.Second != 0)
                    selectedTimeFrom = selectedTimeFrom.AddSeconds(-selectedTimeFrom.Second);
                if (selectedTimeFrom.Millisecond != 0)
                    selectedTimeFrom = selectedTimeFrom.AddMilliseconds(-selectedTimeFrom.Millisecond);
                if (selectedTimeTo.Second != 0)
                    selectedTimeTo = selectedTimeTo.AddSeconds(-selectedTimeTo.Second);
                if (selectedTimeTo.Millisecond != 0)
                    selectedTimeTo = selectedTimeTo.AddMilliseconds(-selectedTimeTo.Millisecond);
            }
            
            SelectedDateFromTB.Text = selectedDateFrom.ToString("d");
            SelectedTimeFromTB.Text = selectedTimeFrom.ToString("HH:mm:ss.fff");
            SelectedDateToTB.Text = selectedDateTo.ToString("d");
            SelectedTimeToTB.Text = selectedTimeTo.ToString("HH:mm:ss.fff");
        }

        private void SelectedDateFromTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDateFromTB.Text))
                return;
            if (DateTime.TryParse(SelectedDateFromTB.Text, out DateTime date))
            {
                selectedDateFrom = date;
            }
        }

        private void SelectedTimeFromTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedTimeFromTB.Text))
                return;
            if (DateTime.TryParse(SelectedTimeFromTB.Text, out DateTime date))
            {
                selectedTimeFrom = date;
            }
        }

        private void SelectedDateToTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDateToTB.Text))
                return;
            if (DateTime.TryParse(SelectedDateToTB.Text, out DateTime date))
            {
                selectedDateTo = date;
            }
        }

        private void SelectedTimeToTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedTimeToTB.Text))
                return;
            if (DateTime.TryParse(SelectedTimeToTB.Text, out DateTime date))
            {
                selectedTimeTo = date;
            }
        }


        public void CalendarFromDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            CalendarFrom.SelectedDate = selectedDateFrom;
        }

        public void CalendarFromDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (!Equals(eventArgs.Parameter, "1")) return;

            if (!CalendarFrom.SelectedDate.HasValue)
            {
                eventArgs.Cancel();
                return;
            }

            selectedDateFrom = CalendarFrom.SelectedDate.Value;
            SelectedDateFromTB.Text = selectedDateFrom.ToString("d");
        }

        public void CalendarToDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            CalendarTo.SelectedDate = selectedDateTo;
        }

        public void CalendarToDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (!Equals(eventArgs.Parameter, "1")) return;

            if (!CalendarTo.SelectedDate.HasValue)
            {
                eventArgs.Cancel();
                return;
            }

            selectedDateTo = CalendarTo.SelectedDate.Value;
            SelectedDateToTB.Text = selectedDateTo.ToString("d");
        }


        public void ClockFromDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            ClockFrom.Time = selectedTimeFrom;
        }

        public void ClockFromDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Equals(eventArgs.Parameter, "1"))
            {
                selectedTimeFrom = ClockFrom.Time;
                SelectedTimeFromTB.Text = selectedTimeFrom.ToString("HH:mm:ss.fff");
            }
        }

        public void ClockToDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            ClockTo.Time = selectedTimeTo;
        }

        public void ClockToDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Equals(eventArgs.Parameter, "1"))
            {
                selectedTimeTo = ClockTo.Time;
                SelectedTimeToTB.Text = selectedTimeTo.ToString("HH:mm:ss.fff");
            }
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
