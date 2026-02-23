# Maintenance NVConso

Ce document regroupe les commandes de maintenance courante du projet.

## 1. Restaurer et valider le projet

```powershell
dotnet restore Tools.sln
dotnet build Tools.sln -c Debug
dotnet test Tools.sln -c Debug
```

## 2. Vérifier les dépendances obsolètes

Mise à jour conservatrice (mineures/patch uniquement) :

```powershell
dotnet list Tools.sln package --outdated --highest-minor
```

Vue complète (inclut les sauts de version majeure) :

```powershell
dotnet list Tools.sln package --outdated --include-transitive
```

## 3. Vérifier les vulnérabilités NuGet

```powershell
dotnet list Tools.sln package --vulnerable
```

## 4. Politique de mise à jour recommandée

- Appliquer les mises à jour patch/minor en priorité.
- Évaluer séparément les mises à jour majeures (impact runtime/tests).
- Après chaque changement de dépendance : build + tests obligatoires.

## 5. Nettoyage local (optionnel)

```powershell
dotnet nuget locals all --clear
```
