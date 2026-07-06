# Releasing NVConso

## Prérequis
- Format de tag obligatoire: `vX.Y.Z` (prerelease supportée: `vX.Y.Z-alpha.1`).
- Droits de push de tags sur le dépôt.
- GitHub Actions autorisées avec permissions workflow en lecture/écriture (pour permettre la création de release et l'upload d'assets).

## Déclencher une release
```bash
git tag v1.0.0
git push origin v1.0.0
```

## Ce que produit la pipeline
- `NVConso-<tag>-win-x64.zip` (obligatoire).
- `NVConso-<tag>-win-arm64.zip` (best-effort, peut être absent en cas d'incompatibilité native).
- Artefacts Velopack `stable` `win-x64`: installeur `NVConso-Setup.exe`, paquet complet `.nupkg`, fichiers `releases.stable.json`, `assets.stable.json` et métadonnées nécessaires à l'auto-update.
- `SHA256SUMS.txt` (checksums SHA-256 des artefacts publiés).
- Corps de release auto-généré (git-cliff avec fallback minimal).

Les ZIP restent portables. L'auto-update complet est disponible uniquement pour une installation Velopack.

## Packaging Velopack local
```powershell
dotnet publish NVConso/NVConso.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o artifacts/publish/win-x64 `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:Version=1.0.0

dotnet tool install --global vpk --version 1.2.0

vpk pack `
  --packId NVConso `
  --packVersion 1.0.0 `
  --packDir artifacts/publish/win-x64 `
  --mainExe NVConso.exe `
  --channel stable `
  --runtime win-x64 `
  --packAuthors "Arnaud Wissart" `
  --packTitle NVConso `
  --icon NVConso/Assets/NVConso.ico `
  --outputDir artifacts/velopack/win-x64
```

Pour un test d'update de bout en bout, installer une version plus ancienne via Velopack, publier une version supérieure sur GitHub Releases (ou sur un dépôt de test pointé par une branche dédiée), puis vérifier depuis le menu tray que la vérification détecte, télécharge et marque la mise à jour comme prête avant l'action `Installer et redémarrer`.

## Où télécharger
- Dernière version: https://github.com/arnaud-wissart-lab/NVConso/releases/latest
- Toutes les releases: https://github.com/arnaud-wissart-lab/NVConso/releases
