using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LogViewer.MVVM.Models;

namespace LogViewer.MVVM.ViewModels
{
    public class ImportLogsProcessViewModel : BaseViewModel
    {
        public List<ImportLogFile> ImportFiles { get; set; } = new List<ImportLogFile>();
    }
}
