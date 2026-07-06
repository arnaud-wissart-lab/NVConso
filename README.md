# WattPilot

Utilitaire Windows WinForms pour piloter prudemment la limite de puissance d'un GPU NVIDIA via NVML, suivre la télémétrie et appliquer des profils d'usage sobres.

WattPilot est le nom produit. Certains identifiants techniques restent `NVConso` pour préserver la compatibilité des installations et mises à jour existantes : dépôt GitHub, PackId Velopack, exécutable, tâche planifiée et dossier de préférences.

[![CI](https://github.com/arnaud-wissart-lab/NVConso/actions/workflows/ci.yml/badge.svg)](https://github.com/arnaud-wissart-lab/NVConso/actions/workflows/ci.yml)
[![Licence](https://img.shields.io/github/license/arnaud-wissart-lab/NVConso)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-net10.0--windows-512BD4)](./NVConso/NVConso.csproj)
[![WinForms](https://img.shields.io/badge/UI-WinForms-0078D4)](./NVConso/NVConso.csproj)

## Télécharger

- Dernière version : [GitHub Releases](https://github.com/arnaud-wissart-lab/NVConso/releases/latest).
- Installation avec mises à jour : artefacts Velopack `stable` pour `win-x64`.
- Version portable : `NVConso-win-x64.zip`, publiée en self-contained. Elle embarque le runtime .NET et ne demande pas d'installer le runtime sur la machine.
- Alias portable : `WattPilot-win-x64.zip`, contenu identique au ZIP `NVConso`, ajouté pour rendre le nom produit visible sans casser les chemins existants.
- Vérification manuelle : `SHA256SUMS.txt` est publié avec les artefacts de release.

Chaque tag Git `vX.Y.Z` déclenche le workflow de release et produit une nouvelle version téléchargeable dans GitHub Releases. Le tag reste la source de vérité pour la version publiée, la version Velopack et les métadonnées d'assembly.

## Mise à jour

WattPilot vérifie les mises à jour au lancement si `AutoCheckUpdates` est activé. La vérification est planifiée après un court délai afin de ne pas bloquer l'ouverture du dashboard.

Si une version existe, une seule action est proposée : `Mettre à jour vers vX.Y.Z...`. WattPilot enchaîne alors confirmation, téléchargement, application Velopack et redémarrage avec `--tray`. Si la mise à jour est déjà téléchargée, l'action devient `Installer et redémarrer...`.

L'auto-update est disponible uniquement pour les installations Velopack compatibles. Le PackId Velopack reste `NVConso` afin de préserver la continuité des installations existantes. WattPilot utilise encore l'identifiant technique `NVConso` pour préserver la compatibilité des mises à jour.

Depuis le ZIP portable ou une exécution développeur, la mise à jour reste manuelle. L'interface affiche un message clair et renvoie vers [GitHub Releases](https://github.com/arnaud-wissart-lab/NVConso/releases/latest). Aucun fichier arbitraire n'est exécuté.

## Fonctionnalités

- Profils GPU `Canicule`, `VideoSurf`, `Indie2D`, `Stock`, `Max` et `Custom`.
- Tableau de bord WinForms avec temps réel, historique persisté, résumé journalier et état Canicule Guard.
- Préférences centralisées : profils, démarrage Windows, mises à jour, historique, affichage, thème et options avancées.
- Démarrage avec Windows via tâche planifiée utilisateur, sans service Windows et sans mot de passe stocké.
- Mises à jour via Velopack pour les installations compatibles.
- Historisation GPU persistante en CSV/JSON sous `%LOCALAPPDATA%\NVConso\telemetry\`.
- Profils écran optionnels, désactivés par défaut, limités au refresh rate supporté.
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
- un résumé affichage quand HDR ou VRR/G-Sync sont connus ;
- l'action `Ouvrir le tableau de bord` ;
- le sous-menu `Profils` ;
- une ligne de mise à jour et une seule action si une version est disponible ou prête ;
- `Préférences...` et `Quitter`.

Les métriques détaillées, les graphes, les jauges, les options de démarrage, les options d'affichage, les réglages Canicule Guard et les détails de mise à jour sont dans le dashboard ou les préférences.

Clic gauche sur l'icône : ouvrir ou afficher le dashboard. Clic droit : afficher le menu compact. Double-clic gauche : ouvrir ou masquer le dashboard.

## Dashboard

Le dashboard est le cockpit graphique de WattPilot. Quand il faut comprendre ce que fait la carte, c'est cette fenêtre qu'il faut ouvrir plutôt que le menu tray.

WattPilot démarre dans la zone de notification ; le dashboard s'ouvre par clic gauche sur l'icône tray, par double-clic gauche ou depuis le menu compact. Fermer la fenêtre masque le dashboard sans arrêter l'application. L'arrêt réel passe par `Quitter` dans le tray.

Il contient :

- un en-tête avec GPU actif, profil actif, version WattPilot, statut de mise à jour court et état Canicule Guard ;
- un onglet `Temps réel`, alimenté par le buffer mémoire `GpuTelemetryHistory` ;
- un onglet `Historique`, alimenté par les fichiers CSV/JSON persistés ;
- les métriques GPU principales : puissance, limite, température, utilisation, décodeur, fréquences et ventilateur quand NVML les expose ;
- les jauges puissance/limite, température/seuil, utilisation GPU et décodeur vidéo ;
- les graphes puissance, température et utilisation GPU/décodeur ;
- une carte `Écrans` avec fréquence courante, fréquence maximale connue, HDR et VRR/G-Sync quand l'information est disponible ;
- les maxima du jour et le nombre de pics enregistrés ;
- l'état de Canicule Guard.

Les graphes temps réel affichent la durée réelle configurée par `TelemetryHistorySeconds`. Ils ne promettent pas de survivre à un redémarrage. L'historique persisté est relu depuis le disque, uniquement pour la journée sélectionnée.

TODO produit : étudier un mode dashboard `Compact` avec 4 cartes, 2 jauges, 1 graphe principal et un bouton `Détails`.

## Préférences

La fenêtre `Préférences` regroupe les réglages qui ne doivent pas rester uniquement dans le menu tray :

- profil de démarrage et restauration `Stock` à la fermeture ;
- démarrage Windows ;
- réglages avancés des mises à jour : vérification automatique, téléchargement automatique, préversions, dernière vérification et lien GitHub Releases ;
- thème du dashboard ;
- Canicule Guard ;
- historique GPU persistant ;
- profils écran ;
- export diagnostic et réinitialisation locale.

Les valeurs numériques sont bornées avant sauvegarde. Les préférences sont stockées dans `%LOCALAPPDATA%\NVConso\settings.json`.

## Historisation persistante

L'enregistrement est activé par défaut. Les fichiers sont écrits de manière asynchrone pour éviter de bloquer l'interface.

Arborescence :

```text
%LOCALAPPDATA%\NVConso\telemetry\
  snapshots\yyyy-MM-dd.csv
  peaks\yyyy-MM-dd.jsonl
  summaries\yyyy-MM.json
```

Les snapshots ne contiennent pas les noms de fenêtres ni la liste des processus. La rétention vaut 30 jours par défaut et ne supprime que les fichiers du dossier `telemetry`.

Voir [docs/telemetry.md](./docs/telemetry.md) pour le format CSV, les événements de pics, la rétention et les exemples d'analyse dans Excel ou LibreOffice.

## Profils écran

Les profils écran sont désactivés par défaut. Une fois activés, ils peuvent réduire uniquement la fréquence de rafraîchissement d'un écran actif, sans changer la résolution, sans couper d'écran et sans modifier la disposition multi-écrans.

État actuel :

| Sujet | Statut |
|---|---|
| Refresh rate | Supporté, avec `CDS_TEST`, mode supporté obligatoire et rollback. |
| HDR | Détection prudente via DXGI/`IDXGIOutput6` quand Windows expose l'état actif. Pas de bascule automatique. |
| VRR/G-Sync | Détection lecture seule via NVAPI quand disponible. État inconnu si l'API ou le pilote ne répond pas. Pas de bascule automatique. |
| Changements HDR/VRR expérimentaux | Options présentes, désactivées par défaut, sans action automatique dans cette version. |

Voir [docs/display-profiles.md](./docs/display-profiles.md) pour les détails de sécurité, restauration et limitations.

La détection HDR indique si HDR est actif sur un écran. Quand DXGI renvoie un état SDR, WattPilot ne peut pas toujours savoir si l'écran ne supporte pas HDR ou si HDR est simplement désactivé dans Windows. Dans ce cas, l'interface affiche un support HDR inconnu.

La détection VRR/G-Sync utilise NVAPI quand le pilote NVIDIA expose `NvAPI_Disp_GetVRRInfo` pour l'écran actif. Windows VRR, NVIDIA G-Sync, G-Sync Compatible et Adaptive Sync sont affichés comme informations de diagnostic quand elles sont fiables. WattPilot peut ouvrir les paramètres graphiques Windows ou le panneau NVIDIA pour une vérification manuelle, mais ne modifie pas ces réglages.

## Canicule Guard

Canicule Guard avertit. Il ne change pas automatiquement de profil et ne modifie pas les réglages écran.

Quand l'option est active, WattPilot surveille la puissance et la température. Les seuils puissance sont adaptés au profil actif : plus stricts en `Canicule`, intermédiaires en `VideoSurf`, plus hauts en `Indie2D`. En `Stock` et `Max`, l'alerte puissance basse consommation est désactivée ; la température reste surveillée dans tous les profils.

Un délai avant alerte et un cooldown évitent le spam. Les alertes peuvent aussi enregistrer un événement de pic dans l'historique.

## Démarrage Windows

WattPilot utilise une tâche planifiée utilisateur déclenchée à l'ouverture de session. La tâche conserve le nom technique `NVConso`, pointe vers `NVConso.exe`, utilise `--tray` et demande le niveau d'exécution le plus élevé disponible.

Cette tâche ne stocke pas de mot de passe. Elle ne remplace pas l'UAC. Elle peut devoir être réparée si l'exécutable a été déplacé.

## Mises à jour et packaging

Velopack est utilisé pour les installations mises à jour automatiquement. Le ZIP portable reste une distribution simple, self-contained, sans installation du runtime .NET, mais la mise à jour automatique complète n'y est pas supportée.

Le workflow de release publie :

- `NVConso-win-x64.zip` ;
- `WattPilot-win-x64.zip`, alias portable équivalent ;
- les artefacts Velopack `stable` pour `win-x64` ;
- `SHA256SUMS.txt`.

Voir [docs/release.md](./docs/release.md) pour le processus de release et les commandes locales de packaging.

## Sécurité

- L'écriture du power limit passe par NVML et peut demander les droits administrateur.
- Les limites sont calculées depuis la plage NVML du GPU actif, pas depuis des valeurs codées pour un modèle précis.
- La restauration `Stock` à la fermeture est optionnelle et activée par défaut.
- Les profils écran ne s'appliquent jamais sans snapshot préalable.
- Les changements écran refusés déclenchent une restauration du snapshot.
- Les mises à jour Velopack demandent une action explicite avant installation/redémarrage.
- L'historique GPU ne journalise pas les fenêtres ni les processus.

## Limitations

- Windows uniquement.
- GPU NVIDIA avec pilote et NVML requis pour les fonctions GPU.
- Certaines métriques NVML peuvent être absentes selon le GPU ou le pilote.
- Certains GPU refusent la modification du power limit.
- HDR et G-Sync/VRR ne sont pas modifiés automatiquement.
- La mise à jour automatique Velopack est indisponible en ZIP portable ou en exécution développeur `bin`.
- WattPilot ne contrôle pas les ventilateurs et ne remplace pas NVIDIA App.

Voir [docs/troubleshooting.md](./docs/troubleshooting.md) pour les diagnostics courants.

## Captures

Aucune fausse capture n'est fournie. Les captures seront faites manuellement sur une machine Windows avec GPU NVIDIA, pilote installé et télémétrie NVML disponible.

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
- [Profils écran](./docs/display-profiles.md)
- [Dépannage](./docs/troubleshooting.md)
- [Maintenance](./docs/MAINTENANCE.md)
- [Changelog](./CHANGELOG.md)

## Licence

Licence MIT. Voir [LICENSE](./LICENSE).
