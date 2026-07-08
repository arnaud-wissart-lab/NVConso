using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;

namespace NVConso.Controls
{
    public partial class NumericBox : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value),
            typeof(int),
            typeof(NumericBox),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged,
                (dependencyObject, baseValue) => CoerceNumericValue((NumericBox)dependencyObject, (int)baseValue)));

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            nameof(Minimum),
            typeof(int),
            typeof(NumericBox),
            new PropertyMetadata(int.MinValue, OnBoundsChanged));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum),
            typeof(int),
            typeof(NumericBox),
            new PropertyMetadata(int.MaxValue, OnBoundsChanged));

        public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
            nameof(Unit),
            typeof(string),
            typeof(NumericBox),
            new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty RecommendationTextProperty = DependencyProperty.Register(
            nameof(RecommendationText),
            typeof(string),
            typeof(NumericBox),
            new PropertyMetadata(string.Empty));

        private bool _syncingText;

        public NumericBox()
        {
            InitializeComponent();
            System.Windows.DataObject.AddPastingHandler(ValueTextBox, OnPaste);
            UpdateTextFromValue();
        }

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public int Minimum
        {
            get => (int)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public int Maximum
        {
            get => (int)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public string RecommendationText
        {
            get => (string)GetValue(RecommendationTextProperty);
            set => SetValue(RecommendationTextProperty, value);
        }

        public static int NormalizeValue(int value, int minimum, int maximum)
        {
            if (minimum > maximum)
                (minimum, maximum) = (maximum, minimum);

            return Math.Clamp(value, minimum, maximum);
        }

        private static int CoerceNumericValue(NumericBox numericBox, int baseValue)
        {
            return NormalizeValue(baseValue, numericBox.Minimum, numericBox.Maximum);
        }

        private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            ((NumericBox)dependencyObject).UpdateTextFromValue();
        }

        private static void OnBoundsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            var numericBox = (NumericBox)dependencyObject;
            numericBox.CoerceValue(ValueProperty);
            numericBox.UpdateTextFromValue();
        }

        private void ValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingText)
                return;

            if (!int.TryParse(ValueTextBox.Text, out int parsed))
                return;

            Value = NormalizeValue(parsed, Minimum, Maximum);
        }

        private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(ValueTextBox.Text, out int parsed))
            {
                UpdateTextFromValue();
                return;
            }

            Value = NormalizeValue(parsed, Minimum, Maximum);
            UpdateTextFromValue();
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(System.Windows.DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            string pastedText = e.DataObject.GetData(System.Windows.DataFormats.Text) as string;
            if (string.IsNullOrWhiteSpace(pastedText) || !pastedText.All(char.IsDigit))
                e.CancelCommand();
        }

        private void UpdateTextFromValue()
        {
            if (ValueTextBox is null)
                return;

            string value = Value.ToString(CultureInfo.InvariantCulture);
            if (ValueTextBox.Text == value)
                return;

            _syncingText = true;
            try
            {
                ValueTextBox.Text = value;
            }
            finally
            {
                _syncingText = false;
            }
        }
    }
}
