using System.Windows;

namespace NVConso
{
    public partial class ElevationPromptDialog : Window
    {
        public ElevationPromptDialog(ElevationReason reason)
        {
            InitializeComponent();
            Title = PrivilegeMessages.AuthorizationTitle;
            MessageText.Text = PrivilegeMessages.GetElevationPromptMessage(reason);
            DetailText.Text = PrivilegeMessages.GetElevationPromptDetail(reason);
            AuthorizeActionButton.Content = PrivilegeMessages.AuthorizeButton;
            CancelActionButton.Content = PrivilegeMessages.CancelButton;
        }

        public static bool Confirm(ElevationReason reason, Window owner = null)
        {
            var dialog = new ElevationPromptDialog(reason);
            if (owner is not null)
                dialog.Owner = owner;

            return dialog.ShowDialog() == true;
        }

        private void Authorize_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
