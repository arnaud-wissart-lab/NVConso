using System.Drawing;

namespace NVConso
{
    public sealed class DashboardWindowBounds
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public bool IsUsable()
        {
            return Width >= 900 && Height >= 600;
        }

        public Rectangle ToRectangle()
        {
            return new Rectangle(X, Y, Width, Height);
        }

        public static DashboardWindowBounds FromRectangle(Rectangle bounds)
        {
            return new DashboardWindowBounds
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height
            };
        }
    }
}
