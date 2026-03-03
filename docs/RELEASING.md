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
- `SHA256SUMS.txt` (checksums SHA-256 de tous les ZIP publiés).
- Corps de release auto-généré (git-cliff avec fallback minimal).

## Où télécharger
- Dernière version: https://github.com/arnaud-wissart/NVConso/releases/latest
- Toutes les releases: https://github.com/arnaud-wissart/NVConso/releases
