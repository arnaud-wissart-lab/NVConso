using NVConso.Views;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NVConso
{
    public partial class CustomPowerLimitDialog : Window
    {
        private readonly uint _minimumPowerLimitMilliwatt;
        private readonly uint _maximumPowerLimitMilliwatt;
        private bool _syncingValue;

        public CustomPowerLimitDialog(
            uint minimumPowerLimitMilliwatt,
            uint maximumPowerLimitMilliwatt,
            uint initialPowerLimitMilliwatt)
        {
            _minimumPowerLimitMilliwatt = minimumPowerLimitMilliwatt;
            _maximumPowerLimitMilliwatt = maximumPowerLimitMilliwatt;

            InitializeComponent();
            Icon = WpfIconLoader.LoadWindowIcon();
            RangeTextBlock.Text = $"Plage autorisée : {GpuTelemetryFormatter.FormatWatts(minimumPowerLimitMilliwatt)} à {GpuTelemetryFormatter.FormatWatts(maximumPowerLimitMilliwatt)}";

            decimal minimumWatts = ToWatts(minimumPowerLimitMilliwatt);
            decimal maximumWatts = ToWatts(maximumPowerLimitMilliwatt);
            decimal initialWatts = ToWatts(Math.Clamp(
                initialPowerLimitMilliwatt,
                minimumPowerLimitMilliwatt,
                maximumPowerLimitMilliwatt));

            PowerLimitSlider.Minimum = (double)minimumWatts;
            PowerLimitSlider.Maximum = (double)maximumWatts;
            PowerLimitSlider.TickFrequency = Math.Max(1d, (PowerLimitSlider.Maximum - PowerLimitSlider.Minimum) / 20d);
            SetCurrentWatts(initialWatts);
        }

        public uint TargetPowerLimitMilliwatt { get; private set; }

        private void PowerLimitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingValue || PowerLimitTextBox is null)
                return;

            SetCurrentWatts(decimal.Round((decimal)e.NewValue, 1, MidpointRounding.AwayFromZero));
        }

        private void PowerLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingValue || !TryParseWatts(PowerLimitTextBox.Text, out decimal watts))
                return;

            decimal clampedWatts = Math.Clamp(
                watts,
                ToWatts(_minimumPowerLimitMilliwatt),
                ToWatts(_maximumPowerLimitMilliwatt));

            _syncingValue = true;
            try
            {
                PowerLimitSlider.Value = (double)clampedWatts;
                ErrorTextBlock.Text = string.Empty;
            }
            finally
            {
                _syncingValue = false;
            }
        }

        private void PowerLimitTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!TryParseWatts(PowerLimitTextBox.Text, out decimal watts))
            {
                SetCurrentWatts((decimal)PowerLimitSlider.Value);
                return;
            }

            SetCurrentWatts(Math.Clamp(
                watts,
                ToWatts(_minimumPowerLimitMilliwatt),
                ToWatts(_maximumPowerLimitMilliwatt)));
        }

        private void PowerLimitTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(character =>
                char.IsDigit(character) || character == ',' || character == '.');
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CustomPowerLimitValidator.TryParseWatts(
                PowerLimitTextBox.Text,
                _minimumPowerLimitMilliwatt,
                _maximumPowerLimitMilliwatt,
                out uint targetMilliwatt,
                out string message))
            {
                ErrorTextBlock.Text = message;
                return;
            }

            TargetPowerLimitMilliwatt = targetMilliwatt;
            DialogResult = true;
            Close();
        }

        private void SetCurrentWatts(decimal watts)
        {
            _syncingValue = true;
            try
            {
                decimal clampedWatts = Math.Clamp(
                    watts,
                    ToWatts(_minimumPowerLimitMilliwatt),
                    ToWatts(_maximumPowerLimitMilliwatt));
                PowerLimitSlider.Value = (double)clampedWatts;
                PowerLimitTextBox.Text = clampedWatts.ToString("0.#", CultureInfo.CurrentCulture);
                ErrorTextBlock.Text = string.Empty;
            }
            finally
            {
                _syncingValue = false;
            }
        }

        private static bool TryParseWatts(string input, out decimal watts)
        {
            watts = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string normalizedInput = input.Trim();
            return decimal.TryParse(normalizedInput, NumberStyles.Number, CultureInfo.CurrentCulture, out watts)
                || decimal.TryParse(normalizedInput, NumberStyles.Number, CultureInfo.InvariantCulture, out watts);
        }

        private static decimal ToWatts(uint milliwatts)
        {
            return milliwatts / 1000m;
        }
    }
}
