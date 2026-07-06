# Publication et packaging

Ce document décrit le processus de publication actuel de WattPilot.

## Prérequis

- Windows pour valider l'application.
- SDK .NET 10.x. Le dépôt contient [../global.json](../global.json).
- Accès en écriture au dépôt GitHub.
- GitHub Actions activé.
- Tag au format strict `vX.Y.Z`.

Le workflow actuel ne publie pas de préversion depuis un tag `vX.Y.Z-alpha.1`. Le tag attendu est par exemple `v2.0.0`.

## Commandes locales avant tag

```powershell
dotnet restore Tools.sln
dotnet build Tools.sln --configuration Release
dotnet test Tools.sln --configuration Release
dotnet publish NVConso/NVConso.csproj -c Release -r win-x64 --self-contained true
```

## Déclencher une release

```powershell
git tag v2.0.0
git push origin v2.0.0
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
10. validation des assets attendus ;
11. calcul de `SHA256SUMS.txt` ;
12. publication des assets dans GitHub Releases.

## Artefacts attendus

- `WattPilot-win-x64.zip` : version portable self-contained.
- Installeur Velopack WattPilot.
- Paquets Velopack `stable` nécessaires à la mise à jour automatique.
- Feed Velopack `releases.stable.json` ou fichier `releases.*` équivalent.
- `SHA256SUMS.txt` : checksums SHA-256 de tous les fichiers publiés.

Le ZIP portable n'a pas besoin d'installation du runtime .NET. Il ne bénéficie pas de la mise à jour automatique Velopack complète.

Les assets principaux publiés ne doivent plus utiliser le nom `NVConso`. Ce nom reste acceptable uniquement pour le chemin du projet dans le dépôt, les namespaces C# et les éléments de migration depuis l'ancien nom technique.

Le workflow échoue si :

- `WattPilot-win-x64.zip` est absent ;
- `SHA256SUMS.txt` est absent ou incomplet ;
- aucun installeur Velopack WattPilot n'est présent ;
- aucun paquet Velopack `.nupkg` n'est présent ;
- aucun feed `releases.*` n'est présent ;
- un ZIP portable `win-x64` inattendu est présent.
- un asset public commence par `NVConso-`.

Quand le workflow restaure un ancien feed Velopack, il ignore les paquets `.nupkg` dont le nom ne commence pas par le PackId courant `WattPilot-`. Cette règle évite de publier dans la release `v2.0.0` des paquets hérités `NVConso-*`, incompatibles avec la nouvelle identité Velopack.

## Identité produit

WattPilot est le nom public du produit. `NVConso` était l'ancien nom technique.

À partir de `v2.0.0`, l'identité distribuée devient :

- PackId Velopack : `WattPilot` ;
- exécutable principal : `WattPilot.exe` ;
- ZIP portable : `WattPilot-win-x64.zip` ;
- titre de release GitHub : `WattPilot vX.Y.Z` ;
- dossier de préférences : `%LOCALAPPDATA%\WattPilot` ;
- tâche planifiée : `WattPilot`.

Le dépôt GitHub reste `arnaud-wissart-lab/NVConso` dans cette passe. Ce nom est une contrainte technique de dépôt, pas le nom produit affiché.

## Migration depuis NVConso

Le changement de PackId et d'exécutable crée une rupture contrôlée avec les installations `NVConso` existantes. Il ne faut pas republier `v1.1.1`. La version recommandée pour ce renommage est `v2.0.0`.

Impact attendu :

- les installations `NVConso` `<= 1.1.1` peuvent nécessiter une réinstallation manuelle depuis GitHub Releases ;
- le feed Velopack `WattPilot` est distinct de l'ancien feed `NVConso` ;
- les raccourcis ou entrées système créés par une ancienne installation peuvent devoir être supprimés par l'utilisateur si l'ancien programme reste installé.

Migration locale intégrée :

- si `%LOCALAPPDATA%\NVConso` existe et `%LOCALAPPDATA%\WattPilot` n'existe pas, WattPilot déplace le dossier vers `%LOCALAPPDATA%\WattPilot` ;
- une sauvegarde horodatée `NVConso.backup-YYYYMMDD-HHMMSS` est créée avant déplacement ;
- les préférences `settings.json` et la télémétrie sous `telemetry` suivent le dossier migré ;
- l'interface tray affiche discrètement `Migration NVConso -> WattPilot effectuée.` ;
- une ancienne tâche planifiée `NVConso` est supprimée après création ou réparation de la tâche `WattPilot`.

## Velopack

Velopack est utilisé uniquement pour les installations compatibles. L'application distingue explicitement trois modes attendus :

- `Mode : installé via Velopack` : auto-update complet disponible ;
- `Mode : portable ZIP — mise à jour manuelle` : lien GitHub Releases, pas d'installation automatique ;
- `Mode : build développeur — auto-update indisponible` : pas d'erreur rouge, diagnostic et lien GitHub Releases uniquement.

Le canal par défaut est `stable`.

Le message affiché hors installation Velopack doit rester explicite sans être présenté comme une erreur : l'auto-update complet nécessite l'installation WattPilot via Velopack, la version ZIP portable doit être mise à jour depuis GitHub Releases, et un build développeur `bin\Debug` ou `bin\Release` ne s'auto-update pas.

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
  -p:Version=2.0.0

dotnet tool install --global vpk --version 1.2.0

vpk pack `
  --packId WattPilot `
  --packVersion 2.0.0 `
  --packDir artifacts/publish/win-x64 `
  --mainExe WattPilot.exe `
  --channel stable `
  --runtime win-x64 `
  --packAuthors "Arnaud Wissart" `
  --packTitle WattPilot `
  --icon NVConso/Assets/WattPilot.ico `
  --outputDir artifacts/velopack/win-x64
```

## Tester l'auto-update chez soi

Procédure attendue pour valider une mise à jour réelle :

1. Publier une release GitHub `vX.Y.Z` avec les artefacts Velopack.
2. Télécharger l'installeur Velopack depuis GitHub Releases.
3. Installer WattPilot avec cet installeur.
4. Lancer WattPilot depuis l'installation, pas depuis `bin\Debug`, `bin\Release` ou le ZIP portable.
5. Vérifier dans le dashboard ou les préférences que le mode affiché est `Mode : installé via Velopack`.
6. Publier une version supérieure.
7. Relancer WattPilot installé ou attendre la vérification automatique.
8. Vérifier que l'action unique `Mettre à jour vers vX.Y.Z...` apparaît.
9. Lancer l'action et vérifier que le téléchargement, l'installation et le redémarrage passent par Velopack.

Le ZIP portable permet de vérifier le lancement sans installation, mais sa mise à jour reste manuelle. Un build développeur sert à valider le code local ; il doit afficher `Mise à jour : indisponible en mode développeur`.

## Procédure après merge

Pour publier une version standard après merge, créer puis pousser le tag cible :

```powershell
git tag v1.2.0
git push origin v1.2.0
```

Remplacer `v1.2.0` par la version décidée pour la release. Pour le changement de PackId et d'exécutable lié au renommage complet, `v2.0.0` reste la version recommandée.

Après le push :

1. attendre la fin du workflow `Release` ;
2. vérifier que GitHub Releases contient `WattPilot-win-x64.zip`, l'installateur Velopack WattPilot, les paquets Velopack, le feed `releases.*` et `SHA256SUMS.txt` ;
3. télécharger l'installateur ;
4. installer WattPilot ;
5. tester le lancement ;
6. vérifier le statut de mise à jour dans le dashboard ou les préférences.

## Vérification manuelle

Après publication :

- télécharger le ZIP portable `WattPilot-win-x64.zip` et lancer `WattPilot.exe` sur une machine Windows ;
- vérifier que le runtime .NET n'est pas requis séparément pour le ZIP ;
- vérifier que les artefacts Velopack WattPilot sont présents dans la release ;
- vérifier `SHA256SUMS.txt` ;
- vérifier que la mise à jour automatique est indisponible proprement depuis le ZIP ;
- vérifier la mise à jour automatique depuis une installation Velopack quand un feed de test est disponible ;
- vérifier qu'aucun asset principal nommé `NVConso-win-x64.zip` n'est publié.
