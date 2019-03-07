using System;
using System.Runtime.Serialization;
using System.Windows.Media;
using System.Xml.Serialization;
using LogViewer.Helpers;

namespace LogViewer.MVVM.Models
{
    public class Theme
    {
        [NonSerialized]
        private SolidColorBrush themeColor = (SolidColorBrush)new BrushConverter().ConvertFrom("#3F51B5");

        [DataMember(Name = "ThemeColorString")]
        private string themeColorString;

        public string Name { get; set; }

        [XmlIgnore]
        public SolidColorBrush Color
        {
            get => themeColor;
            set
            {
                themeColor = value;
                themeColorString = themeColor.ToARGB();
            }
        }

        [XmlIgnore]
        public Uri ThemeUri => new Uri($"pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Primary/MaterialDesignColor.{Name}.xaml");

        public string ThemeColorString
        {
            get => themeColorString;
            set
            {
                themeColorString = value;
                themeColor = themeColor.FromARGB(themeColorString);
            }
        }

        [OnDeserialized]
        private void SetValuesOnDeserialized(StreamingContext context)
        {
            themeColor = themeColor.FromARGB(themeColorString);
        }
    }
}
