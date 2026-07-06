namespace NVConso
{
    public static class DashboardCloseBehavior
    {
        public static bool ShouldHideInsteadOfClose(CloseReason closeReason)
        {
            return closeReason == CloseReason.UserClosing;
        }
    }
}
