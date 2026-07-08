# Captures prévues

Les captures doivent être réalisées manuellement sur une machine Windows avec :

- GPU NVIDIA ;
- pilote NVIDIA installé ;
- NVML disponible ;
- fenêtre WattPilot WPF avec données réelles.

Aucune capture fictive ne doit être ajoutée.

Chemins prévus :

```text
docs/screenshots/tray-menu.png
docs/screenshots/wattpilot-main.png
docs/screenshots/wattpilot-history.png
docs/screenshots/wattpilot-settings.png
```

`tray-menu.png` doit montrer le menu compact : ouvrir WattPilot, sous-menu profils, mise à jour réduite à une ligne d'état et une action maximum, puis quitter.

`wattpilot-main.png` doit montrer la page principale réduite : puissance instantanée, limite active, température GPU, profil actif, sélecteur de profil compact, résumé du jour et graphe de puissance.

`wattpilot-history.png` doit montrer la section historique ouverte depuis `Ouvrir l'historique détaillé`.

`wattpilot-settings.png` doit montrer le panneau `Paramètres` intégré dans la même fenêtre.

Le fichier historique `NVConso.png` peut rester dans le dépôt, mais il ne doit pas être utilisé comme preuve visuelle des fonctionnalités récentes tant qu'il n'a pas été remplacé par des captures à jour.
