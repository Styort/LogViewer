using System;
using System.Windows.Media;
using System.Xml.Serialization;
using LogViewer.Core.Domain;
using LogViewer.Helpers;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Models
{
    /// <summary>
    /// Highlight rule as stored in <c>settings.xml</c> and edited in the settings tab.
    /// List order is match priority. Does not hide rows.
    /// </summary>
    [Serializable]
    public class HighlightRuleItem : BaseViewModel
    {
        [NonSerialized]
        private SolidColorBrush color = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x4D, 255, 0, 0));
        private string colorArgb = "#4DFF0000";
        private int opacityPercent = 30;
        private string name;
        private bool enabled = true;
        private string level = string.Empty;
        private string loggerPattern = string.Empty;
        private bool loggerIsRegex;
        private string messagePattern = string.Empty;
        private bool messageIsRegex;

        public HighlightRuleItem()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public string Id { get; set; }

        public string Name
        {
            get => name;
            set
            {
                name = value;
                OnPropertyChanged();
            }
        }

        public bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Exact level name or empty for any level.</summary>
        public string Level
        {
            get => level ?? string.Empty;
            set
            {
                level = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string LoggerPattern
        {
            get => loggerPattern;
            set
            {
                loggerPattern = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public bool LoggerIsRegex
        {
            get => loggerIsRegex;
            set
            {
                loggerIsRegex = value;
                OnPropertyChanged();
            }
        }

        public string MessagePattern
        {
            get => messagePattern;
            set
            {
                messagePattern = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public bool MessageIsRegex
        {
            get => messageIsRegex;
            set
            {
                messageIsRegex = value;
                OnPropertyChanged();
            }
        }

        [XmlIgnore]
        public SolidColorBrush Color
        {
            get => color;
            set
            {
                if (value == null)
                    return;
                var c = value.Color;
                // The color picker writes opaque A=255; keep the opacity slider so a bright pick stays readable.
                byte a = c.A == 255 ? PercentToAlpha(opacityPercent) : c.A;
                if (c.A != 255)
                    opacityPercent = AlphaToPercent(c.A);
                if (SameColor(a, c.R, c.G, c.B))
                    return;
                ApplyColor(a, c.R, c.G, c.B);
            }
        }

        public string ColorArgb
        {
            get => colorArgb;
            set
            {
                try
                {
                    var parsed = new SolidColorBrush().FromARGB(value);
                    var c = parsed.Color;
                    opacityPercent = AlphaToPercent(c.A == 0 ? (byte)0x4D : c.A);
                    color = parsed;
                    colorArgb = value;
                }
                catch (Exception)
                {
                    color = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x4D, 255, 0, 0));
                    colorArgb = "#4DFF0000";
                    opacityPercent = 30;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(Color));
                OnPropertyChanged(nameof(OpacityPercent));
            }
        }

        /// <summary>Fill opacity 5–100%. Stored as the A channel of <see cref="ColorArgb"/>.</summary>
        [XmlIgnore]
        public int OpacityPercent
        {
            get => opacityPercent;
            set
            {
                int pct = value;
                if (pct < 5)
                    pct = 5;
                if (pct > 100)
                    pct = 100;
                if (pct == opacityPercent)
                    return;
                opacityPercent = pct;
                var c = color.Color;
                ApplyColor(PercentToAlpha(pct), c.R, c.G, c.B);
                OnPropertyChanged();
            }
        }

        private void ApplyColor(byte a, byte r, byte g, byte b)
        {
            color = new SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
            colorArgb = color.ToARGB();
            OnPropertyChanged(nameof(Color));
            OnPropertyChanged(nameof(ColorArgb));
        }

        private bool SameColor(byte a, byte r, byte g, byte b)
        {
            if (color == null)
                return false;
            var c = color.Color;
            return c.A == a && c.R == r && c.G == g && c.B == b;
        }

        private static byte PercentToAlpha(int percent)
        {
            return (byte)Math.Round(percent * 255.0 / 100.0);
        }

        private static int AlphaToPercent(byte alpha)
        {
            int pct = (int)Math.Round(alpha * 100.0 / 255.0);
            if (pct < 5)
                return 5;
            if (pct > 100)
                return 100;
            return pct;
        }

        public bool TryValidateRegex(out string field)
        {
            field = null;
            if (LoggerIsRegex && !string.IsNullOrEmpty(LoggerPattern) && !IsValidRegex(LoggerPattern))
            {
                field = nameof(LoggerPattern);
                return false;
            }
            if (MessageIsRegex && !string.IsNullOrEmpty(MessagePattern) && !IsValidRegex(MessagePattern))
            {
                field = nameof(MessagePattern);
                return false;
            }
            return true;
        }

        private static bool IsValidRegex(string pattern)
        {
            try
            {
                var unused = new System.Text.RegularExpressions.Regex(
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(250));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public HighlightRule ToCore()
        {
            return new HighlightRule
            {
                Id = Id,
                Name = Name,
                Enabled = Enabled,
                Level = Level,
                LoggerPattern = LoggerPattern,
                LoggerIsRegex = LoggerIsRegex,
                MessagePattern = MessagePattern,
                MessageIsRegex = MessageIsRegex,
                ColorArgb = ColorArgb
            };
        }
    }
}
