using System.ComponentModel;

namespace NVConso
{
    public sealed class ProfileButtonControl : Button
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsWarning { get; set; }

        public ProfileButtonControl()
        {
            FlatStyle = FlatStyle.Flat;
            Height = 34;
            MinimumSize = new Size(92, 34);
            Margin = new Padding(0, 2, 8, 2);
            Font = DashboardFonts.Button();
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            AutoEllipsis = true;
        }

        public void ApplyPalette(ThemePalette palette)
        {
            ThemePalette resolvedPalette = palette ?? ThemePalette.Light();
            BackColor = IsWarning
                ? Color.FromArgb(resolvedPalette.IsDark ? 60 : 28, resolvedPalette.Warning)
                : resolvedPalette.SurfaceMuted;
            ForeColor = IsWarning ? resolvedPalette.Warning : resolvedPalette.PrimaryText;
            FlatAppearance.BorderColor = IsWarning ? resolvedPalette.Warning : resolvedPalette.Border;
            FlatAppearance.MouseOverBackColor = IsWarning
                ? Color.FromArgb(resolvedPalette.IsDark ? 78 : 42, resolvedPalette.Warning)
                : resolvedPalette.Border;
            FlatAppearance.MouseDownBackColor = resolvedPalette.Accent;
        }
    }
}
