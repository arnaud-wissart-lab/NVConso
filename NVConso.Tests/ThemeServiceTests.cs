namespace NVConso.Tests
{
    public class ThemeServiceTests
    {
        [Theory]
        [InlineData(UiTheme.Light, false)]
        [InlineData(UiTheme.Dark, true)]
        public void GetPalette_ShouldReturnExpectedPalette_ForConcreteTheme(UiTheme theme, bool expectedDark)
        {
            var service = new ThemeService();

            ThemePalette palette = service.GetPalette(theme);
            ThemePalette expectedPalette = expectedDark
                ? ThemePalette.Dark()
                : ThemePalette.Light();

            Assert.Equal(expectedDark, palette.IsDark);
            Assert.Equal(expectedPalette.Background, palette.Background);
            Assert.NotEqual(palette.Background, palette.Surface);
        }

        [Fact]
        public void ResolveTheme_ShouldReturnConcreteTheme_ForSystemTheme()
        {
            var service = new ThemeService();

            UiTheme resolvedTheme = service.ResolveTheme(UiTheme.System);

            Assert.True(resolvedTheme is UiTheme.Light or UiTheme.Dark);
        }
    }
}
