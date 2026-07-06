using Microsoft.Win32;

namespace NVConso
{
    public sealed class ThemeService
    {
        private readonly Func<bool> _isSystemDarkTheme;

        public ThemeService()
            : this(IsWindowsAppThemeDark)
        {
        }

        internal ThemeService(Func<bool> isSystemDarkTheme)
        {
            _isSystemDarkTheme = isSystemDarkTheme ?? IsWindowsAppThemeDark;
        }

        public ThemePalette GetPalette(UiTheme theme)
        {
            return ResolveTheme(theme) == UiTheme.Dark
                ? ThemePalette.Dark()
                : ThemePalette.Light();
        }

        public UiTheme ResolveTheme(UiTheme theme)
        {
            if (theme == UiTheme.Light || theme == UiTheme.Dark)
                return theme;

            return _isSystemDarkTheme()
                ? UiTheme.Dark
                : UiTheme.Light;
        }

        private static bool IsWindowsAppThemeDark()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

                object value = key?.GetValue("AppsUseLightTheme");
                return value is int intValue && intValue == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
