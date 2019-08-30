using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Models
{
    public class ImportLogFile : BaseViewModel
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }

        private double process = 0;

        public double Process
        {
            get => process;
            set
            {
                process = value;
                OnPropertyChanged();
            }
        }
    }
}
