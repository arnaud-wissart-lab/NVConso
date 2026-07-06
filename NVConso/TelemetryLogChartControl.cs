using System.Globalization;

namespace NVConso
{
    public sealed class TelemetryLogChartControl : DashboardCard
    {
        private IReadOnlyList<TelemetryLogEntry> _entries = [];
        private TelemetryHistoryMetric _metric = TelemetryHistoryMetric.PowerUsageW;
        private ThemePalette _palette = ThemePalette.Light();
        private string _message = "Sélectionnez une date.";

        public TelemetryLogChartControl()
        {
            DoubleBuffered = true;
            MinimumSize = new Size(420, 220);
        }

        public void SetData(
            IReadOnlyList<TelemetryLogEntry> entries,
            TelemetryHistoryMetric metric,
            string message)
        {
            _entries = entries ?? [];
            _metric = metric;
            _message = string.IsNullOrWhiteSpace(message) ? "Aucune donnée." : message;
            Invalidate();
        }

        public new void ApplyPalette(ThemePalette palette)
        {
            _palette = palette ?? ThemePalette.Light();
            base.ApplyPalette(_palette);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Rectangle plotArea = new Rectangle(62, 50, Width - 84, Height - 94);
            if (plotArea.Width <= 20 || plotArea.Height <= 20)
                return;

            using var titleBrush = new SolidBrush(_palette.PrimaryText);
            using var mutedBrush = new SolidBrush(_palette.SecondaryText);
            using var gridPen = new Pen(_palette.ChartGrid);
            using var axisPen = new Pen(_palette.Border);
            using var linePen = new Pen(_palette.Accent, 2F);
            using Font titleFont = DashboardFonts.SectionTitle();

            string unit = TelemetryHistoryMetrics.GetUnit(_metric);
            string title = $"{TelemetryHistoryMetrics.GetDisplayName(_metric)} sur la journée";
            graphics.DrawString(title, titleFont, titleBrush, new PointF(16, 14));

            DrawGrid(graphics, plotArea, gridPen, axisPen);

            if (_entries.Count < 2)
            {
                graphics.DrawString(_message, Font, mutedBrush, plotArea.Left + 8, plotArea.Top + 8);
                DrawAxisLabels(graphics, plotArea, mutedBrush, unit, maximumY: null);
                return;
            }

            double maximumY = ResolveMaximumY();
            if (maximumY <= 0)
                maximumY = 1;

            bool hasPrevious = false;
            PointF previous = default;
            foreach (TelemetryLogEntry entry in _entries)
            {
                double? value = TelemetryHistoryMetrics.GetValue(entry, _metric);
                if (!value.HasValue)
                {
                    hasPrevious = false;
                    continue;
                }

                TimeSpan timeOfDay = entry.TimestampLocal.TimeOfDay;
                float x = plotArea.Left + (float)(plotArea.Width * timeOfDay.TotalSeconds / TimeSpan.FromDays(1).TotalSeconds);
                double clampedValue = Math.Clamp(value.Value, 0, maximumY);
                float y = plotArea.Bottom - (float)(plotArea.Height * clampedValue / maximumY);
                var current = new PointF(x, y);

                if (hasPrevious)
                    graphics.DrawLine(linePen, previous, current);

                previous = current;
                hasPrevious = true;
            }

            DrawAxisLabels(graphics, plotArea, mutedBrush, unit, maximumY);
        }

        private static void DrawGrid(Graphics graphics, Rectangle plotArea, Pen gridPen, Pen axisPen)
        {
            for (int index = 1; index <= 3; index++)
            {
                int y = plotArea.Top + (plotArea.Height * index / 4);
                graphics.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
            }

            for (int index = 1; index <= 5; index++)
            {
                int x = plotArea.Left + (plotArea.Width * index / 6);
                graphics.DrawLine(gridPen, x, plotArea.Top, x, plotArea.Bottom);
            }

            graphics.DrawRectangle(axisPen, plotArea);
        }

        private void DrawAxisLabels(
            Graphics graphics,
            Rectangle plotArea,
            Brush mutedBrush,
            string unit,
            double? maximumY)
        {
            string maximumLabel = maximumY.HasValue
                ? $"{FormatNumber(maximumY.Value)} {unit}".Trim()
                : unit;

            graphics.DrawString(maximumLabel, Font, mutedBrush, 8, plotArea.Top - 6);
            graphics.DrawString("0", Font, mutedBrush, 30, plotArea.Bottom - 12);
            DrawStringLeft(graphics, "00:00", mutedBrush, plotArea.Left, plotArea.Bottom + 8);
            DrawStringCentered(graphics, "12:00", mutedBrush, plotArea.Left + (plotArea.Width / 2), plotArea.Bottom + 8);
            DrawStringRight(graphics, "23:59", mutedBrush, plotArea.Right, plotArea.Bottom + 8);
        }

        private void DrawStringLeft(Graphics graphics, string text, Brush brush, int x, int y)
        {
            graphics.DrawString(text, Font, brush, x, y);
        }

        private void DrawStringCentered(Graphics graphics, string text, Brush brush, int centerX, int y)
        {
            SizeF size = graphics.MeasureString(text, Font);
            graphics.DrawString(text, Font, brush, centerX - (size.Width / 2), y);
        }

        private void DrawStringRight(Graphics graphics, string text, Brush brush, int right, int y)
        {
            SizeF size = graphics.MeasureString(text, Font);
            graphics.DrawString(text, Font, brush, right - size.Width, y);
        }

        private double ResolveMaximumY()
        {
            double maximum = 0;
            foreach (TelemetryLogEntry entry in _entries)
            {
                double? value = TelemetryHistoryMetrics.GetValue(entry, _metric);
                if (value.HasValue)
                    maximum = Math.Max(maximum, value.Value);
            }

            if (_metric is TelemetryHistoryMetric.TemperatureC
                or TelemetryHistoryMetric.GpuUtilizationPercent
                or TelemetryHistoryMetric.DecoderUtilizationPercent)
            {
                return Math.Max(100, maximum);
            }

            return maximum <= 0
                ? 1
                : Math.Ceiling(maximum * 1.15 / 10.0) * 10.0;
        }

        private static string FormatNumber(double value)
        {
            return value >= 10
                ? value.ToString("F0", CultureInfo.InvariantCulture)
                : value.ToString("F1", CultureInfo.InvariantCulture);
        }
    }
}
