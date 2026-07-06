using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace NVConso
{
    public sealed class GaugeControl : Control
    {
        private ThemePalette _palette = ThemePalette.Light();
        private double? _value;
        private DashboardMetricState _state = DashboardMetricState.Unknown;

        public GaugeControl()
        {
            DoubleBuffered = true;
            MinimumSize = new Size(160, 54);
            Height = 58;
            Font = DashboardFonts.Body();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { get; set; } = string.Empty;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ValueText { get; set; } = "--";

        public void SetValue(double? value, string valueText, DashboardMetricState state)
        {
            _value = value.HasValue ? Math.Clamp(value.Value, 0, 1) : null;
            ValueText = valueText ?? "--";
            _state = state;
            AccessibleDescription = $"{Title}: {ValueText}";
            Invalidate();
        }

        public void ApplyPalette(ThemePalette palette)
        {
            _palette = palette ?? ThemePalette.Light();
            BackColor = _palette.Surface;
            ForeColor = _palette.PrimaryText;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            using var titleBrush = new SolidBrush(_palette.SecondaryText);
            using var valueBrush = new SolidBrush(_palette.PrimaryText);
            using var trackBrush = new SolidBrush(_palette.SurfaceMuted);
            using var fillBrush = new SolidBrush(_palette.ResolveStateColor(_state));

            e.Graphics.DrawString(Title, Font, titleBrush, new PointF(0, 0));

            SizeF valueSize = e.Graphics.MeasureString(ValueText, Font);
            e.Graphics.DrawString(
                ValueText,
                Font,
                valueBrush,
                new PointF(Width - valueSize.Width, 0));

            Rectangle bar = new Rectangle(0, 30, Width, 12);
            using GraphicsPath trackPath = DrawingHelpers.CreateRoundedRectangle(bar, _palette.ControlRadius);
            e.Graphics.FillPath(trackBrush, trackPath);

            if (_value.HasValue && _value.Value > 0)
            {
                int fillWidth = Math.Max(8, (int)Math.Round(bar.Width * _value.Value));
                Rectangle fill = new Rectangle(bar.Left, bar.Top, Math.Min(fillWidth, bar.Width), bar.Height);
                using GraphicsPath fillPath = DrawingHelpers.CreateRoundedRectangle(fill, _palette.ControlRadius);
                e.Graphics.FillPath(fillBrush, fillPath);
            }
        }
    }
}
