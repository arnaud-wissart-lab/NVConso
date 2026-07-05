namespace NVConso
{
    public class CustomPowerLimitDialog : Form
    {
        private readonly uint _minimumPowerLimitMilliwatt;
        private readonly uint _maximumPowerLimitMilliwatt;
        private readonly NumericUpDown _powerLimitInput;
        private readonly Label _errorLabel;

        public CustomPowerLimitDialog(
            uint minimumPowerLimitMilliwatt,
            uint maximumPowerLimitMilliwatt,
            uint initialPowerLimitMilliwatt)
        {
            _minimumPowerLimitMilliwatt = minimumPowerLimitMilliwatt;
            _maximumPowerLimitMilliwatt = maximumPowerLimitMilliwatt;

            Text = "Limite personnalisée";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(360, 170);

            var rangeLabel = new Label
            {
                AutoSize = true,
                Text = $"Plage autorisée : {GpuTelemetryFormatter.FormatWatts(minimumPowerLimitMilliwatt)} - {GpuTelemetryFormatter.FormatWatts(maximumPowerLimitMilliwatt)}"
            };

            var inputLabel = new Label
            {
                AutoSize = true,
                Text = "Limite en watts"
            };

            _powerLimitInput = new NumericUpDown
            {
                DecimalPlaces = 1,
                Increment = 1,
                Minimum = minimumPowerLimitMilliwatt / 1000m,
                Maximum = maximumPowerLimitMilliwatt / 1000m,
                Value = GetInitialWatts(initialPowerLimitMilliwatt),
                Width = 120
            };

            _errorLabel = new Label
            {
                AutoSize = false,
                ForeColor = Color.Firebrick,
                Height = 34,
                Text = string.Empty
            };

            var applyButton = new Button
            {
                Text = "Appliquer",
                DialogResult = DialogResult.None,
                Width = 90
            };
            applyButton.Click += OnApplyClicked;

            var cancelButton = new Button
            {
                Text = "Annuler",
                DialogResult = DialogResult.Cancel,
                Width = 90
            };

            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill
            };
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(applyButton);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 5
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(rangeLabel, 0, 0);
            layout.SetColumnSpan(rangeLabel, 2);
            layout.Controls.Add(inputLabel, 0, 1);
            layout.Controls.Add(_powerLimitInput, 1, 1);
            layout.Controls.Add(_errorLabel, 0, 2);
            layout.SetColumnSpan(_errorLabel, 2);
            layout.Controls.Add(buttonPanel, 0, 4);
            layout.SetColumnSpan(buttonPanel, 2);

            Controls.Add(layout);
            AcceptButton = applyButton;
            CancelButton = cancelButton;
        }

        public uint TargetPowerLimitMilliwatt { get; private set; }

        private decimal GetInitialWatts(uint initialPowerLimitMilliwatt)
        {
            uint clampedInitialPowerLimit = Math.Clamp(
                initialPowerLimitMilliwatt,
                _minimumPowerLimitMilliwatt,
                _maximumPowerLimitMilliwatt);

            return clampedInitialPowerLimit / 1000m;
        }

        private void OnApplyClicked(object sender, EventArgs e)
        {
            if (!CustomPowerLimitValidator.TryConvertWattsToMilliwatts(
                _powerLimitInput.Value,
                _minimumPowerLimitMilliwatt,
                _maximumPowerLimitMilliwatt,
                out uint targetMilliwatt,
                out string message))
            {
                _errorLabel.Text = message;
                return;
            }

            TargetPowerLimitMilliwatt = targetMilliwatt;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
