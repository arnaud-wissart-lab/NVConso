using System.Drawing;

namespace NVConso
{
    public sealed class TrayMenuPlacementRequest
    {
        public Point CursorPhysicalPosition { get; init; }
        public Rectangle ScreenBoundsPhysical { get; init; }
        public Rectangle WorkingAreaPhysical { get; init; }
        public Size MenuSizePhysical { get; init; }
        public int MarginPhysicalPixels { get; init; } = TrayMenuPlacementService.DefaultMarginPhysicalPixels;
    }

    public interface ITrayMenuPlacementService
    {
        Rectangle Calculate(TrayMenuPlacementRequest request);
    }

    public sealed class TrayMenuPlacementService : ITrayMenuPlacementService
    {
        public const int DefaultMarginPhysicalPixels = 8;

        public Rectangle Calculate(TrayMenuPlacementRequest request)
        {
            Rectangle workingArea = request.WorkingAreaPhysical.IsEmpty
                ? request.ScreenBoundsPhysical
                : request.WorkingAreaPhysical;
            if (workingArea.IsEmpty)
                workingArea = new Rectangle(0, 0, 1, 1);

            int margin = NormalizeMargin(request.MarginPhysicalPixels, workingArea);
            int minLeft = workingArea.Left + margin;
            int minTop = workingArea.Top + margin;
            int maxRight = workingArea.Right - margin;
            int maxBottom = workingArea.Bottom - margin;

            int width = Math.Min(
                Math.Max(1, request.MenuSizePhysical.Width),
                Math.Max(1, maxRight - minLeft));
            int height = Math.Min(
                Math.Max(1, request.MenuSizePhysical.Height),
                Math.Max(1, maxBottom - minTop));

            int preferredLeft = ResolvePreferredLeft(request.CursorPhysicalPosition.X, width, minLeft, maxRight);
            int preferredTop = ResolvePreferredTop(request.CursorPhysicalPosition.Y, height, minTop, maxBottom);

            int left = Clamp(preferredLeft, minLeft, Math.Max(minLeft, maxRight - width));
            int top = Clamp(preferredTop, minTop, Math.Max(minTop, maxBottom - height));

            return new Rectangle(left, top, width, height);
        }

        private static int ResolvePreferredLeft(int cursorX, int width, int minLeft, int maxRight)
        {
            bool fitsRight = cursorX + width <= maxRight;
            bool fitsLeft = cursorX - width >= minLeft;
            if (fitsRight && (!fitsLeft || cursorX - minLeft < maxRight - cursorX))
                return cursorX;

            if (fitsLeft)
                return cursorX - width;

            return cursorX;
        }

        private static int ResolvePreferredTop(int cursorY, int height, int minTop, int maxBottom)
        {
            bool fitsBelow = cursorY + height <= maxBottom;
            bool fitsAbove = cursorY - height >= minTop;
            if (fitsBelow && (!fitsAbove || cursorY - minTop < maxBottom - cursorY))
                return cursorY;

            if (fitsAbove)
                return cursorY - height;

            return cursorY;
        }

        private static int NormalizeMargin(int margin, Rectangle workingArea)
        {
            int requestedMargin = Math.Max(0, margin);
            int maxMargin = Math.Max(0, (Math.Min(workingArea.Width, workingArea.Height) - 1) / 2);
            return Math.Min(requestedMargin, maxMargin);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;

            return value > max ? max : value;
        }
    }
}
