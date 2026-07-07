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
- le dashboard affiche `Mode lecture seule — une élévation sera demandée pour appliquer les profils.`

Causes possibles :

- l'application n'est pas élevée ;
- l'UAC a été refusé ;
- une politique de sécurité bloque l'élévation.

Actions :

- cliquer sur un profil ou une limite personnalisée, puis choisir `Exécuter en administrateur` ;
- si l'UAC est refusé, relancer l'action et accepter l'élévation ;
- si WattPilot démarre avec Windows, réparer la tâche planifiée depuis les préférences.

WattPilot démarre volontairement sans droits administrateur. L'élévation est demandée seulement pour modifier la limite de puissance GPU ou gérer la tâche planifiée élevée. Le dashboard, l'historique, la télémétrie et les mises à jour restent dans le processus principal non élevé.

Les commandes élevées passent par un mode interne `--elevated-command` limité à une liste blanche : modification de power limit, restauration `Stock`, configuration ou suppression de la tâche planifiée. Les paramètres sont validés et aucun shell arbitraire n'est exécuté.

## Erreur Velopack `L'opération demandée nécessite une élévation`

Symptômes :

- `Setup.exe` installe WattPilot puis affiche `Une erreur s'est produite — L'opération demandée nécessite une élévation. (os error -2147024156)` ;
- l'installation ne parvient pas à lancer l'application après les hooks Velopack.

Cause :

Les anciennes versions forçaient toute l'application à démarrer en administrateur via le manifeste et une relance `runas` au démarrage. Velopack pouvait alors installer puis tenter de lancer WattPilot depuis un contexte non élevé, ce qui déclenchait l'erreur.

Correction attendue :

- installer une version où le manifeste utilise `asInvoker` ;
- vérifier que WattPilot démarre en mode utilisateur standard après installation ;
- accepter l'élévation uniquement au moment d'appliquer un profil GPU, une limite personnalisée, une restauration `Stock` ou une opération de tâche planifiée.

Cette élévation lance une commande dédiée puis se termine ; elle ne relance pas toute l'application en administrateur.

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
