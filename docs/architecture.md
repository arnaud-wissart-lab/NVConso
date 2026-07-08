# Architecture WattPilot

Ce document décrit l'architecture réelle de WattPilot. L'application conserve WinForms pour `NotifyIcon` et le menu tray, tandis que l'expérience utilisateur principale est portée par une fenêtre WPF unique.

## Vue d'ensemble

```mermaid
flowchart LR
  User["Utilisateur"] --> Tray["TrayApplicationContext"]
  Tray --> Main["WattPilotWindow WPF"]
  Tray --> Profiles["GpuProfileController"]
  Profiles --> Nvml["INvmlManager / NvmlManager"]
  Nvml --> NativeNvml["nvml.dll"]
  Tray --> Telemetry["GpuTelemetryService"]
  Telemetry --> History["GpuTelemetryHistory"]
  Telemetry --> Recorder["ITelemetryRecorder / CsvTelemetryRecorder"]
  Recorder --> TelemetryFiles["%LOCALAPPDATA%/WattPilot/telemetry"]
  Main --> Reader["ITelemetryLogReader / CsvTelemetryLogReader"]
  Reader --> TelemetryFiles
  Tray --> Guard["ICaniculeGuard / CaniculeGuardService"]
  Guard --> Recorder
  Tray --> Startup["IStartupManager / WindowsTaskSchedulerStartupManager"]
  Startup --> TaskScheduler["Tâche planifiée Windows"]
  Tray --> Updates["IAppUpdater / VelopackAppUpdater"]
  Updates --> GitHubReleases["GitHub Releases"]
```

## Entrée applicative

[Program.cs](../NVConso/Program.cs) initialise l'application WinForms pour le tray, prépare les services et lance [TrayApplicationContext.cs](../NVConso/TrayApplicationContext.cs). L'application démarre en droits utilisateur standard ; l'élévation administrateur est demandée uniquement lors d'une action explicite qui doit écrire une limite de puissance GPU ou gérer la tâche de démarrage Windows.

Le menu tray reste le point d'entrée technique, mais l'utilisateur ouvre une seule fenêtre WPF principale : `WattPilotWindow`. Les paramètres et l'historique sont intégrés dans cette fenêtre.

## Profils GPU

Les profils sont appliqués par [GpuProfileController.cs](../NVConso/GpuProfileController.cs) et [NvmlManager.cs](../NVConso/NvmlManager.cs).

Les limites sont calculées depuis les bornes NVML du GPU actif :

- minimum ;
- default/stock, quand NVML l'expose ;
- maximum.

`Normal / Stock` et `Max` sont deux états différents. `Normal / Stock` revient à la limite constructeur. `Max` applique le plafond maximal exposé par le GPU.

La limite personnalisée est saisie en watts dans l'interface, puis convertie en milliwatts pour NVML.

## Télémétrie

[GpuTelemetryService.cs](../NVConso/GpuTelemetryService.cs) interroge NVML et publie un snapshot partagé. La fenêtre principale, Canicule Guard et l'enregistreur persistent utilisent cette source commune.

Deux historiques coexistent :

- [GpuTelemetryHistory.cs](../NVConso/GpuTelemetryHistory.cs) : buffer circulaire en mémoire, utilisé par les graphes temps réel.
- [CsvTelemetryRecorder.cs](../NVConso/CsvTelemetryRecorder.cs) : persistance CSV/JSON sur disque, utilisée par le panneau historique.

La relecture est assurée par [CsvTelemetryLogReader.cs](../NVConso/CsvTelemetryLogReader.cs). Elle lit uniquement la journée sélectionnée et downsample les points affichés si nécessaire.

## Canicule Guard

[CaniculeGuardService.cs](../NVConso/CaniculeGuardService.cs) reçoit le snapshot courant, les préférences et le profil actif. Il surveille la puissance et la température.

Le service déclenche uniquement :

- une notification ;
- un statut visible dans la fenêtre principale ;
- un événement de pic via l'enregistreur, quand il est disponible.

Il ne change pas automatiquement le profil GPU.

## Paramètres

Les paramètres sont représentés par [AppSettings.cs](../NVConso/AppSettings.cs), validés par [AppSettingsValidator.cs](../NVConso/AppSettingsValidator.cs) et stockés par [AppSettingsStore.cs](../NVConso/AppSettingsStore.cs). Ils sont modifiables depuis un panneau intégré à `WattPilotWindow`, sans fenêtre de préférences séparée.

Chemin :

```text
%LOCALAPPDATA%\WattPilot\settings.json
```

Le store écrit via un fichier temporaire avant remplacement. Les valeurs inconnues ou invalides sont normalisées quand c'est possible. Au lancement réel, il migre `%LOCALAPPDATA%\NVConso` vers `%LOCALAPPDATA%\WattPilot` si l'ancien dossier existe et que le nouveau n'existe pas encore.

## Démarrage Windows

[WindowsTaskSchedulerStartupManager.cs](../NVConso/WindowsTaskSchedulerStartupManager.cs) crée ou met à jour une tâche planifiée utilisateur nommée `WattPilot`. La tâche utilise l'argument canonique `--tray`.

L'ancien alias `--minimized` reste reconnu au lancement pour compatibilité, mais les nouvelles tâches utilisent `--tray`. Une ancienne tâche `NVConso` est détectée puis supprimée après création de la tâche `WattPilot`.

## Mises à jour

[VelopackAppUpdater.cs](../NVConso/VelopackAppUpdater.cs) utilise Velopack et GitHub Releases. Une application lancée depuis `bin` ou depuis le ZIP portable n'est pas considérée comme installée via Velopack ; la mise à jour automatique y est donc indisponible.

L'installation d'une mise à jour demande une action explicite. WattPilot ne remplace pas son exécutable manuellement.

## Choix de conception

- WinForms est conservé pour `NotifyIcon`, le menu tray compact et la boîte de limite personnalisée.
- WPF porte l'UI principale : page de suivi, panneau paramètres et historique intégré.
- Les graphes utilisent des contrôles internes, sans dépendance graphique lourde.
- Les I/O persistantes passent par des services dédiés.
- Les intégrations externes utilisent des interfaces pour rester testables.
- Les actions risquées sont désactivées par défaut ou limitées à des changements réversibles.
