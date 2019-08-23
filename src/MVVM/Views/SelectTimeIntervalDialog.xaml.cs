using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using LogViewer.MVVM.Models;
using MaterialDesignThemes.Wpf;

namespace LogViewer.MVVM.Views
{
    /// <summary>
    /// Interaction logic for SelectTimeIntervalDialog.xaml
    /// </summary>
    public partial class SelectTimeIntervalDialog : Window
    {
        private DateTime selectedDateTimeFrom = DateTime.Now;
        private DateTime selectedDateTimeTo = DateTime.Now;
        public DateTime DateTimeFrom => selectedDateTimeFrom;
        public DateTime DateTimeTo => selectedDateTimeTo;

        string[] dateFormats = { "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss.f", "dd/MM/yyyy HH:mm:ss.ff", "dd/MM/yyyy HH:mm:ss.fff",
            "dd/MM/yyyy HH:mm:s", "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:m", "dd/MM/yyyy HH"};

        public SelectTimeIntervalDialog(DateTime? currentLogDateTime)
        {
            InitializeComponent();

            if (currentLogDateTime.HasValue)
            {
                selectedDateTimeFrom = currentLogDateTime.Value;
                selectedDateTimeTo = currentLogDateTime.Value;
            }
            else
            {
                // обнуляем секунды и мс. для удобства работы с датой
                if (selectedDateTimeFrom.Second != 0)
                    selectedDateTimeFrom = selectedDateTimeFrom.AddSeconds(-selectedDateTimeFrom.Second);
                if (selectedDateTimeFrom.Millisecond != 0)
                    selectedDateTimeFrom = selectedDateTimeFrom.AddMilliseconds(-selectedDateTimeFrom.Millisecond);
                if (selectedDateTimeTo.Second != 0)
                    selectedDateTimeTo = selectedDateTimeTo.AddSeconds(-selectedDateTimeTo.Second);
                if (selectedDateTimeTo.Millisecond != 0)
                    selectedDateTimeTo = selectedDateTimeTo.AddMilliseconds(-selectedDateTimeTo.Millisecond);
            }

            SelectedDateTimeFromTB.Text = selectedDateTimeFrom.ToString("dd/MM/yyyy HH:mm:ss.fff");
            SelectedDateTimeToTB.Text = selectedDateTimeTo.ToString("dd/MM/yyyy HH:mm:ss.fff");
        }

        private void SelectedDateFromTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDateTimeFromTB.Text))
                return;

            if (DateTime.TryParseExact(SelectedDateTimeFromTB.Text,
                dateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date))
            {
                selectedDateTimeFrom = date;
            }
        }

        private void SelectedDateToTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDateTimeToTB.Text))
                return;

            if (DateTime.TryParseExact(SelectedDateTimeToTB.Text,
                dateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date))
            {
                selectedDateTimeTo = date;
            }
        }


        public void CalendarFromDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            CalendarFrom.SelectedDate = selectedDateTimeFrom;
        }

        public void CalendarFromDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (!Equals(eventArgs.Parameter, "1")) return;

            if (!CalendarFrom.SelectedDate.HasValue)
            {
                eventArgs.Cancel();
                return;
            }

            selectedDateTimeFrom = CalendarFrom.SelectedDate.Value;
            SelectedDateTimeFromTB.Text = selectedDateTimeFrom.ToString("dd/MM/yyyy HH:mm:ss.fff");
        }

        public void CalendarToDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            CalendarTo.SelectedDate = selectedDateTimeTo;
        }

        public void CalendarToDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (!Equals(eventArgs.Parameter, "1")) return;

            if (!CalendarTo.SelectedDate.HasValue)
            {
                eventArgs.Cancel();
                return;
            }

            selectedDateTimeTo = CalendarTo.SelectedDate.Value;
            SelectedDateTimeToTB.Text = selectedDateTimeTo.ToString("dd/MM/yyyy HH:mm:ss.fff");
        }


        public void ClockFromDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            ClockFrom.Time = selectedDateTimeFrom;
        }

        public void ClockFromDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Equals(eventArgs.Parameter, "1"))
            {
                selectedDateTimeFrom = ClockFrom.Time;
                SelectedDateTimeFromTB.Text = selectedDateTimeFrom.ToString("dd/MM/yyyy HH:mm:ss.fff");
            }
        }

        public void ClockToDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            ClockTo.Time = selectedDateTimeTo;
        }

        public void ClockToDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Equals(eventArgs.Parameter, "1"))
            {
                selectedDateTimeTo = ClockTo.Time;
                SelectedDateTimeToTB.Text = selectedDateTimeTo.ToString("dd/MM/yyyy HH:mm:ss.fff");
            }
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
