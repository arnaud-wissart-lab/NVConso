# NVConso

[![CI](https://github.com/arnaud-wissart/NVConso/actions/workflows/ci.yml/badge.svg)](https://github.com/arnaud-wissart/NVConso/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/arnaud-wissart/NVConso)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![WinForms](https://img.shields.io/badge/WinForms-Windows-008080)](https://learn.microsoft.com/dotnet/desktop/winforms/)
[![NVIDIA](https://img.shields.io/badge/GPU-NVIDIA-green)](https://www.nvidia.com/)

## Présentation

**NVConso** est un utilitaire Windows léger qui permet d'ajuster rapidement la limite de consommation (Power Limit) d'un GPU NVIDIA depuis la zone de notification.

L'application propose des profils simples (Éco / Performance) et applique les changements via NVML.

## Fonctionnalités principales

- Icône tray Windows, sans fenêtre principale visible
- Deux profils d'alimentation : Éco et Performance
- Application directe du Power Limit via NVML
- Suivi de la consommation instantanée dans le menu tray (ligne non cliquable)
- Affichage de la limite active en temps réel dans le menu tray
- Vérification de compatibilité au démarrage
- Relance automatique en mode administrateur si nécessaire
- Tests unitaires via xUnit avec mock NVML

## Stack technique

- Application : .NET 8, WinForms
- API GPU : NVML (P/Invoke)
- Injection de dépendances et logs : `Microsoft.Extensions.*`
- Tests : xUnit
- CI : GitHub Actions

## Prérequis

- Windows (x64)
- SDK .NET 8 (pour build/test)
- GPU NVIDIA compatible NVML
- `nvml.dll` accessible (driver NVIDIA installé)
- Droits administrateur pour modifier le Power Limit

## Lancement local

```powershell
dotnet restore Tools.sln
dotnet run --project NVConso/NVConso.csproj
```

## Exécution des tests

```powershell
dotnet test Tools.sln
```

## Mise à jour des dépendances

Vérifier les packages obsolètes (mise à jour conservatrice, sans saut de version majeure) :

```powershell
dotnet list Tools.sln package --outdated --highest-minor
```

Vérifier les vulnérabilités NuGet :

```powershell
dotnet list Tools.sln package --vulnerable
```

Guide détaillé : [docs/MAINTENANCE.md](docs/MAINTENANCE.md)

## Limitations connues

- Le GPU ciblé est actuellement l'index `0`
- La visibilité des changements dans certains outils NVIDIA peut varier
- Certaines cartes (notamment mobiles) limitent la modification du Power Limit

## Roadmap

- Profils supplémentaires (Silent, Turbo, Work)
- Valeurs personnalisées par pas
- Démarrage automatique avec Windows
- Prise en charge multi-GPU

## Licence

Projet sous licence MIT. Voir [LICENSE](LICENSE).

© 2025 Arnaud Wissart
