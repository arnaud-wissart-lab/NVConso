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

    }
}
