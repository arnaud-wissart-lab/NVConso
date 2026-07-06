# Publication et packaging

Ce document décrit le processus de publication actuel de WattPilot.

## Prérequis

- Windows pour valider l'application.
- SDK .NET 10.x. Le dépôt contient [../global.json](../global.json).
- Accès en écriture au dépôt GitHub.
- GitHub Actions activé.
- Tag au format strict `vX.Y.Z`.

Le workflow actuel ne publie pas de préversion depuis un tag `vX.Y.Z-alpha.1`. Le tag attendu est par exemple `v1.1.0`.

## Commandes locales avant tag

```powershell
dotnet restore Tools.sln
dotnet build Tools.sln --configuration Release
dotnet test Tools.sln --configuration Release
dotnet publish NVConso/NVConso.csproj -c Release -r win-x64 --self-contained true
```

## Déclencher une release

```powershell
git tag v1.1.0
git push origin v1.1.0
```

Le workflow [../.github/workflows/release.yml](../.github/workflows/release.yml) dérive la version depuis le tag. Il échoue si le tag ne respecte pas `vX.Y.Z`.

Le workflow peut aussi être lancé manuellement avec `workflow_dispatch`, mais seulement en indiquant un tag existant au format `vX.Y.Z`. Il checkout ce tag et refuse de publier une release non taguée.

## Étapes CI de publication

Le workflow exécute :

1. restauration NuGet ;
2. build Release ;
3. tests Release ;
4. audits NuGet vulnérables et dépréciés ;
5. `dotnet publish` self-contained `win-x64` ;
6. génération du ZIP portable ;
7. installation de `vpk` ;
8. récupération éventuelle du feed Velopack précédent ;
9. packaging Velopack `stable` ;
10. calcul de `SHA256SUMS.txt` ;
11. publication des assets dans GitHub Releases.

## Artefacts attendus

- `NVConso-win-x64.zip` : version portable self-contained conservée pour compatibilité.
- `WattPilot-win-x64.zip` : alias portable au nom produit visible, contenu identique au ZIP `NVConso`.
- Artefacts Velopack `stable` pour `win-x64`, dont l'installeur et les paquets nécessaires à la mise à jour automatique.
- `SHA256SUMS.txt` : checksums SHA-256 de tous les fichiers publiés.

Le ZIP portable n'a pas besoin d'installation du runtime .NET. Il ne bénéficie pas de la mise à jour automatique Velopack complète.

## Velopack

Velopack est utilisé uniquement pour les installations compatibles. Une exécution depuis `bin\Debug`, `bin\Release` ou une archive ZIP portable renvoie une erreur propre du type `Application non installée via Velopack`.

Le canal par défaut est `stable`.

Le nom produit affiché est `WattPilot`, mais le PackId Velopack reste `NVConso`. WattPilot utilise encore l'identifiant technique `NVConso` pour préserver la compatibilité des mises à jour.

Le message affiché hors installation Velopack doit rester explicite : l'auto-update complet nécessite l'installation WattPilot/NVConso via Velopack, et la version ZIP portable doit être mise à jour depuis GitHub Releases.

Identifiants conservés dans cette phase :

- PackId Velopack : `NVConso` ;
- exécutable principal : `NVConso.exe` ;
- ZIP portable historique : `NVConso-win-x64.zip` ;
- alias ZIP portable : `WattPilot-win-x64.zip` ;
- dépôt GitHub : `arnaud-wissart-lab/NVConso`.

### Pourquoi le PackId reste NVConso

Velopack utilise le PackId comme identité d'application pour résoudre le feed, les paquets installés, les deltas et les mises à jour en attente. Garder `NVConso` permet aux installations déjà présentes de voir les nouvelles releases `stable` sans migration intermédiaire.

### Risques d'un changement immédiat de PackId

Changer directement le PackId en `WattPilot` créerait une identité Velopack distincte. Les installations existantes pourraient ne plus détecter les mises à jour, conserver des raccourcis ou entrées système liés à l'ancien produit, ou nécessiter une réinstallation manuelle. Le risque principal est de laisser des utilisateurs sur un feed `NVConso` qui ne reçoit plus de paquets compatibles.

### Migration future du PackId

Une migration complète devra être planifiée comme une release dédiée. Elle devra au minimum :

- publier une dernière version `NVConso` capable d'expliquer ou d'orchestrer la transition ;
- vérifier la compatibilité entre l'ancien feed et le nouveau feed ;
- décider quoi faire du dossier `%LOCALAPPDATA%\NVConso`, de la tâche planifiée `NVConso`, de `NVConso.exe` et des raccourcis ;
- documenter une procédure de rollback ou de réinstallation propre ;
- tester une installation existante avant et après migration.

Une phase future pourra étudier une migration complète du PackId, du dossier AppData, de la tâche planifiée, du nom de dépôt et de la compatibilité de mise à jour entre l'ancien et le nouveau produit.

## Packaging local Velopack

Exemple de packaging local :

```powershell
dotnet publish NVConso/NVConso.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o artifacts/publish/win-x64 `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:Version=1.1.0

dotnet tool install --global vpk --version 1.2.0

vpk pack `
  --packId NVConso `
  --packVersion 1.1.0 `
  --packDir artifacts/publish/win-x64 `
  --mainExe NVConso.exe `
  --channel stable `
  --runtime win-x64 `
  --packAuthors "Arnaud Wissart" `
  --packTitle WattPilot `
  --icon NVConso/Assets/NVConso.ico `
  --outputDir artifacts/velopack/win-x64
```

Pour tester la mise à jour de bout en bout, installez une version plus ancienne via Velopack, publiez une version supérieure, puis vérifiez depuis le tray que WattPilot détecte, télécharge et marque la mise à jour comme prête avant l'action d'installation.

## Vérification manuelle

Après publication :

- télécharger le ZIP portable et lancer `NVConso.exe` sur une machine Windows ;
- vérifier que le runtime .NET n'est pas requis séparément pour le ZIP ;
- vérifier que les artefacts Velopack sont présents dans la release ;
- vérifier `SHA256SUMS.txt` ;
- vérifier que la mise à jour automatique est indisponible proprement depuis le ZIP ;
- vérifier la mise à jour automatique depuis une installation Velopack quand un feed de test est disponible.
