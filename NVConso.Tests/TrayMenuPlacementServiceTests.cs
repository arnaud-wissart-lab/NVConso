using System.Drawing;

namespace NVConso.Tests
{
    public class TrayMenuPlacementServiceTests
    {
        private readonly TrayMenuPlacementService _service = new();

        [Fact]
        public void Calculate_ShouldClampBottomRight_OnFullHdDpi100()
        {
            Rectangle result = Calculate(
                bounds: new Rectangle(0, 0, 1920, 1080),
                workingArea: new Rectangle(0, 0, 1920, 1080),
                cursor: new Point(1910, 1070),
                menuSize: new Size(320, 420));

            AssertInside(result, new Rectangle(0, 0, 1920, 1080));
            Assert.True(result.Left < 1910);
            Assert.True(result.Top < 1070);
        }

        [Fact]
        public void Calculate_ShouldClampBottomRight_OnQhdDpi150()
        {
            Rectangle result = Calculate(
                bounds: new Rectangle(0, 0, 2560, 1440),
                workingArea: new Rectangle(0, 0, 2560, 1440),
                cursor: new Point(2548, 1428),
                menuSize: new Size(480, 630));

            AssertInside(result, new Rectangle(0, 0, 2560, 1440));
            Assert.True(result.Left < 2548);
            Assert.True(result.Top < 1428);
        }

        [Fact]
        public void Calculate_ShouldSupportNegativeSecondaryScreen()
        {
            var workingArea = new Rectangle(-1920, 0, 1920, 1080);

            Rectangle result = Calculate(
                bounds: new Rectangle(-1920, 0, 1920, 1080),
                workingArea: workingArea,
                cursor: new Point(-12, 1048),
                menuSize: new Size(320, 420));

            AssertInside(result, workingArea);
            Assert.True(result.Left < -12);
        }

        [Fact]
        public void Calculate_ShouldPreferBelowCursor_WhenTaskbarIsAtTop()
        {
            var workingArea = new Rectangle(0, 40, 1920, 1040);

            Rectangle result = Calculate(
                bounds: new Rectangle(0, 0, 1920, 1080),
                workingArea: workingArea,
                cursor: new Point(900, 45),
                menuSize: new Size(320, 420));

            AssertInside(result, workingArea);
            Assert.True(result.Top >= 45);
        }

        [Fact]
        public void Calculate_ShouldRespectTaskbarAtRight()
        {
            var workingArea = new Rectangle(0, 0, 1840, 1080);

            Rectangle result = Calculate(
                bounds: new Rectangle(0, 0, 1920, 1080),
                workingArea: workingArea,
                cursor: new Point(1834, 500),
                menuSize: new Size(320, 420));

            AssertInside(result, workingArea);
            Assert.True(result.Left < 1834);
        }

        [Fact]
        public void Calculate_ShouldPlaceNearLeftEdgeInsideWorkingArea()
        {
            var workingArea = new Rectangle(0, 0, 1920, 1080);

            Rectangle result = Calculate(
                bounds: workingArea,
                workingArea: workingArea,
                cursor: new Point(2, 500),
                menuSize: new Size(320, 420));

            AssertInside(result, workingArea);
            Assert.True(result.Left >= 8);
        }

        [Fact]
        public void Calculate_ShouldPlaceNearRightEdgeInsideWorkingArea()
        {
            var workingArea = new Rectangle(0, 0, 1920, 1080);

            Rectangle result = Calculate(
                bounds: workingArea,
                workingArea: workingArea,
                cursor: new Point(1918, 500),
                menuSize: new Size(320, 420));

            AssertInside(result, workingArea);
            Assert.True(result.Right <= 1912);
        }

        [Fact]
        public void Calculate_ShouldFitOversizedMenuInsideWorkingArea()
        {
            var workingArea = new Rectangle(0, 0, 1920, 1080);

            Rectangle result = Calculate(
                bounds: workingArea,
                workingArea: workingArea,
                cursor: new Point(960, 540),
                menuSize: new Size(3000, 2000));

            AssertInside(result, workingArea);
            Assert.Equal(1904, result.Width);
            Assert.Equal(1064, result.Height);
        }

        private Rectangle Calculate(
            Rectangle bounds,
            Rectangle workingArea,
            Point cursor,
            Size menuSize)
        {
            return _service.Calculate(new TrayMenuPlacementRequest
            {
                CursorPhysicalPosition = cursor,
                ScreenBoundsPhysical = bounds,
                WorkingAreaPhysical = workingArea,
                MenuSizePhysical = menuSize
            });
        }

        private static void AssertInside(Rectangle result, Rectangle workingArea)
        {
            Assert.True(result.Left >= workingArea.Left + TrayMenuPlacementService.DefaultMarginPhysicalPixels);
            Assert.True(result.Top >= workingArea.Top + TrayMenuPlacementService.DefaultMarginPhysicalPixels);
            Assert.True(result.Right <= workingArea.Right - TrayMenuPlacementService.DefaultMarginPhysicalPixels);
            Assert.True(result.Bottom <= workingArea.Bottom - TrayMenuPlacementService.DefaultMarginPhysicalPixels);
        }
    }
}
