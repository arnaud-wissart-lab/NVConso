using System.Text;

namespace NVConso
{
    public static class UpdateDiagnosticBuilder
    {
        public static string Build(
            AppExecutionModeInfo executionMode,
            AppSettings settings,
            UpdateUiState state = null)
        {
            executionMode ??= AppExecutionModeInfo.Unknown();
            settings ??= new AppSettings();

            var builder = new StringBuilder();
            builder.AppendLine(ProductNames.DisplayName + " - Diagnostic mise à jour");
            builder.AppendLine(FormattableString.Invariant($"Mode : {executionMode.Mode}"));
            builder.AppendLine(FormattableString.Invariant($"Libellé : {executionMode.ModeLabel}"));
            builder.AppendLine(FormattableString.Invariant($"Version courante : {ProductNames.DisplayVersion}"));
            builder.AppendLine("PackId Velopack : " + ProductNames.VelopackPackId);
            builder.AppendLine(FormattableString.Invariant($"Canal : {settings.UpdateChannel ?? VelopackAppUpdater.StableChannel}"));
            builder.AppendLine(FormattableString.Invariant($"Préversions : {settings.IncludePrereleaseUpdates}"));
            builder.AppendLine(FormattableString.Invariant($"Vérification automatique : {settings.AutoCheckUpdates}"));
            builder.AppendLine(FormattableString.Invariant($"Téléchargement automatique : {settings.AutoDownloadUpdates}"));
            builder.AppendLine(FormattableString.Invariant($"Dernière vérification UTC : {settings.LastUpdateCheckUtc?.ToUniversalTime().ToString("O") ?? "--"}"));
            builder.AppendLine(FormattableString.Invariant($"Dernière erreur : {settings.LastUpdateError ?? "--"}"));
            builder.AppendLine(FormattableString.Invariant($"Statut UI : {state?.Message ?? "--"}"));
            builder.AppendLine(FormattableString.Invariant($"Détail UI : {state?.DetailMessage ?? "--"}"));
            builder.AppendLine(FormattableString.Invariant($"Exécutable : {executionMode.ExecutablePath ?? "--"}"));
            builder.AppendLine("GitHub Releases : " + ProductNames.LatestReleaseUrl);
            return builder.ToString();
        }
    }
}
