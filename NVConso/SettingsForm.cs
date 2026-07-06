using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace NVConso
{
    public sealed class SettingsForm : Form
    {
        private const string LatestReleaseUrl = "https://github.com/arnaud-wissart-lab/NVConso/releases/latest";

        private readonly AppSettingsService _settingsService;
        private readonly IStartupManager _startupManager;
        private readonly AppUpdateWorkflow _updateWorkflow;
        private readonly INvmlManager _nvml;
        private readonly IGpuTelemetryService _telemetryService;

        private readonly CheckBox _startMinimizedCheck = new() { Text = "Démarrer réduit dans la zone de notification", AutoSize = true };
        private readonly CheckBox _restoreStockOnExitCheck = new() { Text = "Restaurer Stock à la fermeture", AutoSize = true };
        private readonly CheckBox _showDashboardOnStartupCheck = new() { Text = "Afficher le tableau de bord au démarrage", AutoSize = true };
        private readonly ComboBox _generalThemeCombo = CreateDropDown();

        private readonly CheckBox _autoApplySavedModeCheck = new() { Text = "Appliquer automatiquement le profil sauvegardé au démarrage", AutoSize = true };
        private readonly ComboBox _startupProfileCombo = CreateDropDown();
        private readonly NumericUpDown _customPowerLimitWattsInput = CreateNumeric(1, 1000, 150);
        private readonly Label _gpuRangeLabel = CreateValueLabel("--");

        private readonly CheckBox _caniculeGuardEnabledCheck = new() { Text = "Activer Canicule Guard", AutoSize = true };
        private readonly NumericUpDown _caniculePowerThresholdInput = CreateNumeric(
            AppSettingsValidator.MinimumCaniculePowerThresholdWatts,
            AppSettingsValidator.MaximumCaniculePowerThresholdWatts,
            CaniculeGuardDefaults.PowerThresholdWatts);
        private readonly NumericUpDown _caniculeTemperatureThresholdInput = CreateNumeric(
            AppSettingsValidator.MinimumCaniculeTemperatureThresholdCelsius,
            AppSettingsValidator.MaximumCaniculeTemperatureThresholdCelsius,
            CaniculeGuardDefaults.TemperatureThresholdCelsius);
        private readonly NumericUpDown _caniculeAlertDelayInput = CreateNumeric(
            AppSettingsValidator.MinimumCaniculeAlertDelaySeconds,
            AppSettingsValidator.MaximumCaniculeAlertDelaySeconds,
            CaniculeGuardDefaults.AlertDelaySeconds);
        private readonly NumericUpDown _caniculeCooldownInput = CreateNumeric(
            AppSettingsValidator.MinimumCaniculeCooldownSeconds,
            AppSettingsValidator.MaximumCaniculeCooldownSeconds,
            CaniculeGuardDefaults.CooldownSeconds);

        private readonly CheckBox _startWithWindowsCheck = new() { Text = "Démarrer avec Windows", AutoSize = true };
        private readonly Label _startupStatusLabel = CreateValueLabel("--");

        private readonly CheckBox _autoCheckUpdatesCheck = new() { Text = "Vérifier automatiquement les mises à jour", AutoSize = true };
        private readonly CheckBox _autoDownloadUpdatesCheck = new() { Text = "Télécharger automatiquement après détection", AutoSize = true };
        private readonly CheckBox _includePrereleaseUpdatesCheck = new() { Text = "Inclure les prereleases", AutoSize = true };
        private readonly Label _lastUpdateCheckLabel = CreateValueLabel("--");
        private readonly Label _lastUpdateStatusLabel = CreateValueLabel("--");
        private readonly LinkLabel _latestReleaseLink = new() { Text = LatestReleaseUrl, AutoSize = true };

        private readonly ComboBox _appearanceThemeCombo = CreateDropDown();
        private readonly NumericUpDown _telemetryHistoryInput = CreateNumeric(
            GpuTelemetryHistory.MinimumCapacitySeconds,
            GpuTelemetryHistory.MaximumCapacitySeconds,
            GpuTelemetryHistory.DefaultCapacitySeconds);

        private readonly TextBox _settingsPathTextBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
        private readonly Label _statusLabel = new() { AutoSize = false, Height = 34, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

        private bool _syncingTheme;

        public SettingsForm(
            AppSettingsService settingsService,
            IStartupManager startupManager,
            AppUpdateWorkflow updateWorkflow,
            INvmlManager nvml,
            IGpuTelemetryService telemetryService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
            _updateWorkflow = updateWorkflow ?? throw new ArgumentNullException(nameof(updateWorkflow));
            _nvml = nvml ?? throw new ArgumentNullException(nameof(nvml));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));

            Text = "NVConso - Préférences";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(780, 560);
            Size = new Size(860, 640);
            Font = DashboardFonts.Body();
            Icon = AppIcon.Load();

            InitializeOptions();
            BuildLayout();
            LoadFromSettings(_settingsService.Current);
            RefreshStartupStatus();
            RefreshUpdateStatus();
        }

        private void InitializeOptions()
        {
            AddThemeOptions(_generalThemeCombo);
            AddThemeOptions(_appearanceThemeCombo);
            _generalThemeCombo.SelectedIndexChanged += (_, _) => SyncThemeSelection(_generalThemeCombo, _appearanceThemeCombo);
            _appearanceThemeCombo.SelectedIndexChanged += (_, _) => SyncThemeSelection(_appearanceThemeCombo, _generalThemeCombo);

            foreach (GpuPowerMode mode in new[]
            {
                GpuPowerMode.Canicule,
                GpuPowerMode.VideoSurf,
                GpuPowerMode.Indie2D,
                GpuPowerMode.Stock,
                GpuPowerMode.Max,
                GpuPowerMode.Custom
            })
            {
                _startupProfileCombo.Items.Add(new ComboOption<GpuPowerMode>(ProfileLabels.GetDisplayName(mode), mode));
            }

            _startupProfileCombo.SelectedIndexChanged += (_, _) => UpdateCustomPowerLimitEnabledState();
            _latestReleaseLink.LinkClicked += (_, _) => OpenExternal(LatestReleaseUrl);
            _settingsPathTextBox.Text = _settingsService.SettingsPath;
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(UiSpacing.Large)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill
            };
            tabs.TabPages.Add(CreateGeneralTab());
            tabs.TabPages.Add(CreateProfilesTab());
            tabs.TabPages.Add(CreateCaniculeGuardTab());
            tabs.TabPages.Add(CreateStartupTab());
            tabs.TabPages.Add(CreateUpdatesTab());
            tabs.TabPages.Add(CreateAppearanceTab());
            tabs.TabPages.Add(CreateAdvancedTab());
            root.Controls.Add(tabs, 0, 0);

            _statusLabel.ForeColor = Color.FromArgb(95, 109, 126);
            root.Controls.Add(_statusLabel, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            root.Controls.Add(buttons, 0, 2);

            Button cancelButton = CreateButton("Annuler");
            cancelButton.Click += (_, _) => Close();
            buttons.Controls.Add(cancelButton);

            Button saveButton = CreateButton("Enregistrer");
            saveButton.Click += (_, _) => SavePreferences(closeAfterSave: false);
            buttons.Controls.Add(saveButton);

            Button okButton = CreateButton("OK");
            okButton.Click += (_, _) => SavePreferences(closeAfterSave: true);
            buttons.Controls.Add(okButton);
        }

        private TabPage CreateGeneralTab()
        {
            TableLayoutPanel panel = CreateTabPanel();
            AddCheckBox(panel, _startMinimizedCheck);
            AddCheckBox(panel, _restoreStockOnExitCheck);
            AddCheckBox(panel, _showDashboardOnStartupCheck);
            AddRow(panel, "Thème", _generalThemeCombo);
            AddFill(panel);
            return CreateTab("Général", panel);
        }

        private TabPage CreateProfilesTab()
        {
            TableLayoutPanel panel = CreateTabPanel();
            AddCheckBox(panel, _autoApplySavedModeCheck);
            AddRow(panel, "Profil appliqué au démarrage", _startupProfileCombo);
            AddRow(panel, "Limite custom sauvegardée (W)", _customPowerLimitWattsInput);
            AddRow(panel, "Plage GPU actif", _gpuRangeLabel);
            AddFill(panel);
            return CreateTab("GPU / Profils", panel);
        }

        private TabPage CreateCaniculeGuardTab()
        {
            TableLayoutPanel panel = CreateTabPanel();
            AddCheckBox(panel, _caniculeGuardEnabledCheck);
            AddRow(panel, "Seuil puissance (W)", _caniculePowerThresholdInput);
            AddRow(panel, "Seuil température (°C)", _caniculeTemperatureThresholdInput);
            AddRow(panel, "Durée avant alerte (s)", _caniculeAlertDelayInput);
            AddRow(panel, "Cooldown (s)", _caniculeCooldownInput);

            Button resetButton = CreateButton("Réinitialiser valeurs par défaut");
            resetButton.Click += (_, _) => ResetCaniculeGuardDefaults();
            AddFullRow(panel, resetButton);
            AddFill(panel);
            return CreateTab("Canicule Guard", panel);
        }

        private TabPage CreateStartupTab()
        {
            TableLayoutPanel panel = CreateTabPanel();
            AddCheckBox(panel, _startWithWindowsCheck);
            AddRow(panel, "Statut tâche planifiée", _startupStatusLabel);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            Button repairButton = CreateButton("Réparer tâche");
            repairButton.Click += (_, _) => RepairStartupTask();
            buttons.Controls.Add(repairButton);

            Button deleteButton = CreateButton("Supprimer tâche");
            deleteButton.Click += (_, _) => DeleteStartupTask();
            buttons.Controls.Add(deleteButton);

            AddFullRow(panel, buttons);
            AddFill(panel);
            return CreateTab("Démarrage Windows", panel);
        }

        private TabPage CreateUpdatesTab()
        {
            TableLayoutPanel panel = CreateTabPanel();
            AddCheckBox(panel, _autoCheckUpdatesCheck);
            AddCheckBox(panel, _autoDownloadUpdatesCheck);
            AddCheckBox(panel, _includePrereleaseUpdatesCheck);
            AddRow(panel, "Dernière vérification", _lastUpdateCheckLabel);
            AddRow(panel, "Dernier état", _lastUpdateStatusLabel);
            AddRow(panel, "Release GitHub", _latestReleaseLink);

            Button checkButton = CreateButton("Vérifier maintenant");
            checkButton.Click += async (_, _) => await CheckForUpdatesNowAsync(checkButton);
            AddFullRow(panel, checkButton);
            AddFill(panel);
            return CreateTab("Mises à jour", panel);
        }

        private TabPage CreateAppearanceTab()
        {
            TableLayoutPanel panel = CreateTabPanel();
            AddRow(panel, "Thème", _appearanceThemeCombo);
            AddRow(panel, "Durée historique graphique (s)", _telemetryHistoryInput);
            AddFullRow(panel, CreateMutedLabel("Les graphes restent locaux et ne sont pas persistés par défaut."));
            AddFill(panel);
            return CreateTab("Apparence", panel);
        }

        private TabPage CreateAdvancedTab()
        {
            TableLayoutPanel panel = CreateTabPanel();
            AddRow(panel, "Chemin settings.json", _settingsPathTextBox);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            Button openFolderButton = CreateButton("Ouvrir dossier settings");
            openFolderButton.Click += (_, _) => OpenSettingsFolder();
            buttons.Controls.Add(openFolderButton);

            Button exportButton = CreateButton("Exporter diagnostic texte");
            exportButton.Click += (_, _) => ExportDiagnostic();
            buttons.Controls.Add(exportButton);

            Button resetButton = CreateButton("Réinitialiser settings");
            resetButton.Click += (_, _) => ResetSettings();
            buttons.Controls.Add(resetButton);

            AddFullRow(panel, buttons);
            AddFill(panel);
            return CreateTab("Avancé", panel);
        }

        private void LoadFromSettings(AppSettings settings)
        {
            _startMinimizedCheck.Checked = settings.StartMinimized;
            _restoreStockOnExitCheck.Checked = settings.RestoreStockOnExit;
            _showDashboardOnStartupCheck.Checked = settings.ShowDashboardOnStartup;
            SelectComboValue(_generalThemeCombo, settings.DashboardTheme);
            SelectComboValue(_appearanceThemeCombo, settings.DashboardTheme);

            _autoApplySavedModeCheck.Checked = settings.AutoApplySavedMode;
            SelectComboValue(_startupProfileCombo, settings.LastSelectedMode);
            _customPowerLimitWattsInput.Value = ToNumericValue(settings.CustomPowerLimitMilliwatt, _customPowerLimitWattsInput);
            _gpuRangeLabel.Text = ResolveGpuRangeText();
            UpdateCustomPowerLimitEnabledState();

            _caniculeGuardEnabledCheck.Checked = settings.CaniculeGuardEnabled;
            _caniculePowerThresholdInput.Value = settings.CaniculeGuardPowerThresholdWatts;
            _caniculeTemperatureThresholdInput.Value = settings.CaniculeGuardTemperatureThresholdCelsius;
            _caniculeAlertDelayInput.Value = settings.CaniculeGuardAlertDelaySeconds;
            _caniculeCooldownInput.Value = settings.CaniculeGuardCooldownSeconds;

            _startWithWindowsCheck.Checked = settings.StartWithWindows;
            _autoCheckUpdatesCheck.Checked = settings.AutoCheckUpdates;
            _autoDownloadUpdatesCheck.Checked = settings.AutoDownloadUpdates;
            _includePrereleaseUpdatesCheck.Checked = settings.IncludePrereleaseUpdates;
            _telemetryHistoryInput.Value = settings.TelemetryHistorySeconds;
            RefreshUpdateStatus();
        }

        private bool TryCollectSettings(out AppSettings settings, out string message)
        {
            settings = _settingsService.CreateEditableCopy();
            message = string.Empty;

            settings.StartMinimized = _startMinimizedCheck.Checked;
            settings.RestoreStockOnExit = _restoreStockOnExitCheck.Checked;
            settings.ShowDashboardOnStartup = _showDashboardOnStartupCheck.Checked;
            settings.DashboardTheme = GetSelectedValue<UiTheme>(_appearanceThemeCombo);
            settings.AutoApplySavedMode = _autoApplySavedModeCheck.Checked;
            settings.LastSelectedMode = GetSelectedValue<GpuPowerMode>(_startupProfileCombo);
            settings.HasSavedMode = settings.AutoApplySavedMode;
            settings.StartWithWindows = _startWithWindowsCheck.Checked;
            settings.AutoCheckUpdates = _autoCheckUpdatesCheck.Checked;
            settings.AutoDownloadUpdates = _autoDownloadUpdatesCheck.Checked;
            settings.IncludePrereleaseUpdates = _includePrereleaseUpdatesCheck.Checked;
            settings.TelemetryHistorySeconds = (int)_telemetryHistoryInput.Value;
            settings.CaniculeGuardEnabled = _caniculeGuardEnabledCheck.Checked;
            settings.CaniculeGuardPowerThresholdWatts = (int)_caniculePowerThresholdInput.Value;
            settings.CaniculeGuardTemperatureThresholdCelsius = (int)_caniculeTemperatureThresholdInput.Value;
            settings.CaniculeGuardAlertDelaySeconds = (int)_caniculeAlertDelayInput.Value;
            settings.CaniculeGuardCooldownSeconds = (int)_caniculeCooldownInput.Value;

            if (settings.LastSelectedMode == GpuPowerMode.Custom)
            {
                if (!TryResolveCustomPowerLimit(out uint customPowerLimitMilliwatt, out message))
                    return false;

                settings.CustomPowerLimitMilliwatt = customPowerLimitMilliwatt;
            }

            AppSettingsValidationResult validation = AppSettingsValidator.Validate(settings);
            if (!validation.IsValid)
            {
                message = validation.Message;
                return false;
            }

            return true;
        }

        private void SavePreferences(bool closeAfterSave)
        {
            if (!TryCollectSettings(out AppSettings settings, out string message))
            {
                ShowError(message);
                return;
            }

            if (!TryApplyStartupPreference(settings, out message))
            {
                ShowError(message);
                RefreshStartupStatus();
                return;
            }

            if (!_settingsService.TrySave(settings, out message))
            {
                ShowError(message);
                return;
            }

            _telemetryService.SetHistoryCapacitySeconds(settings.TelemetryHistorySeconds);
            LoadFromSettings(_settingsService.Current);
            RefreshStartupStatus();
            ShowInfo(message);

            if (closeAfterSave)
                Close();
        }

        private bool TryApplyStartupPreference(AppSettings settings, out string message)
        {
            StartupTaskStatus status = _startupManager.GetStatus();

            if (settings.StartWithWindows)
            {
                StartupOperationResult result = _startupManager.Enable(settings.StartMinimized);
                settings.StartWithWindows = result.Status?.IsEnabledForCurrentExecutable == true;
                message = result.Message;
                return result.Success;
            }

            if (!status.IsAvailable || !status.Exists)
            {
                settings.StartWithWindows = false;
                message = status.Message;
                return true;
            }

            StartupOperationResult disableResult = _startupManager.Disable();
            settings.StartWithWindows = false;
            message = disableResult.Message;
            return disableResult.Success;
        }

        private void RepairStartupTask()
        {
            StartupOperationResult result = _startupManager.Enable(_startMinimizedCheck.Checked);
            if (!result.Success)
            {
                ShowError(result.Message);
                RefreshStartupStatus();
                return;
            }

            _startWithWindowsCheck.Checked = true;
            if (!PersistStartupState(startWithWindows: true))
                return;

            ShowInfo(result.Message);
            RefreshStartupStatus();
        }

        private void DeleteStartupTask()
        {
            StartupOperationResult result = _startupManager.Disable();
            if (!result.Success)
            {
                ShowError(result.Message);
                RefreshStartupStatus();
                return;
            }

            _startWithWindowsCheck.Checked = false;
            if (!PersistStartupState(startWithWindows: false))
                return;

            ShowInfo(result.Message);
            RefreshStartupStatus();
        }

        private bool PersistStartupState(bool startWithWindows)
        {
            AppSettings settings = _settingsService.CreateEditableCopy();
            settings.StartWithWindows = startWithWindows;
            settings.StartMinimized = _startMinimizedCheck.Checked;

            if (!_settingsService.TrySave(settings, out string message))
            {
                ShowError(message);
                return false;
            }

            return true;
        }

        private async Task CheckForUpdatesNowAsync(Button button)
        {
            button.Enabled = false;
            ShowInfo("Vérification des mises à jour en cours...");

            try
            {
                AppSettings settings = _settingsService.CreateEditableCopy();
                AppUpdateOperationResult result = await _updateWorkflow.CheckForUpdatesAsync(settings);
                _settingsService.TrySave(settings, out _);
                RefreshUpdateStatus();
                _lastUpdateStatusLabel.Text = result.Message;

                if (result.HasUpdate)
                    ShowInfo($"Nouvelle version disponible : {result.Update.Version}");
                else
                    ShowInfo(result.Message);
            }
            catch (Exception exception)
            {
                ShowError($"Vérification impossible : {exception.Message}");
            }
            finally
            {
                button.Enabled = true;
            }
        }

        private void RefreshStartupStatus()
        {
            StartupTaskStatus status = _startupManager.GetStatus();
            _startupStatusLabel.Text = status.Message;
            _startWithWindowsCheck.Checked = status.IsAvailable && status.IsEnabledForCurrentExecutable;
        }

        private void RefreshUpdateStatus()
        {
            AppSettings settings = _settingsService.Current;
            _lastUpdateCheckLabel.Text = GpuTelemetryFormatter.FormatRelativeDate(settings.LastUpdateCheckUtc);
            _lastUpdateStatusLabel.Text = string.IsNullOrWhiteSpace(settings.LastUpdateError)
                ? "Aucune erreur enregistrée."
                : settings.LastUpdateError;
        }

        private void ResetCaniculeGuardDefaults()
        {
            _caniculePowerThresholdInput.Value = CaniculeGuardDefaults.PowerThresholdWatts;
            _caniculeTemperatureThresholdInput.Value = CaniculeGuardDefaults.TemperatureThresholdCelsius;
            _caniculeAlertDelayInput.Value = CaniculeGuardDefaults.AlertDelaySeconds;
            _caniculeCooldownInput.Value = CaniculeGuardDefaults.CooldownSeconds;
            ShowInfo("Valeurs Canicule Guard réinitialisées dans le formulaire.");
        }

        private void OpenSettingsFolder()
        {
            string directory = Path.GetDirectoryName(_settingsService.SettingsPath);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            Directory.CreateDirectory(directory);
            OpenExternal(directory);
        }

        private void ExportDiagnostic()
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Exporter un diagnostic NVConso",
                Filter = "Fichier texte (*.txt)|*.txt",
                FileName = $"NVConso-diagnostic-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            File.WriteAllText(dialog.FileName, BuildDiagnosticText(), Encoding.UTF8);
            ShowInfo("Diagnostic exporté.");
        }

        private void ResetSettings()
        {
            DialogResult confirmation = MessageBox.Show(
                this,
                "Réinitialiser toutes les préférences locales de NVConso ? La tâche planifiée Windows n'est pas supprimée automatiquement.",
                "Réinitialiser les préférences",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmation != DialogResult.Yes)
                return;

            if (!_settingsService.TryResetToDefaults(out string message))
            {
                ShowError(message);
                return;
            }

            LoadFromSettings(_settingsService.Current);
            RefreshStartupStatus();
            ShowInfo("Préférences réinitialisées.");
        }

        private string BuildDiagnosticText()
        {
            AppSettings settings = _settingsService.Current;
            StartupTaskStatus startupStatus = _startupManager.GetStatus();

            var builder = new StringBuilder();
            builder.AppendLine("NVConso - Diagnostic préférences");
            builder.AppendLine(FormattableString.Invariant($"Date locale : {DateTimeOffset.Now:O}"));
            builder.AppendLine(FormattableString.Invariant($"Chemin settings : {_settingsService.SettingsPath}"));
            builder.AppendLine(FormattableString.Invariant($"GPU actif : {_nvml.SelectedGpuName} (#{_nvml.SelectedGpuIndex})"));
            builder.AppendLine(FormattableString.Invariant($"Plage GPU : {ResolveGpuRangeText()}"));
            builder.AppendLine(FormattableString.Invariant($"Statut démarrage Windows : {startupStatus.Message}"));
            builder.AppendLine(FormattableString.Invariant($"Thème : {settings.DashboardTheme}"));
            builder.AppendLine(FormattableString.Invariant($"Dashboard au démarrage : {settings.ShowDashboardOnStartup}"));
            builder.AppendLine(FormattableString.Invariant($"Auto update : {settings.AutoCheckUpdates}"));
            builder.AppendLine(FormattableString.Invariant($"Dernière vérification update : {settings.LastUpdateCheckUtc:O}"));
            builder.AppendLine(FormattableString.Invariant($"Dernière erreur update : {settings.LastUpdateError ?? "--"}"));
            builder.AppendLine(FormattableString.Invariant($"Canicule Guard activé : {settings.CaniculeGuardEnabled}"));
            builder.AppendLine(FormattableString.Invariant($"Canicule Guard puissance W : {settings.CaniculeGuardPowerThresholdWatts}"));
            builder.AppendLine(FormattableString.Invariant($"Canicule Guard température °C : {settings.CaniculeGuardTemperatureThresholdCelsius}"));
            return builder.ToString();
        }

        private bool TryResolveCustomPowerLimit(out uint customPowerLimitMilliwatt, out string message)
        {
            customPowerLimitMilliwatt = 0;
            uint minimumPowerLimit = ResolveMinimumPowerLimit();
            uint maximumPowerLimit = ResolveMaximumPowerLimit();

            return CustomPowerLimitValidator.TryConvertWattsToMilliwatts(
                _customPowerLimitWattsInput.Value,
                minimumPowerLimit,
                maximumPowerLimit,
                out customPowerLimitMilliwatt,
                out message);
        }

        private uint ResolveMinimumPowerLimit()
        {
            return _nvml.MinimumPowerLimit > 0 ? _nvml.MinimumPowerLimit : 1000;
        }

        private uint ResolveMaximumPowerLimit()
        {
            return _nvml.MaximumPowerLimit > ResolveMinimumPowerLimit() ? _nvml.MaximumPowerLimit : 1000000;
        }

        private string ResolveGpuRangeText()
        {
            if (_nvml.MaximumPowerLimit <= 0)
                return "--";

            string defaultText = _nvml.IsDefaultPowerLimitAvailable
                ? GpuTelemetryFormatter.FormatWatts(_nvml.DefaultPowerLimit)
                : "--";

            return string.Format(
                CultureInfo.InvariantCulture,
                "min {0} / stock {1} / max {2}",
                GpuTelemetryFormatter.FormatWatts(_nvml.MinimumPowerLimit),
                defaultText,
                GpuTelemetryFormatter.FormatWatts(_nvml.MaximumPowerLimit));
        }

        private void UpdateCustomPowerLimitEnabledState()
        {
            _customPowerLimitWattsInput.Enabled = GetSelectedValue<GpuPowerMode>(_startupProfileCombo) == GpuPowerMode.Custom;
        }

        private void ShowError(string message)
        {
            _statusLabel.ForeColor = Color.FromArgb(178, 52, 52);
            _statusLabel.Text = message;
            MessageBox.Show(this, message, "Préférences NVConso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowInfo(string message)
        {
            _statusLabel.ForeColor = Color.FromArgb(30, 128, 84);
            _statusLabel.Text = message;
        }

        private static TabPage CreateTab(string title, Control content)
        {
            var tab = new TabPage(title)
            {
                Padding = new Padding(UiSpacing.Medium)
            };
            tab.Controls.Add(content);
            return tab;
        }

        private static TableLayoutPanel CreateTabPanel()
        {
            return new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 2,
                RowCount = 0,
                Padding = new Padding(UiSpacing.Medium)
            };
        }

        private static void AddCheckBox(TableLayoutPanel panel, CheckBox checkBox)
        {
            AddFullRow(panel, checkBox);
        }

        private static void AddRow(TableLayoutPanel panel, string label, Control control)
        {
            int row = panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.Controls.Add(CreateLabel(label), 0, row);
            panel.Controls.Add(control, 1, row);
        }

        private static void AddFullRow(TableLayoutPanel panel, Control control)
        {
            int row = panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.Controls.Add(control, 0, row);
            panel.SetColumnSpan(control, 2);
        }

        private static void AddFill(TableLayoutPanel panel)
        {
            int row = panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, row);
        }

        private static Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label CreateValueLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label CreateMutedLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(95, 109, 126),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Button CreateButton(string text)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                Height = 32,
                Margin = new Padding(0, 0, UiSpacing.Small, 0),
                UseVisualStyleBackColor = true
            };
        }

        private static ComboBox CreateDropDown()
        {
            return new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
        }

        private static NumericUpDown CreateNumeric(int minimum, int maximum, int value)
        {
            return new NumericUpDown
            {
                Dock = DockStyle.Left,
                Minimum = minimum,
                Maximum = maximum,
                Value = Math.Clamp(value, minimum, maximum),
                Width = 120
            };
        }

        private static void AddThemeOptions(ComboBox comboBox)
        {
            comboBox.Items.Add(new ComboOption<UiTheme>("Système", UiTheme.System));
            comboBox.Items.Add(new ComboOption<UiTheme>("Clair", UiTheme.Light));
            comboBox.Items.Add(new ComboOption<UiTheme>("Sombre", UiTheme.Dark));
        }

        private void SyncThemeSelection(ComboBox source, ComboBox target)
        {
            if (_syncingTheme)
                return;

            _syncingTheme = true;
            target.SelectedIndex = source.SelectedIndex;
            _syncingTheme = false;
        }

        private static void SelectComboValue<T>(ComboBox comboBox, T value)
        {
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                if (comboBox.Items[index] is ComboOption<T> option && EqualityComparer<T>.Default.Equals(option.Value, value))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        private static T GetSelectedValue<T>(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboOption<T> option)
                return option.Value;

            return comboBox.Items.Count > 0 && comboBox.Items[0] is ComboOption<T> firstOption
                ? firstOption.Value
                : default;
        }

        private static decimal ToNumericValue(uint? milliwatts, NumericUpDown input)
        {
            decimal watts = milliwatts.HasValue ? milliwatts.Value / 1000m : input.Value;
            return Math.Clamp(watts, input.Minimum, input.Maximum);
        }

        private static void OpenExternal(string target)
        {
            Process.Start(new ProcessStartInfo(target)
            {
                UseShellExecute = true
            });
        }

        private sealed class ComboOption<T>
        {
            public ComboOption(string label, T value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }
            public T Value { get; }

            public override string ToString()
            {
                return Label;
            }
        }
    }
}
