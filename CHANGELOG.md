# Changelog

Toutes les notes importantes de WattPilot sont suivies dans ce fichier.

Le format suit l'esprit de Keep a Changelog et les versions suivent SemVer.

## [Unreleased]

## 2.1.11 - 2026-07-09

- Compacte les actions secondaires avec icônes, info-bulles et noms accessibles.
- Harmonise les champs, filtres et formulaires.
- Améliore la densité visuelle de l'historique.
- Clarifie les paramètres sans changer le comportement fonctionnel.
- Clarifie le statut de mise à jour dans le menu tray.
- Retire les statuts permanents anxiogènes autour de l'élévation.
- Corrige le contraste des boutons WPF.

## 2.1.10 - 2026-07-09

- Corrige le positionnement du menu tray WPF.
- Empêche le menu de sortir de l'écran avec DPI élevé, multi-écran ou barre des tâches déplacée.

## 2.1.9 - 2026-07-08

### Modifié

- Remplace le menu visible de la zone de notification par une interface WPF cohérente avec WattPilot.
- Clarifie l'affichage des mises à jour avec un statut court, la version installée et la dernière vérification séparée.
- Simplifie l'accueil en lecture seule : l'état GPU reste visible, mais le changement de mode se fait hors de l'accueil.
- Modernise la page Historique avec des filtres compacts, des actions accessibles par icône et un tableau plus lisible.
- Ajoute des styles WPF compacts réutilisables pour les boutons, listes, sélecteurs et tableaux.

### Corrigé

- Remplace la demande de “relancer en administrateur” par une autorisation Windows ponctuelle pour appliquer un mode GPU.
- Remplace la fenêtre WinForms d'élévation par une boîte WPF et garde WattPilot ouvert normalement pendant l'autorisation.
- Empêche la réapparition des anciens messages d'élévation via des tests de garde-fou.

## 2.1.8 - 2026-07-08

### Note

- 2.1.7 est supersédée par 2.1.8.

### Ajouté

- Affiche la version courte de WattPilot dans l'accueil, la page de mise à jour et le tooltip de la zone de notification.
- Ajoute des presets de surveillance chaleur : `Discret`, `Équilibré`, `Sensible` et `Personnalisé`.
- Affiche les bornes numériques sous les champs de réglage.
- Publie un artefact portable de prévisualisation pour les PR.

### Modifié

- Remplace la sauvegarde manuelle globale des paramètres par un enregistrement automatique.
- Simplifie la page Paramètres en retirant `Général`, le thème visible et le bouton `Enregistrer` global.
- Remplace la boîte de limite personnalisée WinForms par une boîte WPF intégrée.
- Prépare le renommage public complet vers WattPilot : exécutable `WattPilot.exe`, PackId Velopack `WattPilot`, ZIP portable `WattPilot-win-x64.zip` et tâche planifiée `WattPilot`.
- Ajoute la migration locale depuis `%LOCALAPPDATA%\NVConso` vers `%LOCALAPPDATA%\WattPilot` avec sauvegarde horodatée.
- Documente l'impact de migration : les installations `NVConso` `<= 1.1.1` peuvent nécessiter une réinstallation manuelle.

### Corrigé

- Empêche la tâche de démarrage Windows de lancer WattPilot avec les privilèges les plus élevés.
- Corrige l'élévation au lancement en empêchant une demande UAC préventive lors de l'enregistrement des préférences.

## [1.1.1] - 2026-07-06

### Corrigé

- Empêche le crash à l'ouverture du dashboard quand un contrôle WinForms refuse un `BackColor` transparent.
- Sécurise l'application du thème du dashboard avec une couleur de fond opaque et un fallback clair journalisé.

## [1.1.0] - 2026-07-06

### Ajouté

- Migration vers .NET 10 LTS.
- Nom produit visible `WattPilot`, avec identifiants techniques `NVConso` conservés pour compatibilité.
- Profils écran optionnels avec baisse de fréquence de rafraîchissement, snapshot, rollback et restauration.
- Détection HDR / Advanced Color par écran via DXGI quand Windows expose l'information.
- Détection VRR/G-Sync en lecture seule via NVAPI quand le pilote l'expose.
- Historisation persistante de la télémétrie GPU en CSV/JSON.
- Vue historique dans le dashboard avec filtres, résumé et export.
- Canicule Guard branché sur la télémétrie, avec alertes sans changement automatique de profil.
- Documentation française dédiée : architecture, release, télémétrie, profils écran et dépannage.

### Modifié

- README aligné avec le comportement réel de l'application.
- Workflow de release préparé pour publier depuis un tag `vX.Y.Z` et refuser une release non taguée.
- Artefacts de release : conservation de `NVConso-win-x64.zip` et ajout d'un alias portable `WattPilot-win-x64.zip`.

### Sécurité

- Les actions écran restent désactivées par défaut et nécessitent un opt-in explicite.
- HDR, VRR et G-Sync ne sont pas modifiés automatiquement.
- L'historique GPU ne journalise pas les noms de fenêtres ni les processus par défaut.

### Compatibilité

- PackId Velopack conservé : `NVConso`.
- Exécutable conservé : `NVConso.exe`.
- Dossier de préférences conservé : `%LOCALAPPDATA%\NVConso`.
- Tâche planifiée conservée : `NVConso`.

## [1.0.0] - 2026-03-03

### Ajouté

- Première release publique GitHub.
- Application WinForms Windows pour piloter prudemment la limite de puissance NVIDIA via NVML.
- Profils GPU initiaux, zone de notification, dashboard, préférences et packaging Velopack.
