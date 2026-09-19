using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace LogViewer.Localization
{
    /// <summary>
    /// Singleton that provides localized values for bindings.
    /// Default language is Russian.
    /// </summary>
    public class TranslationSource : INotifyPropertyChanged
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly ResourceManager resManager;
        private CultureInfo currentCulture;

        public List<CultureInfo> AvaiableCultures { get; set; }

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static TranslationSource Instance { get; } = new TranslationSource();

        /// <summary>
        /// Creates the translation source and loads available cultures. 
        /// </summary>
        public TranslationSource()
        {
            // Pass the class name of your resources as a parameter e.g. MyResources for MyResources.resx
            resManager = new ResourceManager(typeof(Locals));

            AvaiableCultures = GetAvaiableCultures();

            CurrentCulture = AvaiableCultures.First();
        }

        /// <summary>
        /// Gets or sets the current culture and switches the current thread culture.
        /// </summary>
        public CultureInfo CurrentCulture
        {
            get => this.currentCulture;
            set
            {
                if (this.currentCulture != value)
                {
                    this.currentCulture = value;
                    Thread.CurrentThread.CurrentCulture = value;
                    Thread.CurrentThread.CurrentUICulture = value;

                    RaisePropertyChanged(String.Empty);
                    RaiseLanguageChanged(value);
                }
            }
        }

        /// <summary>
        /// Returns the localized value for the given key in the current culture.
        /// </summary>
        /// <param name="key">Resource key.</param>
        /// <returns>Localized value.</returns>
        public Object this[string key]
        {
            get
            {
                if (String.IsNullOrWhiteSpace(key))
                    return key;

                var result = this.resManager.GetObject(key, this.CurrentCulture);
                return result ?? key;
            }
        }

        /// <summary>
        /// Returns the value for the key in the specified culture.
        /// </summary>
        /// <param name="key">Resource key.</param>
        /// <param name="culture">Culture to look up.</param>
        /// <returns>Value localized for that culture.</returns>
        public Object GetLocalizedValue(String key, String culture)
        {
            logger.Debug($"GetLocalizedValue key: {key}, culture: {culture}");
            if (String.IsNullOrWhiteSpace(key))
            {
                logger.Warn("Key is empty. Returning null");
                return null;
            }

            if (String.IsNullOrWhiteSpace(culture))
            {
                logger.Warn("Culture is empty. Returning value for current culture");
                return this[key];
            }

            var cultureInfo = CultureInfo.CreateSpecificCulture(culture);
            if (!AvaiableCultures.Any(x => x.Equals(cultureInfo)))
            {
                logger.Warn(
                    "Provided culture was not found in avaible cultures collection. Returning value for current culture");
                return this[key];
            }

            return this.resManager.GetObject(key, cultureInfo);
        }

        /// <summary>
        /// Returns a dictionary of flags for available cultures.
        /// </summary>
        /// <returns>Cultures and their flags.</returns>
        public Dictionary<CultureInfo, Bitmap> GetAvaiableCulturesFlags()
        {
            Dictionary<CultureInfo, Bitmap> result = new Dictionary<CultureInfo, Bitmap>();

            foreach (var culture in AvaiableCultures)
            {
                result.Add(culture, this.resManager.GetObject("flag", culture) as Bitmap);
            }

            return result;
        }

        /// <summary>
        /// Languages that have a translation in resources.
        /// </summary>
        /// <returns>Supported languages.</returns>
        private List<CultureInfo> GetAvaiableCultures()
        {
            List<CultureInfo> avaiableCultures = new List<CultureInfo>();

            CultureInfo[] allCultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
            foreach (CultureInfo culture in allCultures)
            {
                try
                {
                    if (culture.Equals(CultureInfo.InvariantCulture)) continue; // skip InvariantCulture

                    ResourceSet resourceSet = resManager.GetResourceSet(culture, true, false);
                    if (resourceSet != null) // found resources for this language
                    {
                        // clone so we can change the date/time pattern
                        CultureInfo cultureClone = (CultureInfo) culture.Clone();

                        string uiCultureDateTimePattern = resManager.GetObject("UICultureDateTimePattern", culture)
                            ?.ToString();
                        if (!string.IsNullOrEmpty(uiCultureDateTimePattern))
                        {
                            cultureClone.DateTimeFormat.FullDateTimePattern =
                                uiCultureDateTimePattern; // apply the date/time pattern
                        }

                        avaiableCultures.Add(cultureClone); // add to supported languages
                    }
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Warn, $"{nameof(GetAvaiableCultures)} Exception {ex} ");
                }
            }

            //Sort for display in priority order. Priority is defined in resources. 
            //Sorting by name would happen automatically (CultureInfo.GetCultures returns an alphabetically sorted list)
            avaiableCultures.Sort((x, y) => GetUICulturePriority(x).CompareTo(GetUICulturePriority(y)));

            return avaiableCultures;
        }

        /// <summary>
        /// UI language display priority.
        /// </summary>
        /// <param name="сultureInfo">Language culture.</param>
        /// <returns>Priority.</returns>
        private int GetUICulturePriority(CultureInfo сultureInfo)
        {
            int uiCulturePriority;

            var uiCulturePriorityString = resManager.GetObject("UICulturePriority", сultureInfo)?.ToString();

            return int.TryParse(uiCulturePriorityString, out uiCulturePriority) ? uiCulturePriority : int.MaxValue;
        }

        /// <summary>
        /// Raised when the UI language changes.
        /// </summary>
        public event EventHandler<LanguageEventArgs> LanguageChanged;

        private void RaiseLanguageChanged(CultureInfo cultureInfo)
        {
            EventHandler<LanguageEventArgs> handler = LanguageChanged;
            handler?.Invoke(this, new LanguageEventArgs(cultureInfo));
        }

        #region INotifyPropertyChanged

        /// <summary>
        /// Raised when a property changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises PropertyChanged.
        /// </summary>
        /// <param name="propertyName">Changed property.</param>
        protected virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raises PropertyChanged.
        /// </summary>
        /// <param name="propertyName">Changed property.</param>
        protected virtual void RaiseOtherPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged
    }
}