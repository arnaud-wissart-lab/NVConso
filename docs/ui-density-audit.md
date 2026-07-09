# Audit de densité visuelle WattPilot

Cet audit prépare la passe compacte après le hotfix `v2.1.10`, sans changement fonctionnel.

## Accueil

- `Historique` et `Paramètres` dans l'en-tête sont des actions de navigation secondaires : à convertir en icônes seules avec info-bulle et nom accessible.
- `Ouvrir l'historique détaillé` reste une action explicite de découverte depuis le panneau du jour : conserver texte + icône.
- Les cartes de métriques et le graphe sont déjà relativement compacts ; la densité dépend surtout des marges de page et des boutons d'en-tête.

## Historique

- La barre d'actions `Exporter CSV`, `Copier le résumé`, `Actualiser` et `Ouvrir le dossier` est déjà en icônes seules avec info-bulle et `AutomationProperties.Name`.
- `Accueil` et `Paramètres` dans l'en-tête sont des actions de navigation secondaires : à convertir en icônes seules.
- Les filtres `Date`, `GPU`, `Profil` et `Mesure` utilisent déjà des styles compacts, mais leur alignement repose sur des `StackPanel` indépendants et des marges répétées.
- Le tableau est lisible, mais les hauteurs de ligne et d'en-tête peuvent être légèrement resserrées pour mieux tenir en `1280x720`.
- L'état vide existe ; il doit rester clair après resserrage du tableau.

## Paramètres

- `Retour` dans l'en-tête est une action de navigation secondaire : à convertir en icône seule.
- `Valeurs recommandées` reste utile en texte + icône : l'action modifie plusieurs seuils et doit rester explicite.
- Dans la section historique, `Ouvrir dossier`, `Copier le chemin` et `Exporter` sont des actions secondaires : à convertir en icônes seules.
- Dans la section mise à jour, `Vérifier maintenant` et l'action primaire de mise à jour restent texte + icône ; `Ouvrir GitHub Releases` et `Copier diagnostic` sont secondaires et peuvent devenir des icônes seules.
- Dans la section avancée, `Réparer la tâche`, `Supprimer la tâche`, `Exporter diagnostic` et `Réinitialiser` restent texte + icône, car elles touchent à la maintenance ou à des actions potentiellement destructrices.
- Les champs `ComboBox` et `NumericBox` sont encore trop hauts ou visuellement dispersés dans les sections de paramètres.
- Les labels et aides des champs numériques sont empilés sans grille commune, ce qui crée une densité verticale irrégulière.
- Les explications doivent rester uniquement sur les réglages à risque ou peu évidents.

## Menu tray WPF

- Les entrées du menu tray doivent rester texte + icône : elles forment un menu contextuel où le libellé est le repère principal.
- La hauteur `TrayMenuButton` est acceptable, mais les marges et le bloc de mise à jour doivent rester surveillés après les ajustements de styles communs.
- L'action de mise à jour du tray reste texte seul via bouton primaire, car elle dépend d'un statut dynamique.

## Dialogues WPF

- `CustomPowerLimitDialog` : `Appliquer` reste action principale en texte ; `Annuler` reste texte simple. Le champ de saisie peut bénéficier des styles compacts communs.
- `ElevationPromptDialog` : les boutons restent texte, car le choix d'autorisation doit être explicite.
- `UpdatePromptDialog` : les boutons restent texte, car la décision d'installation doit être explicite.
- Les dialogues sont globalement cohérents ; il faut éviter de rendre les actions critiques moins lisibles.

## Synthèse des ajustements prévus

- Convertir en icônes seules les navigations d'en-tête et les actions secondaires non destructrices.
- Ajouter systématiquement info-bulle, `AutomationProperties.Name` et focus visible aux actions iconiques.
- Créer des styles compacts réutilisables pour les champs de formulaire et les groupes de champs.
- Aligner les filtres de l'historique et les champs de paramètres avec des grilles simples plutôt que des marges répétées.
- Resserer modérément le tableau d'historique sans sacrifier la lisibilité.
