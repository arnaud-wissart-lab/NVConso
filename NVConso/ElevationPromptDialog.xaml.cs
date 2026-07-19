using System.Windows;

namespace NVConso
{
    public partial class ElevationPromptDialog : Window
    {
        private readonly bool _sessionChoiceEnabled;

        public ElevationPromptDialog(ElevationReason reason)
            : this(reason, sessionChoiceEnabled: reason == ElevationReason.GpuPowerLimit)
        {
        }

        private ElevationPromptDialog(ElevationReason reason, bool sessionChoiceEnabled)
        {
            _sessionChoiceEnabled = sessionChoiceEnabled;
            InitializeComponent();
            Title = sessionChoiceEnabled
                ? PrivilegeMessages.GpuSessionAuthorizationTitle
                : PrivilegeMessages.AuthorizationTitle;
            MessageText.Text = sessionChoiceEnabled
                ? PrivilegeMessages.GpuSessionAuthorizationMessage
                : PrivilegeMessages.GetElevationPromptMessage(reason);
            DetailText.Text = sessionChoiceEnabled
                ? string.Empty
                : PrivilegeMessages.GetElevationPromptDetail(reason);
            DetailText.Visibility = sessionChoiceEnabled ? Visibility.Collapsed : Visibility.Visible;
            AuthorizeActionButton.Content = sessionChoiceEnabled
                ? PrivilegeMessages.AuthorizeForSessionButton
                : PrivilegeMessages.AuthorizeButton;
            OneTimeActionButton.Content = PrivilegeMessages.OneTimeAuthorizationButton;
            OneTimeActionButton.Visibility = sessionChoiceEnabled ? Visibility.Visible : Visibility.Collapsed;
            CancelActionButton.Content = PrivilegeMessages.CancelButton;
        }

        internal ElevationPromptChoice Choice { get; private set; } = ElevationPromptChoice.Cancel;

        public static bool Confirm(ElevationReason reason, Window owner = null)
        {
            var dialog = new ElevationPromptDialog(reason, sessionChoiceEnabled: false);
            if (owner is not null)
                dialog.Owner = owner;

            return dialog.ShowDialog() == true;
        }

        internal static ElevationPromptChoice Choose(ElevationReason reason, Window owner = null)
        {
            var dialog = new ElevationPromptDialog(reason, sessionChoiceEnabled: reason == ElevationReason.GpuPowerLimit);
            if (owner is not null)
                dialog.Owner = owner;

            return dialog.ShowDialog() == true
                ? dialog.Choice
                : ElevationPromptChoice.Cancel;
        }

        private void Authorize_Click(object sender, RoutedEventArgs e)
        {
            Choice = _sessionChoiceEnabled
                ? ElevationPromptChoice.Session
                : ElevationPromptChoice.OneTime;
            DialogResult = true;
        }

        private void OneTime_Click(object sender, RoutedEventArgs e)
        {
            Choice = ElevationPromptChoice.OneTime;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Choice = ElevationPromptChoice.Cancel;
            DialogResult = false;
        }
    }

    internal enum ElevationPromptChoice
    {
        Cancel = 0,
        OneTime = 1,
        Session = 2
    }
}
