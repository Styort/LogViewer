using System;
using System.Collections.Generic;
using System.Linq;
using LogViewer.Enums;

namespace LogViewer.MVVM.Models
{
    public class LogTemplateItem
    {
        public eImportTemplateParameters SelectedTemplateParameter { get; set; }
        public IEnumerable<eImportTemplateParameters> TemplateParameters => Enum.GetValues(typeof(eImportTemplateParameters)).Cast<eImportTemplateParameters>();
    }
}
