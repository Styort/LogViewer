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
        public DateTime SelectedDate { get; set; } = DateTime.Now;
        public DateTime SelectedTime { get; set; } = DateTime.Now;

        public SelectTimestampDialog(DateTime? currentLogDateTime)
        {
            InitializeComponent();

            if (currentLogDateTime.HasValue)
            {
                SelectedDate = currentLogDateTime.Value;
                SelectedTime = currentLogDateTime.Value;
            }
            else
            {
                // обнуляем секунды и мс. для удобства работы с датой
                if (SelectedTime.Second != 0)
                    SelectedTime = SelectedTime.AddSeconds(-SelectedTime.Second);
                if (SelectedTime.Millisecond != 0)
                    SelectedTime = SelectedTime.AddMilliseconds(-SelectedTime.Millisecond);
            }
            
            SelectedDateTB.Text = SelectedDate.ToString("d");
            SelectedTimeTB.Text = SelectedTime.ToString("HH:mm:ss.fff");
        }


        public void CalendarDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            Calendar.SelectedDate = SelectedDate;
        }

        public void CalendarDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (!Equals(eventArgs.Parameter, "1")) return;

            if (!Calendar.SelectedDate.HasValue)
            {
                eventArgs.Cancel();
                return;
            }

            SelectedDate = Calendar.SelectedDate.Value;
            SelectedDateTB.Text = SelectedDate.ToString("d");
        }

        public void ClockDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            Clock.Time = SelectedTime;
        }

        public void ClockDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Equals(eventArgs.Parameter, "1"))
            {
                SelectedTime = Clock.Time;
                SelectedTimeTB.Text = SelectedTime.ToString("HH:mm:ss.fff");
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
                SelectedTime = date;
            }
        }

        private void SelectedDateTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDateTB.Text))
                return;
            if (DateTime.TryParse(SelectedDateTB.Text, out DateTime date))
            {
                SelectedDate = date;
            }
        }
    }
}
