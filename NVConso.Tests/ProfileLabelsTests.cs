namespace NVConso.Tests
{
    public class ProfileLabelsTests
    {
        [Theory]
        [InlineData(GpuPowerMode.Canicule, "Canicule")]
        [InlineData(GpuPowerMode.VideoSurf, "Vidéo / surf")]
        [InlineData(GpuPowerMode.Indie2D, "Indie 2D")]
        [InlineData(GpuPowerMode.Stock, "Normal")]
        [InlineData(GpuPowerMode.Max, "Max")]
        [InlineData(GpuPowerMode.Custom, "Personnalisé")]
        [InlineData((GpuPowerMode)999, "Normal")]
        public void GetDisplayName_ShouldReturnExpectedLabel(GpuPowerMode mode, string expected)
        {
            Assert.Equal(expected, ProfileLabels.GetDisplayName(mode));
        }

        [Fact]
        public void GetDescription_ShouldExplainVideoSurfProfile()
        {
            Assert.Equal(
                "Vidéo / surf limite la puissance pour navigation et vidéo légère.",
                ProfileLabels.GetDescription(GpuPowerMode.VideoSurf));
        }

        [Fact]
        public void PublicLabels_ShouldNotExposeTechnicalStockOrCustomNames()
        {
            string[] labels =
            [
                ProfileLabels.GetDisplayName(GpuPowerMode.Stock),
                ProfileLabels.GetDisplayName(GpuPowerMode.Custom)
            ];

            Assert.DoesNotContain("Stock", labels);
            Assert.DoesNotContain("Custom", labels);
        }
    }
}
