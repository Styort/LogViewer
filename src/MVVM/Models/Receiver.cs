using System;
using System.Windows.Media;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using LogViewer.Helpers;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Models
{
    [Serializable]
    [DataContract]
    public class Receiver : BaseViewModel, ICloneable
    {
        [NonSerialized]
        private SolidColorBrush color = new SolidColorBrush(Colors.White);
        [DataMember(Name = "ColorString")]
        private string colorString;
        private string name = "UDP Receiver";
        private int port = 7071;
        private bool isActive = true;
        private string encoding = "UTF-8";

        public Receiver()
        {
            ColorString = "#FFFFFFFF";
        }

        public string Name
        {
            get => name;
            set
            {
                name = value;
                OnPropertyChanged();
            }
        }

        public int Port
        {
            get => port;
            set
            {
                port = value;
                OnPropertyChanged();
            }
        }

        [XmlIgnore]
        public SolidColorBrush Color
        {
            get => color;
            set
            {
                color = value;
                colorString = color.ToARGB();
                OnPropertyChanged();
            }
        }

        public string ColorString
        {
            get => colorString;
            set
            {
                colorString = value;
                color = color.FromARGB(colorString);
                OnPropertyChanged();
            }
        }

        public bool IsActive
        {
            get => isActive;
            set
            {
                isActive = value;
                OnPropertyChanged();
            }
        }

        public string Encoding
        {
            get => encoding;
            set
            {
                encoding = value;
                OnPropertyChanged();
            }
        }

        [OnDeserialized]
        private void SetValuesOnDeserialized(StreamingContext context)
        {
            color = color.FromARGB(colorString);
        }

        public object Clone()
        {
            return new Receiver
            {
                Name = this.Name,
                ColorString = this.ColorString,
                IsActive = this.IsActive,
                Color = this.Color,
                Port = this.Port
            };
        }
    }
}
