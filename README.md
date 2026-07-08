# WattPilot

WattPilot est un utilitaire Windows pour suivre la consommation d'un GPU NVIDIA et appliquer des profils de limite de puissance via NVML.

WattPilot est le nom public du produit. `NVConso` reste le nom du dépôt, du projet et des namespaces historiques, mais les artefacts distribués utilisent le nom WattPilot.

[![CI](https://github.com/arnaud-wissart-lab/NVConso/actions/workflows/ci.yml/badge.svg)](https://github.com/arnaud-wissart-lab/NVConso/actions/workflows/ci.yml)
[![Licence](https://img.shields.io/github/license/arnaud-wissart-lab/NVConso)](./LICENSE)

## Télécharger

[**Télécharger WattPilot**](https://github.com/arnaud-wissart-lab/NVConso/releases/latest)

- Pour l'auto-update, utilisez `WattPilot-Setup.exe`.
- Le ZIP portable `WattPilot-win-x64.zip` ne s'auto-update pas.
- `SHA256SUMS.txt` permet de vérifier les fichiers téléchargés.

Le workflow de release publie aussi les paquets Velopack et le feed Velopack `releases.stable.json`, nécessaires aux mises à jour automatiques.

## Fonctionnalités

- Profils GPU : `Canicule`, `Vidéo / surf`, `Indie 2D`, `Normal / Stock`, `Max` et `Custom`.
- Fenêtre WPF unique avec dashboard, historique et panneau `Paramètres` intégré.
- Menu tray WinForms compact pour ouvrir WattPilot, appliquer un profil et quitter.
- Historique GPU persistant sous `%LOCALAPPDATA%\WattPilot\telemetry\`.
- Mise à jour automatique via Velopack pour l'installation `WattPilot-Setup.exe`.
- Vérification de mise à jour au lancement quand l'option automatique est active.
- Modes portable ZIP et build développeur affichés comme des états attendus, sans erreur rouge.

WattPilot ne modifie pas HDR, VRR, G-Sync, les ventilateurs ou les profils d'affichage Windows.

## Droits

WattPilot démarre sans droits administrateur. L'UAC est demandé uniquement pour une action privilégiée : appliquer une limite GPU, restaurer `Stock`, créer/réparer la tâche de démarrage Windows ou la supprimer.

Le manifeste doit rester en `asInvoker`. La CI échoue si `requireAdministrator` revient, si `Program.Main` relance l'application avec `runas`, si un asset principal `NVConso-*` réapparaît, ou si l'application repasse à plusieurs fenêtres WPF principales.

## Installation

Consultez [docs/installation.md](./docs/installation.md) pour choisir entre :

- installation Velopack avec auto-update ;
- ZIP portable avec mise à jour manuelle ;
- build développeur sans auto-update.

## Télémétrie

Consultez [docs/telemetry.md](./docs/telemetry.md) pour le format CSV/JSONL, la rétention et les champs enregistrés. WattPilot n'enregistre pas les fenêtres, les processus ou les fichiers ouverts.

## Dépannage

Consultez [docs/troubleshooting.md](./docs/troubleshooting.md) pour NVML indisponible, droits administrateur, ZIP portable, tâche planifiée et historique vide.

## Développement

Prérequis :

- Windows ;
- SDK .NET 10.x ;
- pilote NVIDIA pour tester NVML sur une machine réelle.

Commandes principales :

```powershell
dotnet restore Tools.sln
dotnet build Tools.sln --configuration Release
dotnet test Tools.sln --configuration Release
dotnet publish NVConso/NVConso.csproj -c Release -r win-x64 --self-contained true
```

Procédure de release après merge :

```powershell
git tag v2.1.4
git push origin v2.1.4
```

## Documentation

- [Installation et mise à jour](./docs/installation.md)
- [Télémétrie persistante](./docs/telemetry.md)
- [Dépannage](./docs/troubleshooting.md)
- [Architecture](./docs/architecture.md)
- [Publication et packaging](./docs/release.md)
- [Maintenance](./docs/MAINTENANCE.md)
- [Changelog](./CHANGELOG.md)

## Licence

Licence MIT. Voir [LICENSE](./LICENSE).
