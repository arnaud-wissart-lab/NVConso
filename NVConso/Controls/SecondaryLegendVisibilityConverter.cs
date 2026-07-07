using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NVConso.Controls
{
    public sealed class SecondaryLegendVisibilityConverter : IMultiValueConverter
    {
        public double CompactWidth { get; set; } = 360;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            int index = values?.Length > 0 && values[0] is int parsedIndex
                ? parsedIndex
                : 0;
            double width = values?.Length > 1 && values[1] is double parsedWidth
                ? parsedWidth
                : double.PositiveInfinity;

            return index <= 0 || width >= CompactWidth
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
