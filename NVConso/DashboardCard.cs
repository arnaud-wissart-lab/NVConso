using System.Drawing.Drawing2D;

namespace NVConso
{
    public class DashboardCard : Panel
    {
        private ThemePalette _palette = ThemePalette.Light();

        public DashboardCard()
        {
            DoubleBuffered = true;
            Padding = new Padding(14);
            Margin = new Padding(0);
        }

        public void ApplyPalette(ThemePalette palette)
        {
            _palette = palette ?? ThemePalette.Light();
            BackColor = _palette.Surface;
            ForeColor = _palette.PrimaryText;
            Invalidate();

            foreach (Control child in Controls)
            {
                child.BackColor = _palette.Surface;
                child.ForeColor = _palette.PrimaryText;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bounds = ClientRectangle;
            bounds.Width -= 1;
            bounds.Height -= 1;

            using GraphicsPath path = DrawingHelpers.CreateRoundedRectangle(bounds, _palette.CardRadius);
            using var backgroundBrush = new SolidBrush(_palette.Surface);
            using var borderPen = new Pen(_palette.Border);

            e.Graphics.FillPath(backgroundBrush, path);
            e.Graphics.DrawPath(borderPen, path);
        }

    }
}
