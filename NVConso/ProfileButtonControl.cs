namespace NVConso
{
    public sealed class ProfileButtonControl : Button
    {
        public ProfileButtonControl()
        {
            FlatStyle = FlatStyle.Flat;
            Height = 42;
            MinimumSize = new Size(112, 42);
            Margin = new Padding(0, 2, 10, 2);
            Font = DashboardFonts.Button();
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
        }

        public void ApplyPalette(ThemePalette palette)
        {
            ThemePalette resolvedPalette = palette ?? ThemePalette.Light();
            BackColor = resolvedPalette.SurfaceMuted;
            ForeColor = resolvedPalette.PrimaryText;
            FlatAppearance.BorderColor = resolvedPalette.Border;
            FlatAppearance.MouseOverBackColor = resolvedPalette.Border;
            FlatAppearance.MouseDownBackColor = resolvedPalette.Accent;
        }
    }
}
