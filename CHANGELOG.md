# Changelog

Toutes les notes importantes de WattPilot sont suivies dans ce fichier.

Le format suit l'esprit de Keep a Changelog et les versions suivent SemVer.

## [Unreleased]

### Modifié

- Prépare le renommage public complet vers WattPilot : exécutable `WattPilot.exe`, PackId Velopack `WattPilot`, ZIP portable `WattPilot-win-x64.zip` et tâche planifiée `WattPilot`.
- Ajoute la migration locale depuis `%LOCALAPPDATA%\NVConso` vers `%LOCALAPPDATA%\WattPilot` avec sauvegarde horodatée.
- Documente l'impact de migration : les installations `NVConso` `<= 1.1.1` peuvent nécessiter une réinstallation manuelle.

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
