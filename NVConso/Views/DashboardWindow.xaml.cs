using Microsoft.Win32;
using NVConso.ViewModels;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NVConso.Views
{
    public partial class DashboardWindow : Window
    {
        private readonly DashboardViewModel _viewModel;
        private bool _allowClose;

        public DashboardWindow(DashboardViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = _viewModel;
            Icon = WpfIconLoader.LoadWindowIcon();
            ApplyTheme(_viewModel.ResolvedTheme);
            ApplySavedBounds();
            _viewModel.ThemeChanged += OnThemeChanged;
        }

        public DashboardViewModel ViewModel => _viewModel;

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
            _viewModel.ThemeChanged -= OnThemeChanged;
            _viewModel.Dispose();
            base.OnClosed(e);
        }

        private void DashboardTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, DashboardTabs) || DashboardTabs.SelectedIndex != 1)
                return;

            _ = _viewModel.EnsureHistoryLoadedAsync();
        }

        private async void ExportHistory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exporter l'historique filtré",
                Filter = "Fichier CSV (*.csv)|*.csv",
                FileName = $"{ProductNames.DisplayName}-historique-{_viewModel.HistoryDate:yyyy-MM-dd}.csv"
            };

            if (dialog.ShowDialog(this) == true)
                await _viewModel.ExportFilteredHistoryAsync(dialog.FileName).ConfigureAwait(true);
        }

        private void CopyHistorySummary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(_viewModel.BuildHistoryDiagnosticSummary());
                _viewModel.MarkHistoryDiagnosticCopied();
            }
            catch (Exception exception)
            {
                _viewModel.MarkHistoryDiagnosticCopyFailed(exception);
            }
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

        private void ApplySavedBounds()
        {
            DashboardWindowBounds bounds = _viewModel.SavedBounds;
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
                _viewModel.SaveWindowBounds(Left, Top, Width, Height);
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
