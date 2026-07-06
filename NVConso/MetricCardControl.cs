namespace NVConso
{
    public sealed class MetricCardControl : DashboardCard
    {
        private readonly Label _titleLabel;
        private readonly Label _valueLabel;
        private readonly Label _detailLabel;
        private ThemePalette _palette = ThemePalette.Light();

        public MetricCardControl(string title)
        {
            Height = 96;

            _titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 22,
                Text = title,
                Font = DashboardFonts.Body(),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _valueLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Text = "--",
                Font = DashboardFonts.CardValue(),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _detailLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = 20,
                Text = string.Empty,
                Font = DashboardFonts.Small(),
                TextAlign = ContentAlignment.MiddleLeft
            };

            Controls.Add(_valueLabel);
            Controls.Add(_detailLabel);
            Controls.Add(_titleLabel);
        }

        public void SetValue(string value, string detail = null, DashboardMetricState state = DashboardMetricState.Normal)
        {
            _valueLabel.Text = value ?? "--";
            _detailLabel.Text = detail ?? string.Empty;
            _valueLabel.ForeColor = _palette.ResolveStateColor(state);
        }

        public new void ApplyPalette(ThemePalette palette)
        {
            _palette = palette ?? ThemePalette.Light();
            base.ApplyPalette(_palette);
            _titleLabel.ForeColor = _palette.SecondaryText;
            _detailLabel.ForeColor = _palette.SecondaryText;
            _valueLabel.ForeColor = _palette.Accent;
        }
    }
}
