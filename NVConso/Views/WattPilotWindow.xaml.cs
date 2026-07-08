using Microsoft.Win32;
using NVConso.ViewModels;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NVConso.Views
{
    public partial class WattPilotWindow : Window
    {
        private readonly DashboardViewModel _dashboardViewModel;
        private readonly PreferencesViewModel _preferencesViewModel;
        private bool _allowClose;

        public WattPilotWindow(DashboardViewModel dashboardViewModel, PreferencesViewModel preferencesViewModel)
        {
            _dashboardViewModel = dashboardViewModel ?? throw new ArgumentNullException(nameof(dashboardViewModel));
            _preferencesViewModel = preferencesViewModel ?? throw new ArgumentNullException(nameof(preferencesViewModel));
            InitializeComponent();
            DataContext = _dashboardViewModel;
            SettingsPage.DataContext = _preferencesViewModel;
            Icon = WpfIconLoader.LoadWindowIcon();
            ApplyTheme(_dashboardViewModel.ResolvedTheme);
            ApplySavedBounds();
            _dashboardViewModel.ThemeChanged += OnThemeChanged;
            _preferencesViewModel.ThemeChanged += OnThemeChanged;
        }

        public DashboardViewModel DashboardViewModel => _dashboardViewModel;

        public void CloseForApplicationExit()
        {
            _allowClose = true;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveBounds();

            if (!_allowClose)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _dashboardViewModel.ThemeChanged -= OnThemeChanged;
            _preferencesViewModel.ThemeChanged -= OnThemeChanged;
            _dashboardViewModel.Dispose();
            base.OnClosed(e);
        }

        private async void ExportHistory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exporter l'historique filtré",
                Filter = "Fichier CSV (*.csv)|*.csv",
                FileName = $"{ProductNames.DisplayName}-historique-{_dashboardViewModel.HistoryDate:yyyy-MM-dd}.csv"
            };

            if (dialog.ShowDialog(this) == true)
            {
                await _dashboardViewModel.ExportFilteredHistoryAsync(dialog.FileName).ConfigureAwait(true);
                return;
            }

            _dashboardViewModel.MarkHistoryExportCancelled();
        }

        private void CopyHistorySummary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(_dashboardViewModel.BuildHistoryDiagnosticSummary());
                _dashboardViewModel.MarkHistoryDiagnosticCopied();
            }
            catch (Exception exception)
            {
                _dashboardViewModel.MarkHistoryDiagnosticCopyFailed(exception);
            }
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
            {
                await _preferencesViewModel.ExportTelemetrySessionAsync(dialog.FileName).ConfigureAwait(true);
                return;
            }

            _preferencesViewModel.MarkTelemetryExportCancelled();
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
            {
                await _preferencesViewModel.ExportDiagnosticsAsync(dialog.FileName).ConfigureAwait(true);
                return;
            }

            _preferencesViewModel.MarkDiagnosticsExportCancelled();
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = System.Windows.MessageBox.Show(
                this,
                "Réinitialiser les préférences locales de WattPilot ? La tâche planifiée Windows sera conservée.",
                "Confirmer la réinitialisation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes || !_preferencesViewModel.ResetDefaultsCommand.CanExecute(null))
                return;

            _preferencesViewModel.ResetDefaultsCommand.Execute(null);
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
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/WattPilot;component/Themes/WattPilotWindowStyles.xaml", UriKind.Relative)
            });
        }

        private void ApplySavedBounds()
        {
            DashboardWindowBounds bounds = _dashboardViewModel.SavedBounds;
            if (bounds?.IsUsable() != true)
                return;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = bounds.X;
            Top = bounds.Y;
            Width = bounds.Width;
            Height = bounds.Height;
        }

        private void SaveBounds()
        {
            if (WindowState == WindowState.Normal)
                _dashboardViewModel.SaveWindowBounds(Left, Top, Width, Height);
        }
    }

    internal static class WpfIconLoader
    {
        public static ImageSource LoadWindowIcon()
        {
            string iconPath = AppIcon.GetPhysicalIconPath();
            if (File.Exists(iconPath))
                return LoadFromUri(new Uri(iconPath, UriKind.Absolute));

            using Stream stream = AppIcon.TryOpenEmbeddedIconResource();
            if (stream is not null)
                return LoadFromStream(stream);

            using Icon icon = AppIcon.Load();
            BitmapSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }

        private static BitmapSource LoadFromUri(Uri uri)
        {
            BitmapSource source = BitmapFrame.Create(uri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            source.Freeze();
            return source;
        }

        private static BitmapSource LoadFromStream(Stream stream)
        {
            BitmapSource source = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            source.Freeze();
            return source;
        }
    }
}
