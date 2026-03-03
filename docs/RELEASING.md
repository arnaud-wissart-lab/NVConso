# Releasing NVConso

## Prerequis
- Format de tag obligatoire: `vX.Y.Z` (prerelease supportee: `vX.Y.Z-alpha.1`).
- Droits de push de tags sur le depot.
- GitHub Actions autorisees avec permissions workflow en lecture/ecriture (pour permettre la creation de release et upload d'assets).

## Declencher une release
```bash
git tag v1.0.0
git push origin v1.0.0
```

## Ce que produit la pipeline
- `NVConso-<tag>-win-x64.zip` (obligatoire).
- `NVConso-<tag>-win-arm64.zip` (best-effort, peut etre absent en cas d'incompatibilite native).
- `SHA256SUMS.txt` (checksums SHA-256 de tous les ZIP publies).
- Corps de release auto-genere (git-cliff avec fallback minimal).

## Ou telecharger
- Derniere version: https://github.com/arnaud-wissart/NVConso/releases/latest
- Toutes les releases: https://github.com/arnaud-wissart/NVConso/releases
