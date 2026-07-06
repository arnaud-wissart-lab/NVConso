namespace NVConso
{
    public sealed class ThemePalette
    {
        private ThemePalette(
            bool isDark,
            Color background,
            Color surface,
            Color surfaceMuted,
            Color border,
            Color primaryText,
            Color secondaryText,
            Color accent,
            Color warning,
            Color critical,
            Color success,
            Color chartGrid,
            int cardRadius,
            int controlRadius)
        {
            IsDark = isDark;
            Background = background;
            Surface = surface;
            SurfaceMuted = surfaceMuted;
            Border = border;
            PrimaryText = primaryText;
            SecondaryText = secondaryText;
            Accent = accent;
            Warning = warning;
            Critical = critical;
            Success = success;
            ChartGrid = chartGrid;
            CardRadius = cardRadius;
            ControlRadius = controlRadius;
        }

        public bool IsDark { get; }
        public Color Background { get; }
        public Color Surface { get; }
        public Color SurfaceMuted { get; }
        public Color Border { get; }
        public Color PrimaryText { get; }
        public Color SecondaryText { get; }
        public Color Accent { get; }
        public Color Warning { get; }
        public Color Critical { get; }
        public Color Success { get; }
        public Color ChartGrid { get; }
        public int CardRadius { get; }
        public int ControlRadius { get; }

        public static ThemePalette Light()
        {
            return new ThemePalette(
                isDark: false,
                background: Color.FromArgb(245, 247, 250),
                surface: Color.White,
                surfaceMuted: Color.FromArgb(238, 242, 247),
                border: Color.FromArgb(218, 225, 234),
                primaryText: Color.FromArgb(31, 41, 55),
                secondaryText: Color.FromArgb(95, 109, 126),
                accent: Color.FromArgb(37, 99, 160),
                warning: Color.FromArgb(158, 101, 0),
                critical: Color.FromArgb(178, 52, 52),
                success: Color.FromArgb(30, 128, 84),
                chartGrid: Color.FromArgb(225, 231, 240),
                UiSpacing.CardRadius,
                UiSpacing.ControlRadius);
        }

        public static ThemePalette Dark()
        {
            return new ThemePalette(
                isDark: true,
                background: Color.FromArgb(18, 24, 32),
                surface: Color.FromArgb(28, 36, 47),
                surfaceMuted: Color.FromArgb(38, 48, 62),
                border: Color.FromArgb(58, 70, 86),
                primaryText: Color.FromArgb(236, 242, 248),
                secondaryText: Color.FromArgb(166, 179, 194),
                accent: Color.FromArgb(91, 168, 224),
                warning: Color.FromArgb(226, 171, 69),
                critical: Color.FromArgb(226, 103, 103),
                success: Color.FromArgb(86, 192, 135),
                chartGrid: Color.FromArgb(49, 60, 74),
                UiSpacing.CardRadius,
                UiSpacing.ControlRadius);
        }

        public Color ResolveStateColor(DashboardMetricState state)
        {
            return state switch
            {
                DashboardMetricState.Warning => Warning,
                DashboardMetricState.Critical => Critical,
                DashboardMetricState.Unknown => SecondaryText,
                _ => Accent
            };
        }
    }
}
