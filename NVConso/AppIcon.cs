namespace NVConso
{
    public static class AppIcon
    {
        public static Icon Load()
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "WattPilot.ico");
            return File.Exists(iconPath)
                ? new Icon(iconPath)
                : SystemIcons.Application;
        }
    }
}
