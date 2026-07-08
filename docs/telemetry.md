# Télémétrie persistante

WattPilot conserve deux historiques différents :

- l'historique temps réel en mémoire, utilisé par l'onglet `Temps réel` ;
- l'historique persistant sur disque, utilisé par l'onglet `Historique`.

L'historique mémoire est remis à zéro au redémarrage. L'historique disque est conservé selon la rétention configurée.

## Emplacement

Chemin par défaut :

```text
%LOCALAPPDATA%\WattPilot\telemetry\
```

Arborescence :

```text
snapshots\yyyy-MM-dd.csv
peaks\yyyy-MM-dd.jsonl
summaries\yyyy-MM.json
```

La rétention ne supprime que les fichiers de ces sous-dossiers.

Lors d'une migration depuis l'ancien nom technique, WattPilot déplace `%LOCALAPPDATA%\NVConso` vers `%LOCALAPPDATA%\WattPilot` si le nouveau dossier n'existe pas encore. Une sauvegarde horodatée est conservée avant déplacement.

## Activation et fréquence

Réglages principaux :

| Réglage | Défaut | Bornes |
|---|---:|---:|
| `RecordingEnabled` | `true` | booléen |
| `RecordingIntervalSeconds` | `1` | 1 à 60 |
| `TelemetryRetentionDays` | `30` | 1 à 365 |
| `PeakPowerThresholdWatts` | `100` | 1 à 2000 |
| `PeakTemperatureThresholdCelsius` | `70` | 1 à 150 |

L'enregistreur n'écrit pas plus souvent que la télémétrie collectée. L'écriture passe par une file interne et un worker de fond.

Si une erreur I/O survient, l'enregistreur se désactive temporairement et remonte un avertissement discret. L'application ne doit pas planter pour un échec d'écriture.

## Limite active et pics transitoires

Le `power limit` NVIDIA est un plafond de gestion transmis au GPU par le pilote. Il ne garantit pas que chaque échantillon de télémétrie NVML restera strictement sous cette valeur. Une limite active de 54 W peut donc coexister avec un relevé ponctuel à 70 W si l'échantillonnage tombe pendant une phase de transition, de boost ou de stabilisation.

WattPilot applique une tolérance pour éviter les faux positifs : les écarts inférieurs à 5 W sont ignorés. Un dépassement significatif et bref est classé `Pic transitoire`. Un dépassement qui se prolonge au moins 3 secondes est classé `Dépassement durable` et peut indiquer que le profil n'est pas encore appliqué, que le pilote refuse la limite ou que la limite active lue par NVML ne correspond pas à l'intention utilisateur. Si `PowerLimitW` est absent, WattPilot classe le relevé en `Limite non confirmée`.

Ces diagnostics servent à expliquer la télémétrie ; ils ne modifient pas la limite GPU.

## Format CSV

Un fichier CSV est créé par jour. La première ligne contient l'en-tête.

```text
TimestampUtc,TimestampLocal,GpuIndex,GpuName,ActivePowerMode,IsCustomPowerLimit,PowerUsageW,PowerLimitW,TemperatureC,GpuUtilizationPercent,MemoryUtilizationPercent,DecoderUtilizationPercent,GraphicsClockMHz,MemoryClockMHz,FanSpeedPercent,PerformanceState,MinimumPowerLimitW,DefaultPowerLimitW,MaximumPowerLimitW
```

Colonnes principales :

| Colonne | Description |
|---|---|
| `TimestampUtc` | Horodatage UTC ISO 8601. |
| `TimestampLocal` | Horodatage local ISO 8601. |
| `GpuIndex` | Index du GPU sélectionné. |
| `GpuName` | Nom du GPU retourné par NVML. |
| `ActivePowerMode` | Profil actif affiché par WattPilot. |
| `IsCustomPowerLimit` | Indique si la limite active est personnalisée. |
| `PowerUsageW` | Puissance instantanée en watts, si disponible. |
| `PowerLimitW` | Limite de puissance courante en watts, si disponible. |
| `TemperatureC` | Température GPU en degrés Celsius, si disponible. |
| `GpuUtilizationPercent` | Utilisation GPU, si disponible. |
| `MemoryUtilizationPercent` | Utilisation mémoire GPU, si disponible. |
| `DecoderUtilizationPercent` | Utilisation du décodeur vidéo, si disponible. |
| `GraphicsClockMHz` | Fréquence graphique, si disponible. |
| `MemoryClockMHz` | Fréquence mémoire, si disponible. |
| `FanSpeedPercent` | Vitesse ventilateur, si disponible. |
| `PerformanceState` | État de performance NVML, si disponible. |
| `MinimumPowerLimitW` | Limite minimale NVML en watts. |
| `DefaultPowerLimitW` | Limite stock/default NVML en watts, si disponible. |
| `MaximumPowerLimitW` | Limite maximale NVML en watts. |
Les valeurs absentes sont laissées vides.

## Format des pics

Les pics sont écrits dans un fichier JSON Lines quotidien. Chaque ligne est un objet JSON indépendant.

Exemple :

```json
{"TimestampUtc":"2026-07-06T10:15:30Z","TimestampLocal":"2026-07-06T12:15:30+02:00","Type":"PowerThreshold","GpuIndex":0,"GpuName":"NVIDIA GPU","ActivePowerMode":"Vidéo / surf","Value":126.4,"Threshold":100,"Unit":"W","Message":"Puissance au-dessus du seuil (126.4 W)."}
```

Types produits par l'enregistreur :

- `PowerThreshold` ;
- `TemperatureThreshold` ;
- `PowerDailyMaximum` ;
- `TemperatureDailyMaximum` ;
- `PowerLimitTransientOvershoot` ;
- `PowerLimitSustainedOvershoot` ;
- `PowerLimitUnconfirmed`.

Les événements liés à la limite active peuvent porter le champ `DiagnosticBadge` avec les valeurs affichées dans l'historique : `Pic transitoire`, `Dépassement durable` ou `Limite non confirmée`.

Canicule Guard peut ajouter :

- `CaniculeGuardPowerHigh` ;
- `CaniculeGuardTemperatureHigh`.

Un cooldown par type limite les répétitions.

## Résumés mensuels

Les résumés sont stockés dans `summaries\yyyy-MM.json`. Le fichier contient les résumés journaliers du mois.

Chaque résumé peut contenir :

- puissance min/moyenne/max ;
- température min/moyenne/max ;
- utilisation GPU maximale ;
- utilisation décodeur maximale ;
- temps passé par profil ;
- nombre de pics ;
- premier et dernier timestamp ;
- GPU concerné.

## Rétention

`TelemetryRetentionDays` vaut 30 jours par défaut. Le nettoyage s'exécute :

- au démarrage ;
- puis au plus une fois par jour.

Il ne supprime pas `settings.json`. Il ne supprime pas les logs applicatifs hors du dossier `telemetry`.

## Confidentialité

WattPilot n'enregistre pas :

- les noms de fenêtres ;
- les titres de navigateurs ;
- la liste des processus ;
- les chemins de fichiers ouverts.

Une option future pourra couvrir les processus GPU. Elle devra rester explicite et désactivée par défaut.

## Comparer deux profils

Méthode simple :

1. Activez l'historisation.
2. Appliquez le premier profil.
3. Lancez le même scénario pendant une durée connue.
4. Appliquez le second profil.
5. Relancez le même scénario.
6. Ouvrez l'onglet `Historique`.
7. Filtrez par date, GPU et profil.
8. Comparez `PowerUsageW`, `TemperatureC`, `GpuUtilizationPercent` et les pics.

Pour une comparaison plus propre, gardez les mêmes paramètres applicatifs et le même scénario d'usage.

## Excel et LibreOffice

Pour ouvrir un CSV :

1. Ouvrez le fichier `snapshots\yyyy-MM-dd.csv`.
2. Choisissez l'encodage UTF-8.
3. Choisissez la virgule comme séparateur.
4. Vérifiez que les colonnes numériques utilisent le point décimal.
5. Filtrez par `ActivePowerMode`.
6. Créez un tableau croisé dynamique pour comparer moyenne, maximum et nombre de pics.

Les fichiers `summaries` donnent une vue plus rapide par jour. Les fichiers `peaks` sont utiles pour retrouver les événements ponctuels.
