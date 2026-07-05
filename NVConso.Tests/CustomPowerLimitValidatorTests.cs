namespace NVConso.Tests
{
    public class CustomPowerLimitValidatorTests
    {
        private const uint MinimumPowerLimit = 150000;
        private const uint MaximumPowerLimit = 600000;

        [Theory]
        [InlineData(180, 180000u)]
        [InlineData(180.5, 180500u)]
        public void TryConvertWattsToMilliwatts_ShouldConvert_ValidWatts(decimal watts, uint expectedMilliwatts)
        {
            bool success = CustomPowerLimitValidator.TryConvertWattsToMilliwatts(
                watts,
                MinimumPowerLimit,
                MaximumPowerLimit,
                out uint targetMilliwatt,
                out string message);

            Assert.True(success);
            Assert.Equal(expectedMilliwatts, targetMilliwatt);
            Assert.Equal(string.Empty, message);
        }

        [Theory]
        [InlineData(149.9)]
        [InlineData(600.1)]
        public void TryConvertWattsToMilliwatts_ShouldReject_OutOfRangeValues(decimal watts)
        {
            bool success = CustomPowerLimitValidator.TryConvertWattsToMilliwatts(
                watts,
                MinimumPowerLimit,
                MaximumPowerLimit,
                out uint targetMilliwatt,
                out string message);

            Assert.False(success);
            Assert.Equal(0u, targetMilliwatt);
            Assert.Contains("comprise entre", message);
        }

        [Fact]
        public void TryConvertWattsToMilliwatts_ShouldReject_NegativeValues()
        {
            bool success = CustomPowerLimitValidator.TryConvertWattsToMilliwatts(
                -1,
                MinimumPowerLimit,
                MaximumPowerLimit,
                out uint targetMilliwatt,
                out string message);

            Assert.False(success);
            Assert.Equal(0u, targetMilliwatt);
            Assert.Contains("positive", message);
        }

        [Fact]
        public void TryParseWatts_ShouldReject_NonNumericText()
        {
            bool success = CustomPowerLimitValidator.TryParseWatts(
                "abc",
                MinimumPowerLimit,
                MaximumPowerLimit,
                out uint targetMilliwatt,
                out string message);

            Assert.False(success);
            Assert.Equal(0u, targetMilliwatt);
            Assert.Contains("nombre", message);
        }

        [Fact]
        public void TryValidateMilliwatts_ShouldAccept_InRangeLimit()
        {
            bool success = CustomPowerLimitValidator.TryValidateMilliwatts(
                450000,
                MinimumPowerLimit,
                MaximumPowerLimit,
                out string message);

            Assert.True(success);
            Assert.Equal(string.Empty, message);
        }
    }
}
