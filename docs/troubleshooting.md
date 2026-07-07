# Dépannage

Ce document liste les problèmes courants et les vérifications utiles.

## NVML introuvable

Symptômes :

- le dashboard affiche NVML indisponible ;
- les métriques GPU restent vides ;
- les profils ne s'appliquent pas.

Vérifications :

- installer ou réinstaller le pilote NVIDIA ;
- vérifier que le GPU NVIDIA est présent dans le Gestionnaire de périphériques ;
- redémarrer Windows après installation du pilote ;
- vérifier que l'application s'exécute sur Windows, pas dans un environnement sans pilote NVIDIA.

WattPilot dépend de `nvml.dll`, fourni par le pilote NVIDIA.

## Droits administrateur

Symptômes :

- la lecture de télémétrie fonctionne ;
- l'application d'un power limit échoue ;
- NVML refuse l'écriture.

Causes possibles :

- l'application n'est pas élevée ;
- l'UAC a été refusé ;
- une politique de sécurité bloque l'élévation.

Actions :

- relancer WattPilot en administrateur ;
- vérifier que le manifeste demande bien l'élévation ;
- si WattPilot démarre avec Windows, réparer la tâche planifiée depuis les préférences.

## GPU non compatible power limit

Symptômes :

- NVML est disponible ;
- les limites minimum/default/maximum sont absentes ou incohérentes ;
- `nvmlDeviceSetPowerManagementLimit` échoue.

Actions :

- mettre à jour le pilote NVIDIA ;
- essayer `Stock` avant `Custom` ;
- vérifier que le GPU expose le power management dans NVML ;
- éviter de forcer une limite personnalisée hors plage.

Tous les GPU NVIDIA ne permettent pas de modifier le power limit.

## Update indisponible en ZIP portable

Symptôme :

- le menu de mise à jour indique que l'application n'est pas installée via Velopack.

Explication :

Le ZIP portable est self-contained, mais il n'est pas une installation Velopack. La mise à jour automatique complète est donc désactivée. L'auto-update complet nécessite l'installation WattPilot via Velopack.

À partir de `v2.0.0`, l'identité Velopack distribuée est `WattPilot`. Une installation `NVConso` `<= 1.1.1` peut nécessiter une réinstallation manuelle depuis GitHub Releases.

Actions :

- télécharger manuellement la nouvelle archive depuis [GitHub Releases](https://github.com/arnaud-wissart-lab/NVConso/releases/latest) ;
- ou installer WattPilot via les artefacts Velopack de release.

## Icône Windows par défaut après build local

Action :

- exécuter `dotnet clean Tools.sln`, puis reconstruire en Release.

## Tâche planifiée cassée

Symptômes :

- WattPilot ne démarre plus avec Windows ;
- le statut indique un ancien chemin ;
- la tâche cible un autre utilisateur ou des arguments inattendus.

Actions :

- ouvrir `Préférences` ;
- désactiver puis réactiver `Démarrer avec Windows` ;
- vérifier que le chemin de `WattPilot.exe` n'a pas changé ;
- éviter de déplacer manuellement le dossier d'installation Velopack.

WattPilot utilise l'argument canonique `--tray`. L'ancien alias `--minimized` reste accepté au lancement.
Une ancienne tâche planifiée `NVConso` est remplacée par la tâche `WattPilot` lors de la réparation.

## Dashboard sans données

Symptômes :

- les cartes affichent `--` ;
- les graphes restent vides ;
- l'onglet historique indique qu'aucun fichier n'existe.

Vérifications :

- NVML est-il disponible ?
- un GPU est-il sélectionné ?
- l'historisation persistante est-elle activée ?
- le fichier du jour existe-t-il dans `%LOCALAPPDATA%\WattPilot\telemetry\snapshots` ?
- l'application a-t-elle eu le temps de collecter au moins un snapshot ?

L'onglet `Temps réel` dépend du buffer mémoire. Il est vide après un redémarrage jusqu'à la prochaine collecte. L'onglet `Historique` dépend des fichiers persistés.

## Logs ou télémétrie volumineux

Actions :

- réduire `TelemetryRetentionDays` ;
- augmenter `RecordingIntervalSeconds` ;
- désactiver temporairement `RecordingEnabled` ;
- supprimer manuellement d'anciens fichiers sous `%LOCALAPPDATA%\WattPilot\telemetry` si l'application est arrêtée.

La rétention automatique ne touche pas `settings.json` et ne supprime pas les logs hors du dossier `telemetry`.

## Fichiers utiles

- Préférences : `%LOCALAPPDATA%\WattPilot\settings.json`
- Télémétrie : `%LOCALAPPDATA%\WattPilot\telemetry\`
- Documentation télémétrie : [telemetry.md](./telemetry.md)
