using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using LogViewer.Enums;
using LogViewer.MVVM.Models;

namespace LogViewer.Helpers
{
    public static class ExtensionMethods
    {
        public static string FirstCharToUpper(this string input)
        {
            switch (input)
            {
                case null: throw new ArgumentNullException(nameof(input));
                case "": throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
                default: return input.First().ToString().ToUpper() + input.Substring(1);
            }
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }

        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int N)
        {
            return source.Skip(Math.Max(0, source.Count() - N));
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        /// <summary>
        /// Convert the Brush to a ARGB - Color. 
        /// </summary>
        /// <param name="brush">your object</param>
        /// <returns>
        /// White = #ffffffff
        /// Green = #ff00ff00
        /// </returns>
        public static string ToARGB(this SolidColorBrush brush)
        {
            if (brush == null) throw new ArgumentNullException();
            var c = brush.Color;
            return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        /// <summary>
        /// set the current brush to a new color based on the #argb string
        /// </summary>
        /// <param name="brush">your object</param>
        /// <param name="argb">The #ARGB Color</param>
        /// <returns>the same object as you run the function</returns>
        public static SolidColorBrush FromARGB(this SolidColorBrush brush, string argb)
        {
            if (argb.Length != 9) throw new FormatException("we need #aarrggbb as color");

            byte a = Convert.ToByte(int.Parse(argb.Substring(1, 2), System.Globalization.NumberStyles.HexNumber));
            byte r = Convert.ToByte(int.Parse(argb.Substring(3, 2), System.Globalization.NumberStyles.HexNumber));
            byte g = Convert.ToByte(int.Parse(argb.Substring(5, 2), System.Globalization.NumberStyles.HexNumber));
            byte b = Convert.ToByte(int.Parse(argb.Substring(7, 2), System.Globalization.NumberStyles.HexNumber));
            var c = Color.FromArgb(a, r, g, b);
            brush.Color = c;
            return brush;
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer = null)
        {
            return new HashSet<T>(source.ToList(), comparer);
        }

        /// <summary>
        /// Содержит любое значение из передаваемого массива
        /// </summary>
        /// <param name="line">Строка, в которой ищутся значения массива</param>
        /// <param name="search"></param>
        /// <returns></returns>
        public static bool ContainsAnyOf(this String line, string[] search, bool ignoreCase = false)
        {
            return search.Any(x=> ignoreCase ? line.ToUpper().Contains(x.ToUpper()) : line.Contains(x));
        }

        public static DateTime Truncate(this DateTime dateTime, TimeSpan timeSpan)
        {
            if (timeSpan == TimeSpan.Zero) return dateTime; // Or could throw an ArgumentException
            if (dateTime == DateTime.MinValue || dateTime == DateTime.MaxValue) return dateTime; // do not modify "guard" values
            return dateTime.AddTicks(-(dateTime.Ticks % timeSpan.Ticks));
        }

        /// <summary>
        /// Осуществляет фильтр переданного списка сообщений логов по переданным параметрам
        /// </summary>
        /// <param name="messages">Список сообщений</param>
        /// <param name="text">Искомый текст</param>
        /// <param name="matchCase">Учитывать ли регистр</param>
        /// <param name="matchWholeWord">Учитывать только слово целиком</param>
        /// <param name="useRegularExp">Использовать регулярные выражения</param>
        /// <param name="level">Минимальный уровень лога</param>
        /// <returns></returns>
        public static IEnumerable<LogMessage> Filter(this IEnumerable<LogMessage> messages, string text, bool matchCase, bool matchWholeWord, bool useRegularExp, eLogLevel level = eLogLevel.Trace)
        {
            IEnumerable<LogMessage> searchResult = messages.ToList();

            if (!matchCase && !matchWholeWord)
                return searchResult.Where(x => level.HasFlag(x.Level) && (x.Message.ToUpper().Contains(text, StringComparison.OrdinalIgnoreCase) || useRegularExp && Regex.IsMatch(x.Message.ToUpper(), text, RegexOptions.IgnoreCase)));

            if (matchCase && !matchWholeWord)
                return searchResult.Where(x => level.HasFlag(x.Level) && (x.Message.Contains(text) || useRegularExp && Regex.IsMatch(x.Message, text)));

            if (matchCase && matchWholeWord)
                return searchResult.Where(x => level.HasFlag(x.Level) && (x.Message.Contains($" {text} ") ||
                                                                      x.Message.StartsWith($"{text} ") ||
                                                                      x.Message.EndsWith($" {text}") ||
                                                                      x.Message.StartsWith(text) && x.Message.EndsWith(text) ||
                                                                      useRegularExp && Regex.IsMatch(x.Message, text)));

            if (!matchCase && matchWholeWord)
                return searchResult.Where(x => level.HasFlag(x.Level) && (x.Message.Contains($" {text} ", StringComparison.OrdinalIgnoreCase) ||
                                                                      x.Message.StartsWith($"{text} ", StringComparison.OrdinalIgnoreCase) ||
                                                                      x.Message.EndsWith($" {text}", StringComparison.OrdinalIgnoreCase) ||
                                                                      x.Message.StartsWith(text, StringComparison.OrdinalIgnoreCase) &&
                                                                      x.Message.EndsWith(text, StringComparison.OrdinalIgnoreCase)) || 
                                                                      useRegularExp && Regex.IsMatch(x.Message.ToUpper(), text, RegexOptions.IgnoreCase));

            return searchResult;
        }

        public static string ToPascalCase(this string text)
        {
            var yourString = text.ToLower().Replace("_", " ");
            TextInfo info = CultureInfo.CurrentCulture.TextInfo;
            return info.ToTitleCase(yourString).Replace(" ", string.Empty);
        }

        //public static T DeepClone<T>(T obj)
        //{
        //    //// Don't serialize a null object, simply return the default for that object
        //    //if (object.ReferenceEquals(obj, null))
        //    //{
        //    //    return default(T);
        //    //}

        //    //// initialize inner objects individually
        //    //// for example in default constructor some list property initialized with some values,
        //    //// but in 'source' these items are cleaned -
        //    //// without ObjectCreationHandling.Replace default constructor values will be added to result
        //    //var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        //    //return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj), deserializeSettings);
        //}
    }
}
