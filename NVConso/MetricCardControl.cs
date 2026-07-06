namespace NVConso
{
    public sealed class MetricCardControl : DashboardCard
    {
        private const float PreferredValueFontSize = 15F;
        private const float MinimumValueFontSize = 12F;

        private readonly Label _titleLabel;
        private readonly Label _valueLabel;
        private readonly Label _detailLabel;
        private ThemePalette _palette = ThemePalette.Light();

        public MetricCardControl(string title)
        {
            MinimumSize = new Size(150, 86);
            Padding = new Padding(12, 10, 12, 10);

            _titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 20,
                Text = title,
                Font = DashboardFonts.Body(),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            _valueLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Text = "--",
                Font = DashboardFonts.CardValue(),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false
            };

            _detailLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = 18,
                Text = string.Empty,
                Font = DashboardFonts.Small(),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
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
            FitValueFont();
        }

        public new void ApplyPalette(ThemePalette palette)
        {
            _palette = palette ?? ThemePalette.Light();
            base.ApplyPalette(_palette);
            _titleLabel.ForeColor = _palette.SecondaryText;
            _detailLabel.ForeColor = _palette.SecondaryText;
            _valueLabel.ForeColor = _palette.Accent;
            FitValueFont();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            FitValueFont();
        }

        private void FitValueFont()
        {
            if (_valueLabel.Width <= 0 || string.IsNullOrWhiteSpace(_valueLabel.Text))
                return;

            float size = PreferredValueFontSize;
            Size proposedSize = new(Math.Max(1, _valueLabel.Width), Math.Max(1, _valueLabel.Height));
            while (size > MinimumValueFontSize)
            {
                using var candidate = new Font("Segoe UI Semibold", size, FontStyle.Bold);
                Size textSize = TextRenderer.MeasureText(
                    _valueLabel.Text,
                    candidate,
                    proposedSize,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                if (textSize.Width <= proposedSize.Width && textSize.Height <= proposedSize.Height)
                    break;

                size -= 0.5F;
            }

            if (Math.Abs(_valueLabel.Font.Size - size) > 0.1F)
                _valueLabel.Font = new Font("Segoe UI Semibold", size, FontStyle.Bold);
        }
    }
}
