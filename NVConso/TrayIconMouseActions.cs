namespace NVConso
{
    public static class TrayIconMouseActions
    {
        public static TrayIconMouseAction FromMouseUp(MouseButtons button)
        {
            return button switch
            {
                MouseButtons.Left => TrayIconMouseAction.OpenDashboard,
                MouseButtons.Right => TrayIconMouseAction.ShowMenu,
                _ => TrayIconMouseAction.None
            };
        }

        public static TrayIconMouseAction FromMouseDoubleClick(MouseButtons button)
        {
            return button == MouseButtons.Left
                ? TrayIconMouseAction.OpenDashboard
                : TrayIconMouseAction.None;
        }
    }
}
