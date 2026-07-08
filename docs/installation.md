# Installation et mise à jour

## Fichiers de release

Depuis [GitHub Releases](https://github.com/arnaud-wissart-lab/NVConso/releases/latest) :

- `WattPilot-Setup.exe` : installation recommandée, avec auto-update Velopack.
- `WattPilot-win-x64.zip` : version portable, sans auto-update.
- `SHA256SUMS.txt` : checksums SHA-256 des fichiers publiés.

Les paquets `.nupkg` et `releases.stable.json` sont destinés à Velopack. Ils ne sont pas le choix normal pour installer l'application manuellement.

## Modes affichés

WattPilot affiche le mode d'exécution dans le dashboard et dans `Paramètres > Mise à jour` :

- `Mode : installé via Velopack` : auto-update disponible.
- `Mode : portable ZIP — mise à jour manuelle` : mise à jour depuis GitHub Releases.
- `Mode : build développeur — auto-update indisponible` : exécution depuis un dossier de build local.

Les modes portable et développeur sont normaux. Ils ne doivent pas être affichés comme des erreurs.

## Installation Velopack

`WattPilot-Setup.exe` installe WattPilot dans le profil utilisateur et permet les mises à jour intégrées.

Comportement attendu :

- l'application démarre en droits utilisateur standard ;
- l'installation ne force pas de lancement administrateur ;
- la tâche de démarrage Windows lance WattPilot en droits utilisateur standard ;
- WattPilot vérifie les mises à jour au lancement si `Vérifier automatiquement` est activé ;
- les actions GPU privilégiées demandent l'UAC seulement au moment du clic ;
- l'update intégré peut télécharger puis appliquer une nouvelle version après confirmation.

## ZIP portable

`WattPilot-win-x64.zip` contient une publication self-contained `win-x64`.

Comportement attendu :

- aucune installation n'est nécessaire ;
- le dashboard, les profils et l'historique restent utilisables ;
- l'auto-update Velopack est indisponible ;
- la mise à jour se fait en téléchargeant puis remplaçant le ZIP manuellement.

## Build développeur

Une exécution depuis `bin\Debug`, `bin\Release` ou un dossier de développement est détectée comme build développeur. L'auto-update est désactivé et le lien GitHub Releases reste disponible.

## Vérification après installation

Sur une machine Windows de test :

1. Installer `WattPilot-Setup.exe`.
2. Vérifier que WattPilot démarre sans demande UAC immédiate.
3. Ouvrir la fenêtre depuis le tray.
4. Vérifier que le mode affiché est `Mode : installé via Velopack`.
5. Vérifier que la version courte est visible dans l'accueil et dans `Paramètres > Mise à jour`.
6. Modifier un paramètre simple et vérifier qu'il est enregistré automatiquement.
7. Appliquer un profil GPU et vérifier que l'UAC apparaît seulement à ce moment si l'écriture NVML exige une élévation.
8. Ouvrir `Paramètres > Mise à jour` et vérifier la dernière vérification, le mode et le bouton `Ouvrir GitHub Releases`.

## Migration depuis l'ancien nom

`NVConso` était l'ancien nom technique. WattPilot peut migrer les préférences depuis `%LOCALAPPDATA%\NVConso` vers `%LOCALAPPDATA%\WattPilot` et remplacer l'ancienne tâche planifiée `NVConso` par `WattPilot`.
