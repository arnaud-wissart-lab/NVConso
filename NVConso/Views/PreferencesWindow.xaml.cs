using Microsoft.Win32;
using NVConso.ViewModels;
using System.Windows;

namespace NVConso.Views
{
    public partial class PreferencesWindow : Window
    {
        private readonly PreferencesViewModel _viewModel;

        public PreferencesWindow(PreferencesViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = _viewModel;
            Icon = WpfIconLoader.LoadWindowIcon();
            ApplyTheme(_viewModel.ResolvedTheme);
            _viewModel.ThemeChanged += OnThemeChanged;
        }

        public PreferencesViewModel ViewModel => _viewModel;

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.ThemeChanged -= OnThemeChanged;
            base.OnClosed(e);
        }

        private async void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (await _viewModel.SaveAsync(closeAfterSave: true).ConfigureAwait(true))
                Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void ExportTelemetry_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = $"Exporter la session de télémétrie {ProductNames.DisplayName}",
                Filter = "Archive ZIP (*.zip)|*.zip",
                FileName = $"{ProductNames.DisplayName}-telemetry-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip"
            };

            if (dialog.ShowDialog(this) == true)
                await _viewModel.ExportTelemetrySessionAsync(dialog.FileName).ConfigureAwait(true);
        }

        private async void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = $"Exporter un diagnostic {ProductNames.DisplayName}",
                Filter = "Fichier texte (*.txt)|*.txt",
                FileName = $"{ProductNames.DisplayName}-diagnostic-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt"
            };

            if (dialog.ShowDialog(this) == true)
                await _viewModel.ExportDiagnosticsAsync(dialog.FileName).ConfigureAwait(true);
        }

        private void OnThemeChanged(object sender, UiTheme theme)
        {
            ApplyTheme(theme);
        }

        private void ApplyTheme(UiTheme theme)
        {
            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/WattPilot;component/Themes/{(theme == UiTheme.Dark ? "DarkTheme" : "LightTheme")}.xaml", UriKind.Relative)
            });
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/WattPilot;component/Themes/CommonStyles.xaml", UriKind.Relative)
            });
        }
    }
}
