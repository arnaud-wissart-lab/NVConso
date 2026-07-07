using System.Windows;
using WpfPanel = System.Windows.Controls.Panel;
using WpfSize = System.Windows.Size;

namespace NVConso.Controls
{
    public sealed class ResponsiveWrapPanel : WpfPanel
    {
        public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
            nameof(MinItemWidth),
            typeof(double),
            typeof(ResponsiveWrapPanel),
            new FrameworkPropertyMetadata(220.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
            nameof(MaxColumns),
            typeof(int),
            typeof(ResponsiveWrapPanel),
            new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ItemSpacingProperty = DependencyProperty.Register(
            nameof(ItemSpacing),
            typeof(double),
            typeof(ResponsiveWrapPanel),
            new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double MinItemWidth
        {
            get => (double)GetValue(MinItemWidthProperty);
            set => SetValue(MinItemWidthProperty, value);
        }

        public int MaxColumns
        {
            get => (int)GetValue(MaxColumnsProperty);
            set => SetValue(MaxColumnsProperty, value);
        }

        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        public static int CalculateColumnCount(
            double availableWidth,
            double minItemWidth,
            int maxColumns,
            double itemSpacing,
            int itemCount)
        {
            if (itemCount <= 0)
                return 0;

            int effectiveMaxColumns = maxColumns <= 0 ? itemCount : Math.Min(maxColumns, itemCount);
            if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) || availableWidth <= 0)
                return Math.Max(1, effectiveMaxColumns);

            double effectiveMinItemWidth = Math.Max(1, minItemWidth);
            double effectiveSpacing = Math.Max(0, itemSpacing);
            int columnsForWidth = (int)Math.Floor((availableWidth + effectiveSpacing) / (effectiveMinItemWidth + effectiveSpacing));
            return Math.Clamp(columnsForWidth, 1, effectiveMaxColumns);
        }

        protected override WpfSize MeasureOverride(WpfSize availableSize)
        {
            int childCount = InternalChildren.Count;
            int columns = CalculateColumnCount(availableSize.Width, MinItemWidth, MaxColumns, ItemSpacing, childCount);
            if (columns == 0)
                return new WpfSize();

            double spacing = Math.Max(0, ItemSpacing);
            double itemWidth = CalculateItemWidth(availableSize.Width, columns, spacing);
            double totalHeight = 0;
            double rowHeight = 0;
            int column = 0;

            int childIndex = 0;
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new WpfSize(itemWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                column++;
                childIndex++;

                if (column < columns)
                    continue;

                totalHeight += rowHeight;
                if (childIndex < childCount)
                    totalHeight += spacing;

                rowHeight = 0;
                column = 0;
            }

            if (column > 0)
                totalHeight += rowHeight;

            double width = double.IsInfinity(availableSize.Width)
                ? (itemWidth * columns) + (spacing * (columns - 1))
                : availableSize.Width;
            return new WpfSize(width, totalHeight);
        }

        protected override WpfSize ArrangeOverride(WpfSize finalSize)
        {
            int columns = CalculateColumnCount(finalSize.Width, MinItemWidth, MaxColumns, ItemSpacing, InternalChildren.Count);
            if (columns == 0)
                return finalSize;

            double spacing = Math.Max(0, ItemSpacing);
            double itemWidth = CalculateItemWidth(finalSize.Width, columns, spacing);
            double x = 0;
            double y = 0;
            double rowHeight = 0;
            int column = 0;

            foreach (UIElement child in InternalChildren)
            {
                child.Arrange(new Rect(x, y, itemWidth, child.DesiredSize.Height));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                column++;

                if (column >= columns)
                {
                    x = 0;
                    y += rowHeight + spacing;
                    rowHeight = 0;
                    column = 0;
                    continue;
                }

                x += itemWidth + spacing;
            }

            return finalSize;
        }

        private double CalculateItemWidth(double availableWidth, int columns, double spacing)
        {
            if (columns <= 0)
                return 0;

            if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) || availableWidth <= 0)
                return Math.Max(1, MinItemWidth);

            return Math.Max(1, (availableWidth - (spacing * (columns - 1))) / columns);
        }
    }
}
