using System;
using System.Collections;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LogViewer.MVVM.Models;

namespace LogViewer.Helpers
{
    public class ErrorTimelineControl : FrameworkElement
    {
        private static readonly Brush EmptyFill = CreateFrozenBrush(240, 240, 240);
        private static readonly Brush WarnFill = CreateFrozenBrush(Colors.Orange);
        private static readonly Brush ErrorFill = CreateFrozenBrush(Colors.Red);
        private static readonly Brush FatalFill = CreateFrozenBrush(Colors.DarkRed);
        private static readonly Pen BucketPen = CreateFrozenPen();

        public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
            nameof(Items), typeof(IEnumerable), typeof(ErrorTimelineControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty NavigateCommandProperty = DependencyProperty.Register(
            nameof(NavigateCommand), typeof(ICommand), typeof(ErrorTimelineControl));

        public static readonly DependencyProperty BucketCountChangedCommandProperty = DependencyProperty.Register(
            nameof(BucketCountChangedCommand), typeof(ICommand), typeof(ErrorTimelineControl));

        private int lastReportedBucketCount;
        private object defaultToolTip;

        public ErrorTimelineControl()
        {
            Cursor = Cursors.Hand;
            SnapsToDevicePixels = true;
            Height = 32;
        }

        public IEnumerable Items
        {
            get => (IEnumerable)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public ICommand NavigateCommand
        {
            get => (ICommand)GetValue(NavigateCommandProperty);
            set => SetValue(NavigateCommandProperty, value);
        }

        public ICommand BucketCountChangedCommand
        {
            get => (ICommand)GetValue(BucketCountChangedCommandProperty);
            set => SetValue(BucketCountChangedCommandProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
            double height = double.IsInfinity(availableSize.Height) ? 32 : Math.Max(28, availableSize.Height);
            if (height > 36)
                height = 32;
            return new Size(width, height);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            ReportBucketCount();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var buckets = GetBuckets();
            var width = ActualWidth;
            var height = ActualHeight;
            if (width <= 0 || height <= 0)
                return;

            drawingContext.DrawRectangle(EmptyFill, null, new Rect(0, 0, width, height));
            if (buckets.Count == 0)
                return;

            double bucketWidth = width / buckets.Count;
            for (int i = 0; i < buckets.Count; i++)
            {
                var bucket = buckets[i];
                double x = i * bucketWidth;
                var rect = new Rect(x, 0, Math.Max(1, bucketWidth - 1), height);
                drawingContext.DrawRectangle(EmptyFill, BucketPen, rect);

                double y = height;
                y = DrawStack(drawingContext, x, bucketWidth - 1, y, bucket.WarnHeight, WarnFill);
                y = DrawStack(drawingContext, x, bucketWidth - 1, y, bucket.ErrorHeight, ErrorFill);
                DrawStack(drawingContext, x, bucketWidth - 1, y, bucket.FatalHeight, FatalFill);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (defaultToolTip == null)
                defaultToolTip = ToolTip;
            var bucket = HitTestBucket(e.GetPosition(this).X);
            ToolTip = !string.IsNullOrEmpty(bucket?.ToolTip) ? bucket.ToolTip : defaultToolTip;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            var bucket = HitTestBucket(e.GetPosition(this).X);
            if (bucket == null || !bucket.HasMessages)
                return;

            var command = NavigateCommand;
            if (command != null && command.CanExecute(bucket))
                command.Execute(bucket);
        }

        private void ReportBucketCount()
        {
            if (ActualWidth <= 0)
                return;

            int count = Math.Max(40, (int)(ActualWidth / 4));
            if (count > 200)
                count = 200;
            if (count == lastReportedBucketCount)
                return;

            lastReportedBucketCount = count;
            var command = BucketCountChangedCommand;
            if (command != null && command.CanExecute(count))
                command.Execute(count);
        }

        private ErrorTimelineBucket HitTestBucket(double x)
        {
            var buckets = GetBuckets();
            if (buckets.Count == 0 || ActualWidth <= 0)
                return null;

            int index = (int)(x / ActualWidth * buckets.Count);
            if (index < 0)
                index = 0;
            if (index >= buckets.Count)
                index = buckets.Count - 1;
            return buckets[index];
        }

        private System.Collections.Generic.IList<ErrorTimelineBucket> GetBuckets()
        {
            if (Items is System.Collections.Generic.IList<ErrorTimelineBucket> list)
                return list;

            var result = new System.Collections.Generic.List<ErrorTimelineBucket>();
            if (Items == null)
                return result;

            foreach (var item in Items)
            {
                if (item is ErrorTimelineBucket bucket)
                    result.Add(bucket);
            }

            return result;
        }

        private static double DrawStack(DrawingContext dc, double x, double width, double bottom, double barHeight, Brush brush)
        {
            if (barHeight <= 0 || width <= 0)
                return bottom;
            double top = Math.Max(0, bottom - barHeight);
            dc.DrawRectangle(brush, null, new Rect(x, top, width, bottom - top));
            return top;
        }

        private static Brush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Pen CreateFrozenPen()
        {
            var pen = new Pen(CreateFrozenBrush(228, 228, 228), 0.5);
            pen.Freeze();
            return pen;
        }
    }
}
