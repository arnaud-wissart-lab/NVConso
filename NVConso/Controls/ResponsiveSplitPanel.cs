using System.Windows;
using WpfPanel = System.Windows.Controls.Panel;
using WpfSize = System.Windows.Size;

namespace NVConso.Controls
{
    public sealed class ResponsiveSplitPanel : WpfPanel
    {
        public static readonly DependencyProperty PrimaryMinWidthProperty = DependencyProperty.Register(
            nameof(PrimaryMinWidth),
            typeof(double),
            typeof(ResponsiveSplitPanel),
            new FrameworkPropertyMetadata(640.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty SecondaryMinWidthProperty = DependencyProperty.Register(
            nameof(SecondaryMinWidth),
            typeof(double),
            typeof(ResponsiveSplitPanel),
            new FrameworkPropertyMetadata(280.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty SecondaryMaxWidthProperty = DependencyProperty.Register(
            nameof(SecondaryMaxWidth),
            typeof(double),
            typeof(ResponsiveSplitPanel),
            new FrameworkPropertyMetadata(360.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty SecondaryWidthRatioProperty = DependencyProperty.Register(
            nameof(SecondaryWidthRatio),
            typeof(double),
            typeof(ResponsiveSplitPanel),
            new FrameworkPropertyMetadata(0.28, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ItemSpacingProperty = DependencyProperty.Register(
            nameof(ItemSpacing),
            typeof(double),
            typeof(ResponsiveSplitPanel),
            new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double PrimaryMinWidth
        {
            get => (double)GetValue(PrimaryMinWidthProperty);
            set => SetValue(PrimaryMinWidthProperty, value);
        }

        public double SecondaryMinWidth
        {
            get => (double)GetValue(SecondaryMinWidthProperty);
            set => SetValue(SecondaryMinWidthProperty, value);
        }

        public double SecondaryMaxWidth
        {
            get => (double)GetValue(SecondaryMaxWidthProperty);
            set => SetValue(SecondaryMaxWidthProperty, value);
        }

        public double SecondaryWidthRatio
        {
            get => (double)GetValue(SecondaryWidthRatioProperty);
            set => SetValue(SecondaryWidthRatioProperty, value);
        }

        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        public static bool ShouldStack(
            double availableWidth,
            double primaryMinWidth,
            double secondaryMinWidth,
            double itemSpacing)
        {
            if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) || availableWidth <= 0)
                return false;

            return availableWidth < Math.Max(1, primaryMinWidth) + Math.Max(1, secondaryMinWidth) + Math.Max(0, itemSpacing);
        }

        protected override WpfSize MeasureOverride(WpfSize availableSize)
        {
            if (InternalChildren.Count == 0)
                return new WpfSize();

            double width = double.IsInfinity(availableSize.Width)
                ? PrimaryMinWidth + SecondaryMaxWidth + ItemSpacing
                : availableSize.Width;
            bool stack = InternalChildren.Count < 2 || ShouldStack(width, PrimaryMinWidth, SecondaryMinWidth, ItemSpacing);

            if (stack)
                return MeasureStacked(width);

            (double primaryWidth, double secondaryWidth) = CalculateSplitWidths(width);
            InternalChildren[0].Measure(new WpfSize(primaryWidth, double.PositiveInfinity));
            InternalChildren[1].Measure(new WpfSize(secondaryWidth, double.PositiveInfinity));

            double height = Math.Max(InternalChildren[0].DesiredSize.Height, InternalChildren[1].DesiredSize.Height);
            for (int index = 2; index < InternalChildren.Count; index++)
            {
                InternalChildren[index].Measure(new WpfSize(width, double.PositiveInfinity));
                height += ItemSpacing + InternalChildren[index].DesiredSize.Height;
            }

            return new WpfSize(width, height);
        }

        protected override WpfSize ArrangeOverride(WpfSize finalSize)
        {
            if (InternalChildren.Count == 0)
                return finalSize;

            bool stack = InternalChildren.Count < 2 || ShouldStack(finalSize.Width, PrimaryMinWidth, SecondaryMinWidth, ItemSpacing);
            if (stack)
                return ArrangeStacked(finalSize);

            (double primaryWidth, double secondaryWidth) = CalculateSplitWidths(finalSize.Width);
            double rowHeight = Math.Max(InternalChildren[0].DesiredSize.Height, InternalChildren[1].DesiredSize.Height);
            InternalChildren[0].Arrange(new Rect(0, 0, primaryWidth, rowHeight));
            InternalChildren[1].Arrange(new Rect(primaryWidth + ItemSpacing, 0, secondaryWidth, rowHeight));

            double y = rowHeight + ItemSpacing;
            for (int index = 2; index < InternalChildren.Count; index++)
            {
                UIElement child = InternalChildren[index];
                child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height + ItemSpacing;
            }

            return finalSize;
        }

        private WpfSize MeasureStacked(double width)
        {
            double height = 0;
            for (int index = 0; index < InternalChildren.Count; index++)
            {
                UIElement child = InternalChildren[index];
                child.Measure(new WpfSize(width, double.PositiveInfinity));
                height += child.DesiredSize.Height;
                if (index < InternalChildren.Count - 1)
                    height += Math.Max(0, ItemSpacing);
            }

            return new WpfSize(width, height);
        }

        private WpfSize ArrangeStacked(WpfSize finalSize)
        {
            double y = 0;
            double spacing = Math.Max(0, ItemSpacing);
            for (int index = 0; index < InternalChildren.Count; index++)
            {
                UIElement child = InternalChildren[index];
                child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height;
                if (index < InternalChildren.Count - 1)
                    y += spacing;
            }

            return finalSize;
        }

        private (double PrimaryWidth, double SecondaryWidth) CalculateSplitWidths(double width)
        {
            double spacing = Math.Max(0, ItemSpacing);
            double ratio = Math.Clamp(SecondaryWidthRatio, 0.2, 0.45);
            double secondaryMaxWidth = Math.Max(SecondaryMinWidth, SecondaryMaxWidth);
            double secondaryWidth = Math.Clamp(width * ratio, SecondaryMinWidth, secondaryMaxWidth);
            double primaryWidth = width - secondaryWidth - spacing;

            if (primaryWidth < PrimaryMinWidth)
            {
                primaryWidth = PrimaryMinWidth;
                secondaryWidth = Math.Max(SecondaryMinWidth, width - primaryWidth - spacing);
            }

            return (primaryWidth, secondaryWidth);
        }
    }
}
