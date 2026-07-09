using System.Drawing;
using System.Windows.Forms;
using System.Windows.Threading;

namespace NVConso.Tests
{
    public class TrayMenuTests
    {
        [Fact]
        public void CompactMenu_ShouldNotExposeLegacyPermanentActions()
        {
            TrayMenuViewModel view = CreateMenuView();

            string[] topLevelTexts = view.TopLevelItems
                .Select(item => item.Text)
                .ToArray();

            Assert.Contains("Ouvrir WattPilot", topLevelTexts);
            Assert.Contains("Modes GPU", topLevelTexts);
            Assert.Contains(UpdateLabels.FormatUpToDate(null), topLevelTexts);
            Assert.Contains("Quitter", topLevelTexts);

            Assert.Equal(UpdateLabels.UpToDateStatus, view.UpdateStatusItem.Text);
            Assert.Equal("Prêt", view.StatusItem.Text);
            Assert.DoesNotContain(ProductNames.DisplayName, topLevelTexts);
            Assert.DoesNotContain("À jour", topLevelTexts);
            Assert.DoesNotContain("Mise à jour", GetVisibleTrayMenuTexts(view));
            Assert.DoesNotContain(GetVisibleTrayMenuTexts(view), text => text.Contains("Mode lecture seule", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(GetVisibleTrayMenuTexts(view), text => text.Contains("une élévation sera demandée", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("Préférences...", topLevelTexts);
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Rechercher une mise à jour", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Télécharger la mise à jour", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Installer et redémarrer", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Mises à jour automatiques", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Toujours afficher", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Démarrer réduit", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Utilisation GPU", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Décodeur vidéo", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Ventilateur", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Affichage", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("HDR", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("VRR", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void TrayMenuWindow_ShouldNotExposeUpdateSectionTitle()
        {
            string xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "NVConso", "TrayMenuWindow.xaml"));

            Assert.DoesNotContain("Text=\"Mise à jour\"", xaml);
            Assert.Contains("UpdateStatusItem.Text", xaml);
            Assert.Contains("UpdateStatusItem.DetailText", xaml);
        }

        [Fact]
        public void CompactMenu_ShouldKeepProfilesAccessible()
        {
            TrayMenuViewModel view = CreateMenuView();

            string[] profileTexts = view.ProfileItems
                .Select(item => item.Text)
                .ToArray();

            Assert.Contains("Canicule", profileTexts);
            Assert.Contains("Vidéo / surf", profileTexts);
            Assert.Contains("Indie 2D", profileTexts);
            Assert.Contains("Normal", profileTexts);
            Assert.Contains("Max", profileTexts);
            Assert.Equal("Personnalisé...", view.CustomPowerLimitItem.Text);
            Assert.DoesNotContain("Normal / Stock", profileTexts);
        }

        [Fact]
        public void TrayIconMouseActions_ShouldMapLeftRightAndDoubleClick()
        {
            Assert.Equal(TrayIconMouseAction.OpenDashboard, TrayIconMouseActions.FromMouseUp(MouseButtons.Left));
            Assert.Equal(TrayIconMouseAction.ShowMenu, TrayIconMouseActions.FromMouseUp(MouseButtons.Right));
            Assert.Equal(TrayIconMouseAction.None, TrayIconMouseActions.FromMouseUp(MouseButtons.Middle));
            Assert.Equal(TrayIconMouseAction.OpenDashboard, TrayIconMouseActions.FromMouseDoubleClick(MouseButtons.Left));
            Assert.Equal(TrayIconMouseAction.None, TrayIconMouseActions.FromMouseDoubleClick(MouseButtons.Right));
        }

        [Fact]
        public void TrayMenuView_ShowAt_ShouldDelegateScreenPointToWindow()
        {
            TrayMenuViewModel viewModel = TrayMenuBuilder.CreateViewModel(out IReadOnlyDictionary<GpuPowerMode, TrayMenuActionItem> profileItems);
            var window = new FakeTrayMenuWindow();
            using var view = new TrayMenuView(viewModel, profileItems, window);
            var point = new Point(123, 456);

            view.ShowAt(point);

            Assert.Equal(point, window.LastShowAt);
        }

        private static TrayMenuViewModel CreateMenuView()
        {
            return TrayMenuBuilder.CreateViewModel(out _);
        }

        private static string[] GetVisibleTrayMenuTexts(TrayMenuViewModel view)
        {
            string[] texts =
            [
                view.StatusItem.Text,
                view.OpenDashboardItem.Text,
                view.ProfilesMenuItem.Text,
                .. view.ProfileItems.Select(item => item.Text),
                view.CustomPowerLimitItem.Text,
                view.InstalledVersionLabel,
                view.UpdateStatusItem.Text,
                view.UpdateStatusItem.DetailText,
                view.UpdateActionItem.Text,
                view.QuitItem.Text
            ];

            return texts
                .Select(text => text ?? string.Empty)
                .ToArray();
        }

        private static string FindRepositoryRoot()
        {
            string directory = AppContext.BaseDirectory;

            while (!string.IsNullOrWhiteSpace(directory))
            {
                if (File.Exists(Path.Combine(directory, "Tools.sln")))
                    return directory;

                directory = Directory.GetParent(directory)?.FullName;
            }

            throw new DirectoryNotFoundException("Racine du dépôt introuvable depuis le dossier de test.");
        }

        private sealed class FakeTrayMenuWindow : ITrayMenuWindow
        {
            public Dispatcher Dispatcher { get; } = Dispatcher.CurrentDispatcher;
            public Point? LastShowAt { get; private set; }
            public bool Hidden { get; private set; }
            public bool Closed { get; private set; }

            public void ShowAt(Point screenPoint)
            {
                LastShowAt = screenPoint;
            }

            public void Hide()
            {
                Hidden = true;
            }

            public void Close()
            {
                Closed = true;
            }
        }
    }
}
