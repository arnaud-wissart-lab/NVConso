# NVConso

[![CI](https://github.com/arnaud-wissart/NVConso/actions/workflows/ci.yml/badge.svg)](https://github.com/arnaud-wissart/NVConso/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/arnaud-wissart/NVConso)](./LICENSE)
![.NET 8](https://img.shields.io/badge/.NET-8.0-blueviolet)
![WinForms](https://img.shields.io/badge/Tech-WinForms-008080)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![NVIDIA](https://img.shields.io/badge/GPU-NVIDIA-green)
![PowerLimit](https://img.shields.io/badge/Feature-Power%20Limit-orange)

🎛️ **NVConso** est un utilitaire Windows léger pour ajuster dynamiquement la **limite de consommation électrique (Power Limit)** de ta carte graphique **NVIDIA**, directement depuis la zone de notification Windows.

## 🚀 Fonctionnalités

- Icône discrète dans le **tray Windows**
- Deux modes d’alimentation :
  - 🧘 **Éco** : limite à ~10% du TDP max
  - 🔥 **Performance** : limite maximale autorisée
- Contrôle direct via **NVML** (API officielle NVIDIA)
- Démarrage rapide et silencieux (pas de fenêtre visible)
- Idéal pour :
  - Travailler (dev, bureautique) sans gaspillage énergétique
  - Passer en mode jeu d’un clic

## 🖼️ Captures

![Capture du tray NVConso](docs/screenshots/tray.png)

## ✅ Tests

Ce projet inclut un projet de **tests unitaires** `NVConso.Tests`, basé sur **xUnit**, avec un **Mock de la couche NVML** permettant de tester sans carte NVIDIA réelle.

### 💻 Lancer les tests

```bash
dotnet test NVConso.Tests/NVConso.Tests.csproj
```

## 🛠️ Prérequis

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Carte graphique NVIDIA **compatible NVML**
- Fichier `nvml.dll` disponible dans le PATH ou à côté de l’exécutable
- Application **lancée en mode administrateur**

## 📥 Téléchargement

- **Releases** : placeholder — les binaires seront publiés via l’onglet [Releases](https://github.com/arnaud-wissart/NVConso/releases).
- Selon la configuration Windows et les permissions GPU, `NVConso.exe` peut nécessiter des droits administrateur.

## ⚠️ Disclaimer

NVConso applique des changements matériels via NVML : utilise-le à tes risques, en connaissance de cause.
Vérifie toujours tes valeurs (GPU-Z, télémétrie NVIDIA, etc.) et commence avec des réglages prudents.
L’auteur ne peut pas garantir la compatibilité avec toutes les cartes, drivers ou contextes d’exécution.

## ⚠️ Remarques importantes

- Les modifications de Power Limit peuvent **ne pas s’afficher dans l'application NVIDIA officielle**.
- Pour une lecture fiable des valeurs, utilise un outil comme **GPU-Z**.
- Certaines limitations peuvent s’appliquer selon ta carte (notamment sur les portables).

## 🧭 Roadmap envisagée

- Valeurs personnalisées de limite (par pas de 5%)
- Profils "Turbo", "Silent", "Work", etc.
- Mode automatique basé sur l'activité ou l'utilisation CPU/GPU
- Démarrage automatique avec Windows
- Prise en charge multi-GPU

## 📄 Licence

Ce projet est sous licence **MIT** — libre d'utilisation, modification et redistribution.

---

© 2025 Arnaud Wissart
