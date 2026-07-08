using System.Windows;

namespace NVConso
{
    public partial class UpdatePromptDialog : Window
    {
        public UpdatePromptDialog()
        {
            InitializeComponent();
        }

        public static bool ConfirmInstall(Window owner = null)
        {
            var dialog = new UpdatePromptDialog();
            if (owner is not null)
                dialog.Owner = owner;

            return dialog.ShowDialog() == true;
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Later_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
