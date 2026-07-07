namespace NVConso.Tests
{
    public class ThemeServiceTests
    {
        [Theory]
        [InlineData(UiTheme.Light)]
        [InlineData(UiTheme.Dark)]
        public void ResolveTheme_ShouldReturnConcreteTheme_WhenThemeIsExplicit(UiTheme theme)
        {
            var service = new ThemeService();

            UiTheme resolvedTheme = service.ResolveTheme(theme);

            Assert.Equal(theme, resolvedTheme);
        }

        [Theory]
        [InlineData(true, UiTheme.Dark)]
        [InlineData(false, UiTheme.Light)]
        public void ResolveTheme_ShouldUseSystemTheme_WhenThemeIsSystem(bool isSystemDark, UiTheme expected)
        {
            var service = new ThemeService(() => isSystemDark);

            UiTheme resolvedTheme = service.ResolveTheme(UiTheme.System);

            Assert.Equal(expected, resolvedTheme);
        }
    }
}
