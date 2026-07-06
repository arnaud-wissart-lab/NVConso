using NVConso.ViewModels;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace NVConso.Controls
{
    public sealed class TelemetrySparkline : FrameworkElement
    {
        public static readonly DependencyProperty ChartProperty = DependencyProperty.Register(
            nameof(Chart),
            typeof(ChartViewModel),
            typeof(TelemetrySparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnChartChanged));

        public ChartViewModel Chart
        {
            get => (ChartViewModel)GetValue(ChartProperty);
            set => SetValue(ChartProperty, value);
        }

        protected override WpfSize MeasureOverride(WpfSize availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
            double height = double.IsInfinity(availableSize.Height) ? 160 : availableSize.Height;
            return new WpfSize(Math.Max(240, width), Math.Max(140, height));
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            Rect bounds = new(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width <= 1 || bounds.Height <= 1)
                return;

            DrawGrid(drawingContext, bounds);
            if (Chart?.HasData != true)
            {
                DrawCenteredText(drawingContext, bounds, Chart?.EmptyMessage ?? "Aucune donnée.");
                return;
            }

            double maximum = ResolveMaximum();
            foreach (ChartSeriesViewModel series in Chart.Series)
                DrawSeries(drawingContext, bounds, series, maximum);
        }

        private static void OnChartChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is not TelemetrySparkline chart)
                return;

            if (args.OldValue is ChartViewModel oldChart)
                oldChart.PropertyChanged -= chart.OnChartPropertyChanged;

            if (args.NewValue is ChartViewModel newChart)
                newChart.PropertyChanged += chart.OnChartPropertyChanged;
        }

        private void OnChartPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            InvalidateVisual();
        }

        private void DrawGrid(DrawingContext drawingContext, Rect bounds)
        {
            WpfBrush gridBrush = TryFindResource("ChartGridBrush") as WpfBrush ?? WpfBrushes.LightGray;
            WpfPen gridPen = new(gridBrush, 1);
            for (int index = 1; index < 4; index++)
            {
                double y = bounds.Top + bounds.Height * index / 4;
                drawingContext.DrawLine(gridPen, new WpfPoint(bounds.Left, y), new WpfPoint(bounds.Right, y));
            }
        }

        private void DrawSeries(DrawingContext drawingContext, Rect bounds, ChartSeriesViewModel series, double maximum)
        {
            if (series?.Values?.Count > 1 != true)
                return;

            WpfPen pen = new(CreateBrush(series.Color), 2);
            StreamGeometry geometry = new();
            using (StreamGeometryContext context = geometry.Open())
            {
                bool segmentOpen = false;
                int count = series.Values.Count;
                for (int index = 0; index < count; index++)
                {
                    double? value = series.Values[index];
                    if (!value.HasValue)
                    {
                        segmentOpen = false;
                        continue;
                    }

                    double x = bounds.Left + (count == 1 ? 0 : index * bounds.Width / (count - 1));
                    double normalized = Math.Clamp(value.Value / maximum, 0, 1);
                    double y = bounds.Bottom - normalized * bounds.Height;
                    WpfPoint point = new(x, y);

                    if (!segmentOpen)
                    {
                        context.BeginFigure(point, isFilled: false, isClosed: false);
                        segmentOpen = true;
                        continue;
                    }

                    context.LineTo(point, isStroked: true, isSmoothJoin: true);
                }
            }

            geometry.Freeze();
            drawingContext.DrawGeometry(null, pen, geometry);
        }

        private double ResolveMaximum()
        {
            if (Chart?.FixedMaximumY is > 0)
                return Chart.FixedMaximumY.Value;

            double maximum = Chart?.Series
                .SelectMany(series => series.Values)
                .Where(value => value.HasValue)
                .Select(value => value.Value)
                .DefaultIfEmpty(1)
                .Max() ?? 1;

            return maximum <= 0 ? 1 : maximum * 1.1;
        }

        private WpfBrush CreateBrush(string color)
        {
            try
            {
                if (WpfColorConverter.ConvertFromString(color) is WpfColor parsed)
                    return new SolidColorBrush(parsed);
            }
            catch
            {
            }

            return TryFindResource("AccentBrush") as WpfBrush ?? WpfBrushes.DodgerBlue;
        }

        private void DrawCenteredText(DrawingContext drawingContext, Rect bounds, string text)
        {
            WpfBrush brush = TryFindResource("SecondaryTextBrush") as WpfBrush ?? WpfBrushes.Gray;
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                13,
                brush,
                pixelsPerDip)
            {
                MaxTextWidth = Math.Max(1, bounds.Width - 16),
                TextAlignment = TextAlignment.Center
            };

            drawingContext.DrawText(
                formattedText,
                new WpfPoint(bounds.Left + 8, bounds.Top + Math.Max(0, (bounds.Height - formattedText.Height) / 2)));
        }
    }
}
