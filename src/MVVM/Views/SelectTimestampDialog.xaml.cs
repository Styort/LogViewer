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
    /// Interaction logic for SelectTimestampDialog.xaml
    /// </summary>
    public partial class SelectTimestampDialog : Window
    {
        private DateTime selectedDate = DateTime.Now;
        private DateTime selectedTime = DateTime.Now;
        public DateTime PickedDateTime => selectedDate.Date + selectedTime.TimeOfDay;

        public SelectTimestampDialog(DateTime? currentLogDateTime)
        {
            InitializeComponent();

            if (currentLogDateTime.HasValue)
            {
                selectedDate = currentLogDateTime.Value;
                selectedTime = currentLogDateTime.Value;
            }
            else
            {
                // обнуляем секунды и мс. для удобства работы с датой
                if (selectedTime.Second != 0)
                    selectedTime = selectedTime.AddSeconds(-selectedTime.Second);
                if (selectedTime.Millisecond != 0)
                    selectedTime = selectedTime.AddMilliseconds(-selectedTime.Millisecond);
            }
            
            SelectedDateTB.Text = selectedDate.ToString("d");
            SelectedTimeTB.Text = selectedTime.ToString("HH:mm:ss.fff");
        }


        public void CalendarDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            Calendar.SelectedDate = selectedDate;
        }

        public void CalendarDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (!Equals(eventArgs.Parameter, "1")) return;

            if (!Calendar.SelectedDate.HasValue)
            {
                eventArgs.Cancel();
                return;
            }

            selectedDate = Calendar.SelectedDate.Value;
            SelectedDateTB.Text = selectedDate.ToString("d");
        }

        public void ClockDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            Clock.Time = selectedTime;
        }

        public void ClockDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Equals(eventArgs.Parameter, "1"))
            {
                selectedTime = Clock.Time;
                SelectedTimeTB.Text = selectedTime.ToString("HH:mm:ss.fff");
            }
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void SelectedTimeTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if(string.IsNullOrEmpty(SelectedTimeTB.Text))
                return;
            if (DateTime.TryParse(SelectedTimeTB.Text, out DateTime date))
            {
                selectedTime = date;
            }
        }

        private void SelectedDateTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDateTB.Text))
                return;
            if (DateTime.TryParse(SelectedDateTB.Text, out DateTime date))
            {
                selectedDate = date;
            }
        }
    }
}
