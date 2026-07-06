# Captures prévues

Les captures doivent être réalisées manuellement sur une machine Windows avec :

- GPU NVIDIA ;
- pilote NVIDIA installé ;
- NVML disponible ;
- dashboard avec données réelles ;
- profils écran vérifiés sur une configuration connue.

Aucune capture fictive ne doit être ajoutée.

Chemins prévus :

```text
docs/screenshots/tray-menu.png
docs/screenshots/dashboard-realtime.png
docs/screenshots/dashboard-history.png
docs/screenshots/preferences.png
```

`tray-menu.png` doit montrer le menu compact : résumé GPU/profil, puissance/température, affichage si disponible, sous-menu profils, mise à jour réduite à une ligne d'état et une action maximum.

`dashboard-realtime.png` doit montrer le cockpit graphique : en-tête GPU/profil/version/update/Canicule Guard, cartes de métriques, jauges et graphes temps réel.

Le fichier historique `NVConso.png` peut rester dans le dépôt, mais il ne doit pas être utilisé comme preuve visuelle des fonctionnalités récentes tant qu'il n'a pas été remplacé par des captures à jour.
