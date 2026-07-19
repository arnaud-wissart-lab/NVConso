# Dépannage

## NVML indisponible

Symptômes :

- les métriques GPU restent vides ;
- les profils ne s'appliquent pas ;
- WattPilot indique que NVML est indisponible.

Vérifications :

- installer ou réinstaller le pilote NVIDIA ;
- vérifier que le GPU NVIDIA est visible dans le Gestionnaire de périphériques ;
- redémarrer Windows après installation du pilote ;
- vérifier que l'application s'exécute sur Windows avec accès au pilote NVIDIA.

## Droits administrateur

WattPilot démarre sans droits administrateur. L'UAC est demandé seulement pour modifier la limite de puissance GPU ou gérer la tâche planifiée de démarrage.

Pour un changement de mode GPU, WattPilot peut proposer :

- `Autoriser pour cette session` : Windows demande une autorisation, puis les changements de mode GPU suivants peuvent être appliqués sans nouvelle demande pendant la session ;
- `Une seule fois` : seule l'action en cours utilise l'autorisation Windows ;
- `Annuler` : aucune modification GPU n'est appliquée.

L'autorisation de session ne concerne pas la tâche de démarrage Windows. Réparer ou supprimer cette tâche garde une autorisation ponctuelle séparée.

La tâche de démarrage Windows doit lancer WattPilot en droits utilisateur standard. Si l'UAC apparaît au lancement, supprimer ou réparer la tâche depuis `Paramètres > Avancé`.

Si une action échoue :

- relancer l'action et accepter l'UAC ;
- essayer `Une seule fois` si l'autorisation de session ne démarre pas correctement ;
- vérifier qu'une politique de sécurité ne bloque pas l'élévation ;
- réparer la tâche planifiée depuis `Paramètres > Avancé` si le démarrage Windows est concerné.

Si l'UAC est validé avec un autre compte administrateur, WattPilot peut ne pas réutiliser l'autorisation de session. C'est attendu : la connexion locale au helper est limitée au compte Windows qui l'a lancé.

## Installation ou update Velopack

`WattPilot-Setup.exe` doit lancer WattPilot en mode utilisateur standard. Si une ancienne version affiche `L'opération demandée nécessite une élévation`, installer une version récente où le manifeste est `asInvoker`.

Depuis le ZIP portable ou un build développeur, l'auto-update est indisponible. C'est attendu : télécharger la nouvelle version depuis [GitHub Releases](https://github.com/arnaud-wissart-lab/NVConso/releases/latest).

## GPU incompatible avec le power limit

Tous les GPU NVIDIA ne permettent pas de modifier le power limit.

Actions :

- mettre à jour le pilote NVIDIA ;
- essayer `Normal / Stock` avant `Custom` ;
- vérifier que NVML expose une plage minimum/default/maximum cohérente ;
- ne pas forcer une limite personnalisée hors plage.

## Tâche planifiée cassée

Symptômes :

- WattPilot ne démarre plus avec Windows ;
- le statut indique un ancien chemin ;
- la tâche cible un autre utilisateur ou des arguments inattendus.

Actions :

- ouvrir `Paramètres > Avancé` ;
- utiliser `Réparer la tâche` ;
- vérifier que le chemin de `WattPilot.exe` n'a pas changé ;
- éviter de déplacer manuellement le dossier d'installation Velopack.

Une ancienne tâche `NVConso` peut être remplacée par la tâche `WattPilot` lors de la réparation.

## Historique vide

Vérifications :

- l'historisation est-elle activée ?
- WattPilot a-t-il eu le temps de collecter au moins un snapshot ?
- le fichier du jour existe-t-il dans `%LOCALAPPDATA%\WattPilot\telemetry\snapshots` ?
- la date et les filtres de l'historique correspondent-ils aux données enregistrées ?

Pour réduire le volume disque, diminuer `TelemetryRetentionDays`, augmenter `RecordingIntervalSeconds` ou désactiver temporairement `RecordingEnabled`.

## Fichiers utiles

- Préférences : `%LOCALAPPDATA%\WattPilot\settings.json`
- Télémétrie : `%LOCALAPPDATA%\WattPilot\telemetry\`
- Documentation télémétrie : [telemetry.md](./telemetry.md)
