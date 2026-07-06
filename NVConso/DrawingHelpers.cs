using System.Drawing.Drawing2D;

namespace NVConso
{
    public static class DrawingHelpers
    {
        public static GraphicsPath CreateRoundedRectangle(Rectangle rectangle, int radius)
        {
            int normalizedRadius = Math.Max(0, radius);
            int diameter = normalizedRadius * 2;
            var path = new GraphicsPath();

            if (diameter == 0)
            {
                path.AddRectangle(rectangle);
                return path;
            }

            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
