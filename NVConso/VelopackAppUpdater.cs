using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace NVConso
{
    public sealed class VelopackAppUpdater : IAppUpdater
    {
        public const string StableChannel = "stable";
        public static string RepositoryUrl => ProductNames.RepositoryUrl;
        public static string TechnicalIdentityCompatibilityMessage =>
            $"{ProductNames.DisplayName} utilise encore l'identifiant technique {ProductNames.LegacyTechnicalName} pour préserver la compatibilité des mises à jour.";
        public static string NotInstalledMessage =>
            $"Application non installée via Velopack : l'auto-update complet nécessite l'installation {ProductNames.DisplayName}/{ProductNames.LegacyTechnicalName} via Velopack. {TechnicalIdentityCompatibilityMessage} Téléchargements : {ProductNames.LatestReleaseUrl}";

        private readonly ILogger<VelopackAppUpdater> _logger;
        private readonly Func<string, bool, UpdateManager> _updateManagerFactory;
        private UpdateInfo _lastUpdate;
        private string _lastChannel = StableChannel;
        private bool _lastIncludePrerelease;

        public VelopackAppUpdater(ILogger<VelopackAppUpdater> logger = null)
            : this(CreateUpdateManager, logger)
        {
        }

        public VelopackAppUpdater(
            Func<string, bool, UpdateManager> updateManagerFactory,
            ILogger<VelopackAppUpdater> logger = null)
        {
            _updateManagerFactory = updateManagerFactory;
            _logger = logger;
        }

        public async Task<AppUpdateOperationResult> CheckForUpdatesAsync(
            string channel,
            bool includePrerelease,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string resolvedChannel = NormalizeChannel(channel);
                UpdateManager manager = CreateManager(resolvedChannel, includePrerelease);
                _lastChannel = resolvedChannel;
                _lastIncludePrerelease = includePrerelease;
                AppUpdateOperationResult installedCheck = EnsureInstalled(manager);
                if (installedCheck is not null)
                    return installedCheck;

                _lastUpdate = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
                if (_lastUpdate is null)
                    return AppUpdateOperationResult.Succeeded(AppUpdateStatus.NoUpdate, $"{ProductNames.DisplayName} est à jour.");

                return AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.UpdateAvailable,
                    $"Mise à jour disponible : {FormatVersion(_lastUpdate.TargetFullRelease)}",
                    ToAppUpdateInfo(_lastUpdate));
            }
            catch (Exception exception)
            {
                return HandleException(exception, "vérification de mise à jour");
            }
        }

        public async Task<AppUpdateOperationResult> DownloadUpdateAsync(
            string channel,
            bool includePrerelease,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string resolvedChannel = NormalizeChannel(channel);
                UpdateManager manager = CreateManager(resolvedChannel, includePrerelease);
                _lastChannel = resolvedChannel;
                _lastIncludePrerelease = includePrerelease;
                AppUpdateOperationResult installedCheck = EnsureInstalled(manager);
                if (installedCheck is not null)
                    return installedCheck;

                UpdateInfo update = IsCachedUpdateFor(resolvedChannel, includePrerelease)
                    ? _lastUpdate
                    : await manager.CheckForUpdatesAsync().ConfigureAwait(false);

                if (update is null)
                    return AppUpdateOperationResult.Succeeded(AppUpdateStatus.NoUpdate, "Aucune mise à jour à télécharger.");

                await manager
                    .DownloadUpdatesAsync(update, value => progress?.Report(value), cancellationToken)
                    .ConfigureAwait(false);

                _lastUpdate = update;
                return AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.Downloaded,
                    $"Mise à jour téléchargée : {FormatVersion(update.TargetFullRelease)}",
                    ToAppUpdateInfo(update));
            }
            catch (Exception exception)
            {
                return HandleException(exception, "téléchargement de mise à jour");
            }
        }

        public Task<AppUpdateOperationResult> ApplyUpdateAndRestartAsync(
            string channel,
            bool includePrerelease,
            string[] restartArgs = null)
        {
            try
            {
                string resolvedChannel = NormalizeChannel(channel);
                UpdateManager manager = CreateManager(resolvedChannel, includePrerelease);
                AppUpdateOperationResult installedCheck = EnsureInstalled(manager);
                if (installedCheck is not null)
                    return Task.FromResult(installedCheck);

                VelopackAsset pendingUpdate = manager.UpdatePendingRestart;
                UpdateInfo cachedUpdate = IsCachedUpdateFor(resolvedChannel, includePrerelease)
                    ? _lastUpdate
                    : null;

                if (pendingUpdate is null && cachedUpdate is null)
                {
                    return Task.FromResult(AppUpdateOperationResult.Failed(
                        AppUpdateStatus.Failed,
                        "Aucune mise à jour téléchargée n'est prête à installer."));
                }

                VelopackAsset updateToApply = pendingUpdate ?? cachedUpdate.TargetFullRelease;
                manager.ApplyUpdatesAndRestart(updateToApply, restartArgs);

                return Task.FromResult(AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.PendingRestart,
                    "Installation de la mise à jour lancée."));
            }
            catch (Exception exception)
            {
                return Task.FromResult(HandleException(exception, "application de mise à jour"));
            }
        }

        public PendingUpdateStatus GetPendingUpdateStatus(string channel, bool includePrerelease)
        {
            try
            {
                UpdateManager manager = CreateManager(channel, includePrerelease);
                if (!manager.IsInstalled || manager.IsPortable)
                    return PendingUpdateStatus.None(NotInstalledMessage);

                VelopackAsset pendingUpdate = manager.UpdatePendingRestart;
                return pendingUpdate is null
                    ? PendingUpdateStatus.None()
                    : PendingUpdateStatus.Pending(FormatVersion(pendingUpdate), pendingUpdate.FileName);
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Impossible de lire le statut de mise à jour Velopack.");
                return PendingUpdateStatus.None($"Statut de mise à jour indisponible : {exception.Message}");
            }
        }

        private static UpdateManager CreateUpdateManager(string channel, bool includePrerelease)
        {
            string resolvedChannel = NormalizeChannel(channel);
            var source = new GithubSource(
                RepositoryUrl,
                accessToken: null,
                prerelease: includePrerelease);

            return new UpdateManager(
                source,
                new UpdateOptions
                {
                    ExplicitChannel = resolvedChannel
                });
        }

        private UpdateManager CreateManager(string channel, bool includePrerelease)
        {
            return _updateManagerFactory(NormalizeChannel(channel), includePrerelease);
        }

        private bool IsCachedUpdateFor(string channel, bool includePrerelease)
        {
            return _lastUpdate is not null
                && string.Equals(_lastChannel, NormalizeChannel(channel), StringComparison.OrdinalIgnoreCase)
                && _lastIncludePrerelease == includePrerelease;
        }

        private static AppUpdateOperationResult EnsureInstalled(UpdateManager manager)
        {
            if (manager.IsInstalled && !manager.IsPortable)
                return null;

            return AppUpdateOperationResult.Failed(
                AppUpdateStatus.NotInstalled,
                NotInstalledMessage);
        }

        private static string NormalizeChannel(string channel)
        {
            return string.IsNullOrWhiteSpace(channel)
                ? StableChannel
                : channel.Trim();
        }

        private AppUpdateOperationResult HandleException(Exception exception, string operation)
        {
            switch (exception)
            {
                case NotInstalledException:
                    _logger?.LogWarning(exception, "Velopack indisponible : application non installée.");
                    return AppUpdateOperationResult.Failed(
                        AppUpdateStatus.NotInstalled,
                        NotInstalledMessage);

                case ChecksumFailedException:
                    _logger?.LogError(exception, "Checksum Velopack invalide pendant {Operation}.", operation);
                    return AppUpdateOperationResult.Failed(
                        AppUpdateStatus.ChecksumFailed,
                        "Le checksum du paquet de mise à jour est invalide. La mise à jour a été refusée.");

                case AcquireLockFailedException:
                    _logger?.LogWarning(exception, "Un verrou Velopack existe déjà pendant {Operation}.", operation);
                    return AppUpdateOperationResult.Failed(
                        AppUpdateStatus.UpdateInProgress,
                        "Une autre opération de mise à jour est déjà en cours.");

                case HttpRequestException:
                case IOException:
                    _logger?.LogWarning(exception, "Réseau indisponible pendant {Operation}.", operation);
                    return AppUpdateOperationResult.Failed(
                        AppUpdateStatus.NetworkUnavailable,
                        $"Réseau indisponible pendant la {operation}.");

                default:
                    _logger?.LogWarning(exception, "Échec Velopack pendant {Operation}.", operation);
                    return AppUpdateOperationResult.Failed(
                        AppUpdateStatus.Failed,
                        $"Échec de {operation} : {exception.Message}");
            }
        }

        private static AppUpdateInfo ToAppUpdateInfo(UpdateInfo update)
        {
            VelopackAsset target = update.TargetFullRelease;
            return new AppUpdateInfo(
                FormatVersion(target),
                target.NotesMarkdown,
                update.IsDowngrade,
                target.FileName);
        }

        private static string FormatVersion(VelopackAsset asset)
        {
            return asset?.Version?.ToString() ?? string.Empty;
        }
    }
}
