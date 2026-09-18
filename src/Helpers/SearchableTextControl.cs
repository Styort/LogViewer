using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LogViewer.Core.Services;

namespace LogViewer.Helpers
{
    public class SearchableTextControl : Control
    {
        static SearchableTextControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SearchableTextControl),
                new FrameworkPropertyMetadata(typeof(SearchableTextControl)));
        }

        #region DependencyProperties

        /// <summary>
        /// Text sandbox which is used to get or set the value from a dependency property.
        /// </summary>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }


        // Real implementation about TextProperty which  registers a dependency property with 
        // the specified property name, property type, owner type, and property metadata. 
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SearchableTextControl),
                new UIPropertyMetadata(string.Empty,
                    UpdateControlCallBack));

        /// <summary>
        /// HighlightBackground sandbox which is used to get or set the value from a dependency property,
        /// if it gets a value,it should be forced to bind to a Brushes type.
        /// </summary>
        public Brush HighlightBackground
        {
            get => (Brush)GetValue(HighlightBackgroundProperty);
            set => SetValue(HighlightBackgroundProperty, value);
        }


        // Real implementation about HighlightBackgroundProperty which registers a dependency property 
        // with the specified property name, property type, owner type, and property metadata. 
        public static readonly DependencyProperty HighlightBackgroundProperty =
            DependencyProperty.Register("HighlightBackground", typeof(Brush), typeof(SearchableTextControl),
                new UIPropertyMetadata(Brushes.Yellow, UpdateControlCallBack));

        /// <summary>
        /// HighlightForeground sandbox which is used to get or set the value from a dependency property,
        /// if it gets a value,it should be forced to bind to a Brushes type.
        /// </summary>
        public Brush HighlightForeground
        {
            get => (Brush)GetValue(HighlightForegroundProperty);
            set => SetValue(HighlightForegroundProperty, value);
        }


        // Real implementation about HighlightForegroundProperty which registers a dependency property with
        // the specified property name, property type, owner type, and property metadata. 
        public static readonly DependencyProperty HighlightForegroundProperty =
            DependencyProperty.Register("HighlightForeground", typeof(Brush), typeof(SearchableTextControl),
                new UIPropertyMetadata(Brushes.Black, UpdateControlCallBack));

        /// <summary>
        /// IsMatchCase sandbox which is used to get or set the value from a dependency property,
        /// if it gets a value,it should be forced to bind to a bool type.
        /// </summary>
        public bool IsMatchCase
        {
            get => (bool)GetValue(IsMatchCaseProperty);
            set => SetValue(IsMatchCaseProperty, value);
        }

        // Real implementation about IsMatchCaseProperty which  registers a dependency property with
        // the specified property name, property type, owner type, and property metadata. 
        public static readonly DependencyProperty IsMatchCaseProperty =
            DependencyProperty.Register("IsMatchCase", typeof(bool), typeof(SearchableTextControl),
                new UIPropertyMetadata(true, UpdateControlCallBack));

        /// <summary>
        /// IsHighlight sandbox which is used to get or set the value from a dependency property,
        /// if it gets a value,it should be forced to bind to a bool type.
        /// </summary>
        public bool IsHighlight
        {
            get => (bool)GetValue(IsHighlightProperty);
            set => SetValue(IsHighlightProperty, value);
        }

        // Real implementation about IsHighlightProperty which  registers a dependency property with
        // the specified property name, property type, owner type, and property metadata. 
        public static readonly DependencyProperty IsHighlightProperty =
            DependencyProperty.Register("IsHighlight", typeof(bool), typeof(SearchableTextControl),
                new UIPropertyMetadata(false, UpdateControlCallBack));

        /// <summary>
        /// SearchText sandbox which is used to get or set the value from a dependency property,
        /// if it gets a value,it should be forced to bind to a string type.
        /// </summary>
        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        /// <summary>
        /// Real implementation about SearchTextProperty which registers a dependency property with
        /// the specified property name, property type, owner type, and property metadata. 
        /// </summary>
        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register("SearchText", typeof(string), typeof(SearchableTextControl),
                new UIPropertyMetadata(string.Empty, UpdateControlCallBack));

        /// <summary>
        /// Whether to treat SearchText as a regular expression. Invalid patterns must not throw in OnRender.
        /// </summary>
        public bool UseRegex
        {
            get => (bool)GetValue(UseRegexProperty);
            set => SetValue(UseRegexProperty, value);
        }

        public static readonly DependencyProperty UseRegexProperty =
            DependencyProperty.Register("UseRegex", typeof(bool), typeof(SearchableTextControl),
                new UIPropertyMetadata(false, UpdateControlCallBack));

        /// <summary>
        /// Whole-word highlighting. Ignored when <see cref="UseRegex"/> is true (same contract as Core search).
        /// </summary>
        public bool IsMatchWholeWord
        {
            get => (bool)GetValue(IsMatchWholeWordProperty);
            set => SetValue(IsMatchWholeWordProperty, value);
        }

        public static readonly DependencyProperty IsMatchWholeWordProperty =
            DependencyProperty.Register("IsMatchWholeWord", typeof(bool), typeof(SearchableTextControl),
                new UIPropertyMetadata(false, UpdateControlCallBack));

        /// <summary>
        /// Create a call back function which is used to invalidate the rendering of the element, 
        /// and force a complete new layout pass.
        /// One such advanced scenario is if you are creating a PropertyChangedCallback for a 
        /// dependency property that is not  on a Freezable or FrameworkElement derived class that 
        /// still influences the layout when it changes.
        /// </summary>
        private static void UpdateControlCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SearchableTextControl obj = d as SearchableTextControl;
            obj.InvalidateVisual();
        }
        #endregion

        /// <summary>
        /// override the OnRender method which is used to search for the keyword and highlight
        /// it when the operation gets the result.
        /// </summary>
        protected override void OnRender(DrawingContext drawingContext)
        {
            // Define a TextBlock to hold the search result.
            TextBlock displayTextBlock = this.Template.FindName("PART_TEXT", this) as TextBlock;

            displayTextBlock.TextWrapping = TextWrapping.NoWrap;
            displayTextBlock.TextTrimming = TextTrimming.CharacterEllipsis;

            if (string.IsNullOrEmpty(this.Text))
            {
                base.OnRender(drawingContext);

                return;
            }
            if (!this.IsHighlight)
            {
                displayTextBlock.Text = this.Text;
                base.OnRender(drawingContext);

                return;
            }

            displayTextBlock.Inlines.Clear();

            var matcher = SearchMatcher.Create(this.SearchText, this.IsMatchCase, this.UseRegex, this.IsMatchWholeWord);
            if (matcher.IsPatternInvalid || matcher.IsEmpty)
            {
                displayTextBlock.Text = this.Text;
                base.OnRender(drawingContext);
                return;
            }

            var matches = matcher.FindMatches(this.Text);
            if (matches == null || matches.Count == 0)
            {
                displayTextBlock.Text = this.Text;
                base.OnRender(drawingContext);
                return;
            }

            int cursor = 0;
            string displayText = this.Text;
            foreach (var match in matches)
            {
                if (match.Index < cursor || match.Length <= 0 || match.Index + match.Length > displayText.Length)
                    continue;

                Run prefix = GenerateRun(displayText.Substring(cursor, match.Index - cursor), false);
                if (prefix != null)
                    displayTextBlock.Inlines.Add(prefix);

                Run hit = GenerateRun(displayText.Substring(match.Index, match.Length), true);
                if (hit != null)
                    displayTextBlock.Inlines.Add(hit);

                cursor = match.Index + match.Length;
            }

            Run tail = GenerateRun(displayText.Substring(cursor), false);
            if (tail != null)
                displayTextBlock.Inlines.Add(tail);

            base.OnRender(drawingContext);
        }

        /// <summary>
        /// Set inline-level flow content element intended to contain a run of formatted or unformatted 
        /// text into your background and foreground setting.
        /// </summary>
        private Run GenerateRun(string searchedString, bool isHighlight)
        {
            if (!string.IsNullOrEmpty(searchedString))
            {
                Run run = new Run(searchedString)
                {
                    Background = isHighlight ? this.HighlightBackground : this.Background,
                    Foreground = isHighlight ? this.HighlightForeground : this.Foreground,

                    // Set the source text with the style which is Bold.
                    FontWeight = isHighlight ? FontWeights.Bold : FontWeights.Normal,
                };
                return run;
            }
            return null;
        }
    }
}
