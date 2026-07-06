using System.Windows.Forms;

namespace NVConso.Tests
{
    public class TrayMenuTests
    {
        [Fact]
        public void CompactMenu_ShouldNotExposeLegacyPermanentActions()
        {
            using TrayMenuView view = CreateMenuView();

            string[] topLevelTexts = view.Menu.Items
                .OfType<ToolStripMenuItem>()
                .Select(item => item.Text)
                .ToArray();

            Assert.Contains(ProductNames.DisplayName, topLevelTexts);
            Assert.Contains("Ouvrir le tableau de bord", topLevelTexts);
            Assert.Contains("Profils", topLevelTexts);
            Assert.Contains("Préférences...", topLevelTexts);
            Assert.Contains("Quitter", topLevelTexts);

            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Rechercher une mise à jour", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Télécharger la mise à jour", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Installer et redémarrer", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Mises à jour automatiques", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Toujours afficher", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Démarrer réduit", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Utilisation GPU", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Décodeur vidéo", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(topLevelTexts, text => text.Contains("Ventilateur", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void CompactMenu_ShouldKeepProfilesAndPreferencesAccessible()
        {
            using TrayMenuView view = CreateMenuView();

            string[] profileTexts = view.ProfilesMenuItem.DropDownItems
                .OfType<ToolStripMenuItem>()
                .Select(item => item.Text)
                .ToArray();

            Assert.Contains("Canicule", profileTexts);
            Assert.Contains("Vidéo / surf", profileTexts);
            Assert.Contains("Indie 2D", profileTexts);
            Assert.Contains("Stock", profileTexts);
            Assert.Contains("Max", profileTexts);
            Assert.Contains("Limite personnalisée...", profileTexts);
            Assert.Equal("Préférences...", view.PreferencesItem.Text);
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

        private static TrayMenuView CreateMenuView()
        {
            return TrayMenuBuilder.Create();
        }
    }
}
