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
    /// Синглтон, который предоставляет источник данных для биндинга локализованных данных
    /// Язык по умолчанию - русский
    /// </summary>
    public class TranslationSource : INotifyPropertyChanged
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly ResourceManager resManager;
        private CultureInfo currentCulture;

        public List<CultureInfo> AvaiableCultures { get; set; }

        /// <summary>
        /// Экземпляр синглтона
        /// </summary>
        public static TranslationSource Instance { get; } = new TranslationSource();

        /// <summary>
        /// Возвращает экземпляр источника данных с путём подключения 
        /// </summary>
        public TranslationSource()
        {
            // Pass the class name of your resources as a parameter e.g. MyResources for MyResources.resx
            resManager = new ResourceManager(typeof(Locals));

            AvaiableCultures = GetAvaiableCultures();

            CurrentCulture = AvaiableCultures.First();
        }

        /// <summary>
        /// Возвращает и устанавливает текущую культуру с одновременным переключением культуры текущего потока
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
        /// Возвращает локализованное значение в текущей культуре для переданного тега
        /// </summary>
        /// <param name="key">Тег для поиска значения</param>
        /// <returns>Локализованное значение</returns>
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
        /// Возвращает локализованное в указанной культуре значение, соответствующее указанному ключу
        /// </summary>
        /// <param name="key">Ключ для поиска значений</param>
        /// <param name="culture">Культура, в которой искать</param>
        /// <returns>Локализованное для данной культуры значение</returns>
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
        /// Возвращает словарь флагов доступных культур
        /// </summary>
        /// <returns>Словарь культур и соответствующих флагов</returns>
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
        /// Получить доступные языки, для которых поддерживается перевод в ресурсах
        /// </summary>
        /// <returns>Поддерживаемые языки</returns>
        private List<CultureInfo> GetAvaiableCultures()
        {
            List<CultureInfo> avaiableCultures = new List<CultureInfo>();

            CultureInfo[] allCultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
            foreach (CultureInfo culture in allCultures)
            {
                try
                {
                    if (culture.Equals(CultureInfo.InvariantCulture)) continue; //Пропускаем InvariantCulture

                    ResourceSet resourceSet = resManager.GetResourceSet(culture, true, false);
                    if (resourceSet != null) // Нашли ресурсы для языка
                    {
                        //Делаем копию, чтобы поменять паттерн для вывода времени
                        CultureInfo cultureClone = (CultureInfo) culture.Clone();

                        string uiCultureDateTimePattern = resManager.GetObject("UICultureDateTimePattern", culture)
                            ?.ToString();
                        if (!string.IsNullOrEmpty(uiCultureDateTimePattern))
                        {
                            cultureClone.DateTimeFormat.FullDateTimePattern =
                                uiCultureDateTimePattern; //Применяем паттерн времени
                        }

                        avaiableCultures.Add(cultureClone); // Добавляем в поддерживаемые языки
                    }
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Warn, $"{nameof(GetAvaiableCultures)} Exception {ex} ");
                }
            }

            //Сортировка для отображения в порядке приоритета. Задается в ресурсах. 
            //Сортировка по имени будет автоматически (CultureInfo.GetCultures(CultureTypes.AllCultures) - вывод список отсортированный по алфавиту)
            avaiableCultures.Sort((x, y) => GetUICulturePriority(x).CompareTo(GetUICulturePriority(y)));

            return avaiableCultures;
        }

        /// <summary>
        /// Получить приоритет языка на интерфейсе пользователя
        /// </summary>
        /// <param name="сultureInfo">Культура языка</param>
        /// <returns>Приоритет</returns>
        private int GetUICulturePriority(CultureInfo сultureInfo)
        {
            int uiCulturePriority;

            var uiCulturePriorityString = resManager.GetObject("UICulturePriority", сultureInfo)?.ToString();

            return int.TryParse(uiCulturePriorityString, out uiCulturePriority) ? uiCulturePriority : int.MaxValue;
        }

        /// <summary>
        /// Событие смены языка
        /// </summary>
        public event EventHandler<LanguageEventArgs> LanguageChanged;

        private void RaiseLanguageChanged(CultureInfo cultureInfo)
        {
            EventHandler<LanguageEventArgs> handler = LanguageChanged;
            handler?.Invoke(this, new LanguageEventArgs(cultureInfo));
        }

        #region Реализация INotifyPropertyChanged

        /// <summary>
        /// Возникает, когда изменяется какой-нибудь свойство.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Возникновение события PropertyChanged.
        /// </summary>
        /// <param name="propertyName">Изменяемое свойство.</param>
        protected virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Возникновение события PropertyChanged.
        /// </summary>
        /// <param name="propertyName">Изменяемое свойство.</param>
        protected virtual void RaiseOtherPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Реализация INotifyPropertyChanged
    }
}