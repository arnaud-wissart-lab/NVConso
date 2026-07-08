# WattPilot

Utilitaire Windows avec fenêtre principale WPF et tray WinForms pour piloter prudemment la limite de puissance d'un GPU NVIDIA via NVML, suivre la télémétrie et appliquer des profils d'usage sobres.

WattPilot est le nom public du produit. `NVConso` était l'ancien nom technique ; il peut encore apparaître dans le dépôt GitHub, le dossier projet et les namespaces C#, mais les artefacts distribués utilisent désormais le nom WattPilot.

[![CI](https://github.com/arnaud-wissart-lab/NVConso/actions/workflows/ci.yml/badge.svg)](https://github.com/arnaud-wissart-lab/NVConso/actions/workflows/ci.yml)
[![Licence](https://img.shields.io/github/license/arnaud-wissart-lab/NVConso)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-net10.0--windows-512BD4)](./NVConso/NVConso.csproj)
[![WPF](https://img.shields.io/badge/UI-WPF-0078D4)](./NVConso/NVConso.csproj)

## Télécharger

[**Télécharger WattPilot**](https://github.com/arnaud-wissart-lab/NVConso/releases/latest)

- Pour l'auto-update, utilisez `WattPilot-Setup.exe`.
- Le ZIP portable `WattPilot-win-x64.zip` ne s'auto-update pas. Il embarque le runtime .NET et se met à jour manuellement depuis GitHub Releases.
- `SHA256SUMS.txt` permet de vérifier les fichiers téléchargés.

Chaque tag Git `vX.Y.Z` déclenche le workflow de release et produit une nouvelle version téléchargeable dans GitHub Releases. Le tag reste la source de vérité pour la version publiée, la version Velopack et les métadonnées d'assembly.

Voir [docs/installation.md](./docs/installation.md) pour choisir entre installation Velopack, ZIP portable et build développeur.

## Mise à jour

WattPilot détecte le mode d'exécution et adapte l'interface de mise à jour :

- `Mode : installé via Velopack` : vérification au lancement si `AutoCheckUpdates` est activé, téléchargement et application automatique possibles ;
- `Mode : portable ZIP — mise à jour manuelle` : lien vers GitHub Releases, sans action `Installer et redémarrer` ;
- `Mode : build développeur — auto-update indisponible` : aucune erreur rouge, lien GitHub Releases et diagnostic disponibles.

Si une version existe pour une installation Velopack, une seule action est proposée : `Mettre à jour vers vX.Y.Z...`. WattPilot enchaîne alors confirmation, téléchargement, application Velopack et redémarrage avec `--tray`. Si la mise à jour est déjà téléchargée, l'action devient `Installer et redémarrer...`.

L'auto-update est disponible uniquement pour les installations Velopack compatibles. À partir de `v2.0.0`, le PackId Velopack et l'exécutable utilisent `WattPilot`. Les installations `NVConso` `<= 1.1.1` peuvent nécessiter une réinstallation manuelle depuis GitHub Releases. Si `WattPilot-Setup.exe` est relancé sur une version déjà installée, Velopack peut proposer une réparation ou une réinstallation ; utilisez l'update intégré ou le Setup de la nouvelle release pour monter de version.

WattPilot démarre en mode utilisateur standard après installation. Les droits administrateur ne sont demandés qu'au moment d'appliquer un profil GPU, une limite personnalisée, une restauration `Stock` ou une opération de tâche planifiée qui nécessite une élévation. L'élévation exécute une commande interne dédiée, puis rend la main à la fenêtre principale sans relancer toute l'application.

Depuis le ZIP portable ou une exécution développeur `bin\Debug` / `bin\Release`, la mise à jour reste manuelle. L'interface affiche un message clair et renvoie vers [GitHub Releases](https://github.com/arnaud-wissart-lab/NVConso/releases/latest). Aucun fichier arbitraire n'est exécuté.

## Fonctionnalités

- Profils GPU `Canicule`, `Vidéo / surf`, `Indie 2D`, `Normal / Stock`, `Max` et `Custom`.
- Fenêtre WPF unique avec temps réel, historique à la demande, résumé journalier et état Canicule Guard.
- Paramètres intégrés dans la fenêtre principale : profils, démarrage Windows, mises à jour, historique, thème et options avancées.
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
| `Vidéo / surf` | Vidéo, navigateur, visio | Applique une limite basse mais plus confortable. |
| `Indie 2D` | Petits jeux et jeux 2D | Applique un compromis entre sobriété et marge GPU. |
| `Normal / Stock` | Retour constructeur | Restaure la limite stock/default quand elle est disponible. |
| `Max` | Usage volontairement agressif | Applique la limite maximale autorisée par la carte. |
| `Custom` | Réglage manuel | Valide une limite en watts contre la plage NVML. |

`Normal / Stock` et `Max` restent volontairement distincts. `Normal / Stock` revient au comportement normal du constructeur. `Max` pousse le plafond au maximum autorisé par le BIOS GPU.

En mode non administrateur, la fenêtre principale reste lisible et la télémétrie continue quand NVML l'autorise. Les actions qui modifient le power limit affichent une demande UAC limitée à l'action ; si l'utilisateur annule ou si la commande élevée échoue, aucun profil n'est enregistré comme appliqué.

## Menu tray

Le menu tray est volontairement compact. Il sert de télécommande rapide, pas de fenêtre miniature.

Il affiche seulement :

- l'action `Ouvrir WattPilot` ;
- le sous-menu `Profils` ;
- une ligne de mise à jour et une seule action si une version est disponible ou prête ;
- `Quitter`.

Les métriques détaillées, les graphes, les jauges, les options de démarrage, les réglages Canicule Guard et les détails de mise à jour sont dans la fenêtre WattPilot.

Clic gauche sur l'icône : ouvrir ou afficher WattPilot. Clic droit : afficher le menu compact. Double-clic gauche : ouvrir WattPilot.

## Fenêtre principale

`WattPilotWindow` est la seule fenêtre principale de l'application. Quand il faut comprendre ce que fait la carte, c'est cette fenêtre qu'il faut ouvrir plutôt que le menu tray.

WattPilot démarre dans la zone de notification ; la fenêtre s'ouvre par clic gauche sur l'icône tray, par double-clic gauche ou depuis le menu compact. Fermer la fenêtre masque WattPilot sans arrêter l'application. L'arrêt réel passe par `Quitter` dans le tray.

Elle contient :

- un en-tête compact avec GPU actif, profil actif, mode d'exécution et statut de mise à jour court ;
- les métriques GPU principales : puissance instantanée, limite active, température et utilisation GPU ;
- un graphe principal de puissance sur la durée configurée ;
- des graphes secondaires de température et d'utilisation ;
- les maxima du jour et le nombre de pics enregistrés ;
- un sélecteur de profil compact : `Canicule`, `Vidéo / surf`, `Indie 2D`, `Normal / Stock`, `Max`, `Custom` ;
- une phrase courte qui explique l'effet du profil sélectionné ;
- un panneau `Détails techniques`, replié par défaut, pour l'utilisation GPU, le décodeur, les fréquences et le ventilateur ;
- un bouton `Ouvrir l'historique détaillé` qui ouvre l'historique dans la même fenêtre ;
- un panneau `Paramètres` intégré, sans fenêtre de préférences séparée.

Les graphes temps réel affichent la durée réelle configurée par `TelemetryHistorySeconds`. Ils ne promettent pas de survivre à un redémarrage. L'historique persisté est relu depuis le disque, uniquement pour la journée sélectionnée.

## Paramètres

Le panneau `Paramètres` intégré regroupe les réglages qui ne doivent pas rester uniquement dans le menu tray :

- profil de démarrage ;
- démarrage Windows ;
- réglages avancés des mises à jour : vérification automatique, téléchargement automatique, préversions, dernière vérification et lien GitHub Releases ;
- thème de la fenêtre ;
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

WattPilot ne modifie pas HDR, VRR ou G-Sync. Ces réglages ne sont pas affichés dans la fenêtre principale ni dans le tray.

## Canicule Guard

Canicule Guard avertit. Il ne change pas automatiquement de profil.

Quand l'option est active, WattPilot surveille la puissance et la température. Les seuils puissance sont adaptés au profil actif : plus stricts en `Canicule`, intermédiaires en `Vidéo / surf`, plus hauts en `Indie 2D`. En `Normal / Stock` et `Max`, l'alerte puissance basse consommation est désactivée ; la température reste surveillée dans tous les profils.

Un délai avant alerte et un cooldown évitent le spam. Les alertes peuvent aussi enregistrer un événement de pic dans l'historique.

## Démarrage Windows

WattPilot utilise une tâche planifiée utilisateur déclenchée à l'ouverture de session. La tâche s'appelle `WattPilot`, pointe vers `WattPilot.exe`, utilise `--tray` et demande le niveau d'exécution le plus élevé disponible. Une ancienne tâche `NVConso` est détectée puis remplacée lors de la réparation ou de l'activation du démarrage Windows.

Cette tâche ne stocke pas de mot de passe. Elle ne remplace pas l'UAC. Elle peut devoir être réparée si l'exécutable a été déplacé.

La création, la réparation ou la suppression de cette tâche peut demander une élévation administrateur dédiée. WattPilot ne demande pas cette élévation au démarrage.

## Mises à jour et packaging

Velopack est utilisé pour les installations mises à jour automatiquement. Le ZIP portable reste une distribution simple, self-contained, sans installation du runtime .NET, mais la mise à jour automatique complète n'y est pas supportée.

L'installation et l'auto-update Velopack ne nécessitent pas que toute l'application soit lancée en administrateur. Les hooks Velopack s'exécutent avant l'initialisation de l'interface et ne déclenchent pas d'UAC.

Les actions privilégiées utilisent le même exécutable avec un mode interne `--elevated-command`. Ce mode accepte uniquement une liste blanche stricte :

- `set-power-limit` ;
- `restore-stock` ;
- `configure-startup-task` ;
- `delete-startup-task`.

Les paramètres GPU, limites, profils et chemins de résultat sont validés avant exécution. Aucun argument ne permet de lancer une commande shell arbitraire.

Le workflow de release publie :

- `WattPilot-Setup.exe` ;
- `WattPilot-win-x64.zip` ;
- les paquets Velopack `stable` pour `win-x64` ;
- le feed Velopack `releases.stable.json` ;
- `SHA256SUMS.txt`.

Voir [docs/installation.md](./docs/installation.md) pour l'expérience d'installation et [docs/release.md](./docs/release.md) pour le processus de release et les commandes locales de packaging.

Procédure après merge :

```powershell
git tag v2.1.0
git push origin v2.1.0
```

Attendre le workflow `Release`, vérifier les assets publiés, télécharger l'installateur, installer WattPilot, tester le lancement puis contrôler le statut de mise à jour dans la fenêtre principale.

## Sécurité

- WattPilot démarre sans droits administrateur.
- L'écriture du power limit passe par NVML et demande les droits administrateur seulement au clic sur une action de modification, via une commande élevée dédiée.
- Les limites sont calculées depuis la plage NVML du GPU actif, pas depuis des valeurs codées pour un modèle précis.
- La restauration `Stock` à la fermeture est optionnelle et activée par défaut.
- Les mises à jour Velopack demandent une action explicite avant installation/redémarrage, sans imposer un lancement global en administrateur.
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
docs/screenshots/wattpilot-main.png
docs/screenshots/wattpilot-history.png
docs/screenshots/wattpilot-settings.png
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
- [Installation et mise à jour](./docs/installation.md)
- [Publication et packaging](./docs/release.md)
- [Télémétrie persistante](./docs/telemetry.md)
- [Fonctionnalité Display Profiles retirée](./docs/display-profiles.md)
- [Dépannage](./docs/troubleshooting.md)
- [Maintenance](./docs/MAINTENANCE.md)
- [Changelog](./CHANGELOG.md)

## Licence

Licence MIT. Voir [LICENSE](./LICENSE).
