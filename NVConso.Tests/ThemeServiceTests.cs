namespace NVConso.Tests
{
    public class ThemeServiceTests
    {
        [Fact]
        public void GetPalette_ShouldReturnLightPalette_ForLightTheme()
        {
            var service = new ThemeService();

            ThemePalette palette = service.GetPalette(UiTheme.Light);

            Assert.False(palette.IsDark);
            Assert.Equal(ThemePalette.Light().Background, palette.Background);
            Assert.NotEqual(palette.Background, palette.Surface);
        }

        [Fact]
        public void GetPalette_ShouldReturnDarkPalette_ForDarkTheme()
        {
            var service = new ThemeService();

            ThemePalette palette = service.GetPalette(UiTheme.Dark);

            Assert.True(palette.IsDark);
            Assert.Equal(ThemePalette.Dark().Background, palette.Background);
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
