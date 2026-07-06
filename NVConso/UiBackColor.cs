namespace NVConso
{
    public static class UiBackColor
    {
        public static void Set(Control control, Color color)
        {
            Set(control, color, SystemColors.Control);
        }

        public static void Set(Control control, Color color, Color fallbackColor)
        {
            if (control is null)
                return;

            try
            {
                control.BackColor = color;
            }
            catch (ArgumentException) when (IsTransparent(color))
            {
                control.BackColor = ResolvePaintColor(fallbackColor, SystemColors.Control);
            }
        }

        public static Color ResolvePaintColor(Color color, Color fallbackColor)
        {
            if (!IsTransparent(color))
                return color;

            return IsTransparent(fallbackColor)
                ? SystemColors.Control
                : fallbackColor;
        }

        private static bool IsTransparent(Color color)
        {
            return color == Color.Transparent || color.A == 0;
        }
    }
}
