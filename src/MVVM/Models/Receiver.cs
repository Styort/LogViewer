using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using LogViewer.Core.Domain;
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
        private ReceiverTransport transport = ReceiverTransport.Udp;

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

        /// <summary>
        /// UDP or TCP. Bound in settings UI. XML uses <see cref="TransportName"/>.
        /// </summary>
        [XmlIgnore]
        public ReceiverTransport Transport
        {
            get => transport;
            set
            {
                if (transport == value)
                    return;
                bool rename = IsStockName(name);
                transport = value;
                if (rename)
                    name = StockName(transport);
                OnPropertyChanged();
                if (rename)
                    OnPropertyChanged(nameof(Name));
            }
        }

        /// <summary>
        /// Serialized as &lt;Transport&gt;. Empty or unknown values are UDP so old settings.xml keep working.
        /// </summary>
        [XmlElement("Transport")]
        public string TransportName
        {
            get => transport.ToString();
            set => transport = ReceiverTransportHelper.ParseOrUdp(value);
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
                Port = this.Port,
                Encoding = this.Encoding,
                Transport = this.Transport
            };
        }

        /// <summary>
        /// UDP and TCP may share a numeric port; identity is port + transport.
        /// </summary>
        public static Receiver Find(IEnumerable<Receiver> receivers, int port, ReceiverTransport transport)
        {
            return receivers?.FirstOrDefault(x => x.Port == port && x.Transport == transport);
        }

        private static bool IsStockName(string value)
        {
            return string.IsNullOrEmpty(value)
                   || value == "UDP Receiver"
                   || value == "TCP Receiver";
        }

        private static string StockName(ReceiverTransport value)
        {
            return value == ReceiverTransport.Tcp ? "TCP Receiver" : "UDP Receiver";
        }
    }
}
