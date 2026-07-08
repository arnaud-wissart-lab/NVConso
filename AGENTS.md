# Consignes de travail du dépôt

## Produit et utilisateurs

- Le produit public s'appelle WattPilot.
- L'utilisateur cible est non technique.
- L'interface utilisateur doit être rédigée en français clair, sans jargon développeur.
- Toute option d'interface doit avoir une utilité utilisateur claire.

## Branches et commits

- Ne jamais travailler directement sur `main`.
- Utiliser une branche dédiée par chantier.
- Faire des commits petits et explicites.
- Ne jamais créer de tag.
- Ne jamais publier de release.

## Validation

- Après chaque phase importante, exécuter :

```powershell
dotnet build Tools.sln --configuration Release
dotnet test Tools.sln --configuration Release
```

- Stopper immédiatement si le build ou les tests échouent.

## Élévation de privilèges

- Ne jamais réintroduire `requireAdministrator` dans `app.manifest`.
- Ne jamais relancer automatiquement `Program.Main` avec `Verb="runas"`.
- Garder l'élévation uniquement sur action utilisateur explicite.

## Qualité et maintenance

- Toute dépendance nouvelle doit être justifiée.
- Supprimer le code mort après refactor.
- Mettre à jour la documentation française quand le comportement change.
