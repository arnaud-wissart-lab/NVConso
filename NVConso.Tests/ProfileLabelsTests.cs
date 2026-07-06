namespace NVConso.Tests
{
    public class ProfileLabelsTests
    {
        [Theory]
        [InlineData(GpuPowerMode.Canicule, "Canicule")]
        [InlineData(GpuPowerMode.VideoSurf, "Vidéo / surf")]
        [InlineData(GpuPowerMode.Indie2D, "Indie 2D")]
        [InlineData(GpuPowerMode.Stock, "Stock")]
        [InlineData(GpuPowerMode.Max, "Max")]
        [InlineData(GpuPowerMode.Custom, "Personnalisé")]
        [InlineData((GpuPowerMode)999, "Stock")]
        public void GetDisplayName_ShouldReturnExpectedLabel(GpuPowerMode mode, string expected)
        {
            Assert.Equal(expected, ProfileLabels.GetDisplayName(mode));
        }
    }
}
