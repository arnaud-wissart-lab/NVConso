# Télémétrie persistante

WattPilot conserve un historique temps réel en mémoire et un historique persistant sur disque. L'historique mémoire est remis à zéro au redémarrage ; l'historique disque respecte la rétention configurée.

## Emplacement

```text
%LOCALAPPDATA%\WattPilot\telemetry\
  snapshots\yyyy-MM-dd.csv
  peaks\yyyy-MM-dd.jsonl
  summaries\yyyy-MM.json
```

La rétention ne supprime que les fichiers de ces sous-dossiers.

## Réglages

| Réglage | Défaut | Bornes |
|---|---:|---:|
| `RecordingEnabled` | `true` | booléen |
| `RecordingIntervalSeconds` | `1` | 1 à 60 |
| `TelemetryRetentionDays` | `30` | 1 à 365 |
| `PeakPowerThresholdWatts` | `100` | 1 à 2000 |
| `PeakTemperatureThresholdCelsius` | `70` | 1 à 150 |

L'écriture est asynchrone. En cas d'erreur I/O, l'enregistreur se désactive temporairement et remonte un avertissement sans faire planter l'application.

## Format CSV

Un fichier snapshot est créé par jour :

```text
TimestampUtc,TimestampLocal,GpuIndex,GpuName,ActivePowerMode,IsCustomPowerLimit,PowerUsageW,PowerLimitW,TemperatureC,GpuUtilizationPercent,MemoryUtilizationPercent,DecoderUtilizationPercent,GraphicsClockMHz,MemoryClockMHz,FanSpeedPercent,PerformanceState,MinimumPowerLimitW,DefaultPowerLimitW,MaximumPowerLimitW
```

Les valeurs absentes sont laissées vides. Les fichiers ne contiennent pas les titres de fenêtres, les processus ou les chemins de fichiers ouverts.

## Pics

Les pics sont écrits en JSON Lines dans `peaks\yyyy-MM-dd.jsonl`.

Types possibles :

- `PowerThreshold` ;
- `TemperatureThreshold` ;
- `PowerDailyMaximum` ;
- `TemperatureDailyMaximum` ;
- `PowerLimitTransientOvershoot` ;
- `PowerLimitSustainedOvershoot` ;
- `PowerLimitUnconfirmed` ;
- `CaniculeGuardPowerHigh` ;
- `CaniculeGuardTemperatureHigh`.

Les diagnostics de limite active peuvent afficher `Pic transitoire`, `Dépassement durable` ou `Limite non confirmée`. Ils expliquent la télémétrie ; ils ne modifient pas la limite GPU.

## Résumés mensuels

Les fichiers `summaries\yyyy-MM.json` regroupent les résumés journaliers : puissance min/moyenne/max, température min/moyenne/max, utilisation maximale, temps par profil, nombre de pics et GPU concerné.

## Comparer deux profils

1. Activer l'historisation.
2. Appliquer un profil.
3. Exécuter un scénario reproductible.
4. Appliquer un second profil.
5. Rejouer le même scénario.
6. Ouvrir l'historique dans WattPilot et filtrer par date, GPU et profil.

Pour Excel ou LibreOffice, ouvrir le CSV en UTF-8 avec virgule comme séparateur.
