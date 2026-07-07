# WattPilot

Utilitaire Windows avec dashboard WPF et tray WinForms pour piloter prudemment la limite de puissance d'un GPU NVIDIA via NVML, suivre la télémétrie et appliquer des profils d'usage sobres.

WattPilot est le nom public du produit. `NVConso` était l'ancien nom technique ; il peut encore apparaître dans le dépôt GitHub, le dossier projet et les namespaces C#, mais les artefacts distribués utilisent désormais le nom WattPilot.

[![CI](https://github.com/arnaud-wissart-lab/NVConso/actions/workflows/ci.yml/badge.svg)](https://github.com/arnaud-wissart-lab/NVConso/actions/workflows/ci.yml)
[![Licence](https://img.shields.io/github/license/arnaud-wissart-lab/NVConso)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-net10.0--windows-512BD4)](./NVConso/NVConso.csproj)
[![WPF](https://img.shields.io/badge/UI-WPF-0078D4)](./NVConso/NVConso.csproj)

## Télécharger

[**Télécharger WattPilot**](https://github.com/arnaud-wissart-lab/NVConso/releases/latest)

- Installateur : recommandé pour bénéficier de l'auto-update Velopack.
- ZIP portable : `WattPilot-win-x64.zip`, mise à jour manuelle depuis GitHub Releases. Il embarque le runtime .NET et ne demande pas d'installer le runtime sur la machine.
- Vérification manuelle : `SHA256SUMS.txt` est publié avec les artefacts de release.

Chaque tag Git `vX.Y.Z` déclenche le workflow de release et produit une nouvelle version téléchargeable dans GitHub Releases. Le tag reste la source de vérité pour la version publiée, la version Velopack et les métadonnées d'assembly.

## Mise à jour

WattPilot détecte le mode d'exécution et adapte l'interface de mise à jour :

- `Mode : installé via Velopack` : vérification au lancement si `AutoCheckUpdates` est activé, téléchargement et application automatique possibles ;
- `Mode : portable ZIP — mise à jour manuelle` : lien vers GitHub Releases, sans action `Installer et redémarrer` ;
- `Mode : build développeur — auto-update indisponible` : aucune erreur rouge, lien GitHub Releases et diagnostic disponibles.

Si une version existe pour une installation Velopack, une seule action est proposée : `Mettre à jour vers vX.Y.Z...`. WattPilot enchaîne alors confirmation, téléchargement, application Velopack et redémarrage avec `--tray`. Si la mise à jour est déjà téléchargée, l'action devient `Installer et redémarrer...`.

L'auto-update est disponible uniquement pour les installations Velopack compatibles. À partir de `v2.0.0`, le PackId Velopack et l'exécutable utilisent `WattPilot`. Les installations `NVConso` `<= 1.1.1` peuvent nécessiter une réinstallation manuelle depuis GitHub Releases.

Depuis le ZIP portable ou une exécution développeur `bin\Debug` / `bin\Release`, la mise à jour reste manuelle. L'interface affiche un message clair et renvoie vers [GitHub Releases](https://github.com/arnaud-wissart-lab/NVConso/releases/latest). Aucun fichier arbitraire n'est exécuté.

## Fonctionnalités

- Profils GPU `Canicule`, `VideoSurf`, `Indie2D`, `Stock`, `Max` et `Custom`.
- Tableau de bord WPF avec temps réel, historique persisté, résumé journalier et état Canicule Guard.
- Préférences WPF centralisées : profils, démarrage Windows, mises à jour, historique, thème et options avancées.
- Démarrage avec Windows via tâche planifiée utilisateur, sans service Windows et sans mot de passe stocké.
- Mises à jour via Velopack pour les installations compatibles.
- Historisation GPU persistante en CSV/JSON sous `%LOCALAPPDATA%\WattPilot\telemetry\`.
- Canicule Guard : alertes puissance/température avec seuils adaptés au profil actif, sans changement automatique de profil.

## Profils GPU

WattPilot ajuste le `power limit` NVIDIA. Ce plafond ne force pas la carte à consommer cette puissance ; il limite seulement le maximum autorisé par le GPU.

Les profils sont calculés depuis la plage NVML du GPU actif :

| Profil | Rôle | Comportement |
|---|---|---|
| `Canicule` | Sobriété maximale | Applique la limite minimale exposée par NVML. |
| `VideoSurf` | Vidéo, navigateur, visio | Applique une limite basse mais plus confortable. |
| `Indie2D` | Petits jeux et jeux 2D | Applique un compromis entre sobriété et marge GPU. |
| `Stock` | Retour constructeur | Restaure la limite stock/default quand elle est disponible. |
| `Max` | Usage volontairement agressif | Applique la limite maximale autorisée par la carte. |
| `Custom` | Réglage manuel | Valide une limite en watts contre la plage NVML. |

`Stock` et `Max` restent volontairement distincts. `Stock` revient au comportement normal du constructeur. `Max` pousse le plafond au maximum autorisé par le BIOS GPU.

## Menu tray

Le menu tray est volontairement compact. Il sert de télécommande rapide, pas de dashboard miniature.

Il affiche seulement :

- le nom `WattPilot` ;
- un résumé GPU/profil ;
- un résumé puissance/température ;
- l'action `Ouvrir le tableau de bord` ;
- le sous-menu `Profils` ;
- une ligne de mise à jour et une seule action si une version est disponible ou prête ;
- `Préférences...` et `Quitter`.

Les métriques détaillées, les graphes, les jauges, les options de démarrage, les réglages Canicule Guard et les détails de mise à jour sont dans le dashboard ou les préférences.

Clic gauche sur l'icône : ouvrir ou afficher le dashboard. Clic droit : afficher le menu compact. Double-clic gauche : ouvrir le dashboard.

## Dashboard

Le dashboard WPF est le cockpit graphique de WattPilot. Quand il faut comprendre ce que fait la carte, c'est cette fenêtre qu'il faut ouvrir plutôt que le menu tray.

WattPilot démarre dans la zone de notification ; le dashboard s'ouvre par clic gauche sur l'icône tray, par double-clic gauche ou depuis le menu compact. Fermer la fenêtre masque le dashboard sans arrêter l'application. L'arrêt réel passe par `Quitter` dans le tray.

Il contient :

- un en-tête compact avec GPU actif, profil actif, version WattPilot, statut de mise à jour court et état Canicule Guard ;
- un onglet `Temps réel`, alimenté par le buffer mémoire `GpuTelemetryHistory` ;
- un onglet `Historique`, alimenté par les fichiers CSV/JSON persistés ;
- les métriques GPU principales : puissance, limite, température, utilisation, décodeur, fréquences et ventilateur quand NVML les expose ;
- les jauges puissance/limite, température/seuil, utilisation GPU et décodeur vidéo ;
- les graphes puissance, température et utilisation GPU/décodeur ;
- les maxima du jour et le nombre de pics enregistrés ;
- l'état de Canicule Guard.

Les graphes temps réel affichent la durée réelle configurée par `TelemetryHistorySeconds`. Ils ne promettent pas de survivre à un redémarrage. L'historique persisté est relu depuis le disque, uniquement pour la journée sélectionnée.

## Préférences

La fenêtre WPF `Préférences` regroupe les réglages qui ne doivent pas rester uniquement dans le menu tray :

- profil de démarrage et restauration `Stock` à la fermeture ;
- démarrage Windows ;
- réglages avancés des mises à jour : vérification automatique, téléchargement automatique, préversions, dernière vérification et lien GitHub Releases ;
- thème du dashboard ;
- Canicule Guard ;
- historique GPU persistant ;
- export diagnostic et réinitialisation locale.

Les valeurs numériques sont bornées avant sauvegarde. Les préférences sont stockées dans `%LOCALAPPDATA%\WattPilot\settings.json`. Au premier lancement compatible, WattPilot migre automatiquement `%LOCALAPPDATA%\NVConso` vers `%LOCALAPPDATA%\WattPilot` si le nouveau dossier n'existe pas encore, avec une sauvegarde horodatée.

## Historisation persistante

L'enregistrement est activé par défaut. Les fichiers sont écrits de manière asynchrone pour éviter de bloquer l'interface.

Arborescence :

```text
%LOCALAPPDATA%\WattPilot\telemetry\
  snapshots\yyyy-MM-dd.csv
  peaks\yyyy-MM-dd.jsonl
  summaries\yyyy-MM.json
```

Les snapshots ne contiennent pas les noms de fenêtres ni la liste des processus. La rétention vaut 30 jours par défaut et ne supprime que les fichiers du dossier `telemetry`.

Voir [docs/telemetry.md](./docs/telemetry.md) pour le format CSV, les événements de pics, la rétention et les exemples d'analyse dans Excel ou LibreOffice.

## Non inclus actuellement

WattPilot ne modifie pas HDR, VRR ou G-Sync. Ces réglages ne sont pas affichés dans le dashboard principal ni dans le tray.

## Canicule Guard

Canicule Guard avertit. Il ne change pas automatiquement de profil.

Quand l'option est active, WattPilot surveille la puissance et la température. Les seuils puissance sont adaptés au profil actif : plus stricts en `Canicule`, intermédiaires en `VideoSurf`, plus hauts en `Indie2D`. En `Stock` et `Max`, l'alerte puissance basse consommation est désactivée ; la température reste surveillée dans tous les profils.

Un délai avant alerte et un cooldown évitent le spam. Les alertes peuvent aussi enregistrer un événement de pic dans l'historique.

## Démarrage Windows

WattPilot utilise une tâche planifiée utilisateur déclenchée à l'ouverture de session. La tâche s'appelle `WattPilot`, pointe vers `WattPilot.exe`, utilise `--tray` et demande le niveau d'exécution le plus élevé disponible. Une ancienne tâche `NVConso` est détectée puis remplacée lors de la réparation ou de l'activation du démarrage Windows.

Cette tâche ne stocke pas de mot de passe. Elle ne remplace pas l'UAC. Elle peut devoir être réparée si l'exécutable a été déplacé.

## Mises à jour et packaging

Velopack est utilisé pour les installations mises à jour automatiquement. Le ZIP portable reste une distribution simple, self-contained, sans installation du runtime .NET, mais la mise à jour automatique complète n'y est pas supportée.

Le workflow de release publie :

- `WattPilot-win-x64.zip` ;
- l'installeur Velopack WattPilot ;
- les paquets Velopack `stable` pour `win-x64` ;
- le feed Velopack `releases.*` ;
- `SHA256SUMS.txt`.

Voir [docs/release.md](./docs/release.md) pour le processus de release et les commandes locales de packaging.

Procédure après merge :

```powershell
git tag v1.2.0
git push origin v1.2.0
```

Attendre le workflow `Release`, vérifier les assets publiés, télécharger l'installateur, installer WattPilot, tester le lancement puis contrôler le statut de mise à jour dans le dashboard ou les préférences.

## Sécurité

- L'écriture du power limit passe par NVML et peut demander les droits administrateur.
- Les limites sont calculées depuis la plage NVML du GPU actif, pas depuis des valeurs codées pour un modèle précis.
- La restauration `Stock` à la fermeture est optionnelle et activée par défaut.
- Les mises à jour Velopack demandent une action explicite avant installation/redémarrage.
- L'historique GPU ne journalise pas les fenêtres ni les processus.

## Limitations

- Windows uniquement.
- GPU NVIDIA avec pilote et NVML requis pour les fonctions GPU.
- Certaines métriques NVML peuvent être absentes selon le GPU ou le pilote.
- Certains GPU refusent la modification du power limit.
- WattPilot ne modifie pas HDR, VRR ou G-Sync.
- La mise à jour automatique Velopack est indisponible en ZIP portable ou en exécution développeur `bin`.
- WattPilot ne contrôle pas les ventilateurs et ne remplace pas NVIDIA App.

Voir [docs/troubleshooting.md](./docs/troubleshooting.md) pour les diagnostics courants.

## Captures

Aucune fausse capture n'est fournie. Les captures WPF rafraîchies sont à refaire manuellement sur une machine Windows avec GPU NVIDIA, pilote installé et télémétrie NVML disponible.

Chemins prévus :

```text
docs/screenshots/tray-menu.png
docs/screenshots/dashboard-realtime.png
docs/screenshots/dashboard-history.png
docs/screenshots/preferences.png
```

Voir [docs/screenshots/README.md](./docs/screenshots/README.md).

## Développement

Prérequis :

- Windows.
- SDK .NET 10.x. Le dépôt contient [global.json](./global.json) pour stabiliser le SDK.
- Pilote NVIDIA installé pour tester NVML sur une machine réelle.

Commandes principales :

```powershell
dotnet restore Tools.sln
dotnet build Tools.sln --configuration Release
dotnet test Tools.sln --configuration Release
dotnet publish NVConso/NVConso.csproj -c Release -r win-x64 --self-contained true
```

La cible principale est `net10.0-windows` en `x64`. Nullable reste désactivé globalement pour éviter un diff massif.

## Documentation

- [Architecture](./docs/architecture.md)
- [Publication et packaging](./docs/release.md)
- [Télémétrie persistante](./docs/telemetry.md)
- [Fonctionnalité Display Profiles retirée](./docs/display-profiles.md)
- [Dépannage](./docs/troubleshooting.md)
- [Maintenance](./docs/MAINTENANCE.md)
- [Changelog](./CHANGELOG.md)

## Licence

Licence MIT. Voir [LICENSE](./LICENSE).
