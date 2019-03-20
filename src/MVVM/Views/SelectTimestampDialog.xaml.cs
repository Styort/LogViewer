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
        private DateTime selectedDateTime = DateTime.Now;
        public DateTime PickedDateTime => selectedDateTime;

        public SelectTimestampDialog(DateTime? currentLogDateTime)
        {
            InitializeComponent();

            if (currentLogDateTime.HasValue)
                selectedDateTime = currentLogDateTime.Value;
            
            else
            {
                // обнуляем секунды и мс. для удобства работы с датой
                if (selectedDateTime.Second != 0)
                    selectedDateTime = selectedDateTime.AddSeconds(-selectedDateTime.Second);
                if (selectedDateTime.Millisecond != 0)
                    selectedDateTime = selectedDateTime.AddMilliseconds(-selectedDateTime.Millisecond);
            }
            
            SelectedDateTimeTB.Text = selectedDateTime.ToString("dd/MM/yyyy HH:mm:ss.fff");
        }
        
        public void CalendarDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            Calendar.SelectedDate = selectedDateTime;
        }

        public void CalendarDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (!Equals(eventArgs.Parameter, "1")) return;

            if (!Calendar.SelectedDate.HasValue)
            {
                eventArgs.Cancel();
                return;
            }

            selectedDateTime = Calendar.SelectedDate.Value;
            SelectedDateTimeTB.Text = selectedDateTime.ToString("dd/MM/yyyy HH:mm:ss.fff");
        }

        public void ClockDialogOpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            Clock.Time = selectedDateTime;
        }

        public void ClockDialogClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Equals(eventArgs.Parameter, "1"))
            {
                selectedDateTime = Clock.Time;
                SelectedDateTimeTB.Text = selectedDateTime.ToString("dd/MM/yyyy HH:mm:ss.fff");
            }
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void SelectedDateTB_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDateTimeTB.Text))
                return;
            if (DateTime.TryParse(SelectedDateTimeTB.Text, out DateTime date))
            {
                selectedDateTime = date;
            }
        }
    }
}
