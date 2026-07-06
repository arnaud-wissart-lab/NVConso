using System.Drawing.Drawing2D;

namespace NVConso
{
    public sealed class StatusPillControl : Control
    {
        private ThemePalette _palette = ThemePalette.Light();
        private DashboardMetricState _state = DashboardMetricState.Unknown;
        private string _statusText = "Initialisation";

        public StatusPillControl()
        {
            DoubleBuffered = true;
            MinimumSize = new Size(160, 34);
            Height = 34;
            Font = DashboardFonts.Body();
            AccessibleRole = AccessibleRole.StatusBar;
        }

        public void SetStatus(string text, DashboardMetricState state)
        {
            _statusText = string.IsNullOrWhiteSpace(text) ? "Statut indisponible" : text;
            _state = state;
            AccessibleName = "Statut NVML";
            AccessibleDescription = _statusText;
            Invalidate();
        }

        public void ApplyPalette(ThemePalette palette)
        {
            _palette = palette ?? ThemePalette.Light();
            BackColor = Color.Transparent;
            ForeColor = _palette.PrimaryText;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            Color stateColor = _palette.ResolveStateColor(_state);
            Color background = Color.FromArgb(_palette.IsDark ? 48 : 24, stateColor);
            Rectangle bounds = ClientRectangle;
            bounds.Width -= 1;
            bounds.Height -= 1;

            using GraphicsPath path = DrawingHelpers.CreateRoundedRectangle(bounds, _palette.ControlRadius);
            using var backgroundBrush = new SolidBrush(background);
            using var borderPen = new Pen(stateColor);

            e.Graphics.FillPath(backgroundBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            TextRenderer.DrawText(
                e.Graphics,
                _statusText,
                Font,
                bounds,
                _palette.PrimaryText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
