namespace NVConso
{
    public sealed class TelemetryChartControl : DashboardCard
    {
        private readonly List<TelemetryChartSeries> _series = [];
        private GpuTelemetrySnapshot[] _snapshots = [];
        private ThemePalette _palette = ThemePalette.Light();

        public TelemetryChartControl(string title, double? fixedMaximumY = null)
        {
            Title = title;
            FixedMaximumY = fixedMaximumY;
            TimeRangeLabel = "historique";
            DoubleBuffered = true;
            MinimumSize = new Size(260, 180);
        }

        public string Title { get; }
        public double? FixedMaximumY { get; }
        public string TimeRangeLabel { get; private set; }

        public void AddSeries(string name, Color color, Func<GpuTelemetrySnapshot, double?> valueAccessor)
        {
            _series.Add(new TelemetryChartSeries(name, color, valueAccessor));
        }

        public void SetData(GpuTelemetrySnapshot[] snapshots)
        {
            _snapshots = snapshots ?? [];
            Invalidate();
        }

        public void SetTimeRangeSeconds(int seconds)
        {
            TimeRangeLabel = FormatDurationLabel(seconds);
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

            Rectangle plotArea = new Rectangle(46, 42, Width - 66, Height - 74);
            if (plotArea.Width <= 20 || plotArea.Height <= 20)
                return;

            using var titleBrush = new SolidBrush(_palette.PrimaryText);
            using var mutedBrush = new SolidBrush(_palette.SecondaryText);
            using var gridPen = new Pen(_palette.ChartGrid);
            using var axisPen = new Pen(_palette.Border);

            using Font titleFont = DashboardFonts.SectionTitle();
            graphics.DrawString(Title, titleFont, titleBrush, new PointF(16, 14));
            DrawLegend(graphics, mutedBrush);

            DrawGrid(graphics, plotArea, gridPen, axisPen);

            if (_snapshots.Length < 2)
            {
                graphics.DrawString("En attente de données", Font, mutedBrush, plotArea.Left + 8, plotArea.Top + 8);
                return;
            }

            double maximumY = ResolveMaximumY();
            if (maximumY <= 0)
                maximumY = 1;

            DrawSeries(graphics, plotArea, maximumY);

            string maxLabel = maximumY >= 10
                ? maximumY.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
                : maximumY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            graphics.DrawString(maxLabel, Font, mutedBrush, 8, plotArea.Top - 6);
            graphics.DrawString("0", Font, mutedBrush, 24, plotArea.Bottom - 12);
            graphics.DrawString(TimeRangeLabel, Font, mutedBrush, plotArea.Left, plotArea.Bottom + 8);
            graphics.DrawString("maintenant", Font, mutedBrush, plotArea.Right - 68, plotArea.Bottom + 8);
        }

        public static string FormatDurationLabel(int seconds)
        {
            if (seconds < 60)
                return $"{Math.Max(1, seconds)} s";

            if (seconds < 3600)
                return $"{Math.Max(1, seconds / 60)} min";

            double hours = seconds / 3600.0;
            return hours >= 10
                ? $"{hours:0} h"
                : $"{hours:0.#} h";
        }

        private void DrawLegend(Graphics graphics, Brush mutedBrush)
        {
            int x = 16;
            int y = Height - 24;

            foreach (TelemetryChartSeries series in _series)
            {
                using var pen = new Pen(series.Color, 3);
                graphics.DrawLine(pen, x, y + 7, x + 18, y + 7);
                graphics.DrawString(series.Name, Font, mutedBrush, x + 24, y);
                x += 100;
            }
        }

        private static void DrawGrid(Graphics graphics, Rectangle plotArea, Pen gridPen, Pen axisPen)
        {
            for (int index = 1; index <= 3; index++)
            {
                int y = plotArea.Top + (plotArea.Height * index / 4);
                graphics.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
            }

            for (int index = 1; index <= 4; index++)
            {
                int x = plotArea.Left + (plotArea.Width * index / 5);
                graphics.DrawLine(gridPen, x, plotArea.Top, x, plotArea.Bottom);
            }

            graphics.DrawRectangle(axisPen, plotArea);
        }

        private double ResolveMaximumY()
        {
            if (FixedMaximumY.HasValue)
                return FixedMaximumY.Value;

            double maximum = 0;
            foreach (GpuTelemetrySnapshot snapshot in _snapshots)
            {
                foreach (TelemetryChartSeries series in _series)
                {
                    double? value = series.GetValue(snapshot);
                    if (value.HasValue)
                        maximum = Math.Max(maximum, value.Value);
                }
            }

            if (maximum <= 0)
                return 1;

            return Math.Ceiling(maximum * 1.15 / 10.0) * 10.0;
        }

        private void DrawSeries(Graphics graphics, Rectangle plotArea, double maximumY)
        {
            foreach (TelemetryChartSeries series in _series)
            {
                using var pen = new Pen(series.Color, 2F);
                bool hasPrevious = false;
                PointF previous = default;

                for (int index = 0; index < _snapshots.Length; index++)
                {
                    double? value = series.GetValue(_snapshots[index]);
                    if (!value.HasValue)
                    {
                        hasPrevious = false;
                        continue;
                    }

                    float x = plotArea.Left + (_snapshots.Length == 1
                        ? 0
                        : plotArea.Width * index / (float)(_snapshots.Length - 1));
                    double clampedValue = Math.Clamp(value.Value, 0, maximumY);
                    float y = plotArea.Bottom - (float)(plotArea.Height * clampedValue / maximumY);
                    var current = new PointF(x, y);

                    if (hasPrevious)
                        graphics.DrawLine(pen, previous, current);

                    previous = current;
                    hasPrevious = true;
                }
            }
        }

        private sealed class TelemetryChartSeries
        {
            public TelemetryChartSeries(string name, Color color, Func<GpuTelemetrySnapshot, double?> valueAccessor)
            {
                Name = name;
                Color = color;
                _valueAccessor = valueAccessor;
            }

            private readonly Func<GpuTelemetrySnapshot, double?> _valueAccessor;

            public string Name { get; }
            public Color Color { get; }

            public double? GetValue(GpuTelemetrySnapshot snapshot)
            {
                return _valueAccessor(snapshot);
            }
        }
    }
}
