# Checklist UI WattPilot

Cette checklist sert de blocage manuel avant toute release lorsque l'interface a changé, ou lorsqu'une release est destinée à corriger un risque d'interface cassée.

## Environnement

- [ ] Installer ou lancer la build candidate correspondant exactement au commit publié.
- [ ] Pour tester une PR, télécharger l'artefact GitHub Actions `WattPilot-pr-preview-win-x64`.
- [ ] Cet artefact sert au test manuel de l'UI et du comportement applicatif.
- [ ] L'auto-update Velopack ne se teste pas avec cet artefact PR.
- [ ] Vérifier que l'application est lancée en utilisateur standard.
- [ ] Préparer au moins une machine Windows avec un GPU NVIDIA lorsque les profils GPU doivent être validés réellement.

## Résolutions

- [ ] 1280x720 : aucun texte tronqué de manière gênante, aucun chevauchement, boutons utilisables.
- [ ] 1600x900 : disposition équilibrée, cartes lisibles, navigation claire.
- [ ] 2560x1440 : l'interface reste dense et lisible, sans étirement excessif.
- [ ] Fenêtre réduite : la fenêtre respecte sa taille minimale, le défilement reste utilisable.
- [ ] Plein écran : les sections restent lisibles et aucune zone ne semble cassée.

## DPI

- [ ] DPI 100 % : textes, icônes et espacements corrects.
- [ ] DPI 125 % : aucun bouton, label ou champ ne déborde.
- [ ] DPI 150 % : l'interface reste exploitable avec défilement si nécessaire.

## Apparence

- [ ] Contrastes suffisants, états actifs visibles, graphiques lisibles.
- [ ] Aucun réglage de thème inutile n'est visible dans les paramètres.
- [ ] La version courte est visible dans l'accueil, dans la page de mise à jour et dans le tooltip tray.

## États fonctionnels

- [ ] Paramètres ouverts : navigation latérale visible, sections cohérentes, boutons accessibles.
- [ ] Paramètres modifiés : l'enregistrement automatique se déclenche sans bouton de sauvegarde global.
- [ ] Surveillance chaleur : les presets `Discret`, `Équilibré`, `Sensible` et `Personnalisé` sont visibles.
- [ ] Limite personnalisée : la boîte WPF affiche la plage autorisée et valide la valeur saisie.
- [ ] Historique ouvert : filtres visibles, états vide/chargé lisibles, export accessible.
- [ ] Historique ouvert : les bornes numériques des champs de réglage sont visibles.
- [ ] Détails techniques repliés : l'accueil reste centré sur les informations essentielles.
- [ ] Détails techniques dépliés : les métriques secondaires restent lisibles et ne masquent pas les actions principales.
- [ ] Menu tray : entrée d'ouverture WattPilot, modes GPU, statut de mise à jour et action quitter visibles.
- [ ] Menu tray : un clic en dehors ferme le menu sans appliquer de nouveau le mode GPU actif.

## Validation de sortie

- [ ] Aucune erreur visuelle bloquante n'est observée.
- [ ] Les libellés restent compréhensibles pour un utilisateur non technique.
- [ ] Les détails techniques restent accessibles sans dominer l'expérience principale.
- [ ] Toute anomalie restante est documentée avec capture, résolution/DPI/thème et décision de release.
