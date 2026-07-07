# Installation et mise à jour

Cette page clarifie les modes de distribution de WattPilot et le comportement attendu pendant l'installation, la mise à jour et la réparation.

## Quel fichier télécharger

Depuis [GitHub Releases](https://github.com/arnaud-wissart-lab/NVConso/releases/latest) :

- `WattPilot-Setup.exe` : installation recommandée. Pour l'auto-update, utilisez `WattPilot-Setup.exe`.
- `WattPilot-win-x64.zip` : version portable. Le ZIP portable ne s'auto-update pas.
- `SHA256SUMS.txt` : checksums SHA-256 des fichiers publiés.

Les paquets Velopack `.nupkg` et les fichiers `releases.*` sont nécessaires au mécanisme d'auto-update. Ils ne sont pas le choix normal pour une installation utilisateur.

## Modes affichés par WattPilot

WattPilot détecte son mode d'exécution et l'affiche dans le dashboard ou les préférences :

- `Mode : installé via Velopack` : auto-update disponible.
- `Mode : portable ZIP — mise à jour manuelle` : mise à jour manuelle depuis GitHub Releases.
- `Mode : build développeur — auto-update indisponible` : exécution locale depuis `bin\Debug`, `bin\Release` ou un dossier de développement.

Les modes portable et développeur sont des états attendus. Ils ne doivent pas être affichés comme des erreurs rouges. Le message associé est informatif et peut renvoyer vers GitHub Releases.

## Installation Velopack

`WattPilot-Setup.exe` est un installateur Velopack one-click. Il installe WattPilot dans le profil utilisateur, puis lance l'application.

Comportement attendu :

- l'installation ne demande pas d'élévation pour lancer WattPilot ;
- WattPilot démarre en mode utilisateur standard ;
- l'auto-update intégré est disponible depuis l'application ;
- les actions GPU privilégiées demandent l'UAC seulement au moment de l'action.

Velopack limite volontairement l'interface de son `Setup.exe`. Les options documentées côté installateur portent notamment sur le mode silencieux, les logs et le dossier d'installation. Il n'existe pas, dans la documentation officielle consultée, d'option dédiée pour remplacer ou masquer finement l'interface de réparation. Si une interface installateur plus complète devient nécessaire, l'option MSI générée par Velopack avec WiX est la piste à évaluer.

Références Velopack :

- [Windows installer](https://docs.velopack.io/packaging/installer)
- [Setup.exe options](https://docs.velopack.io/reference/cli/content/setup-windows)
- [Windows packaging overview](https://docs.velopack.io/packaging/operating-systems/windows)

## ZIP portable

`WattPilot-win-x64.zip` contient une publication self-contained `win-x64`.

Comportement attendu :

- aucune installation n'est nécessaire ;
- la télémétrie et le dashboard restent utilisables ;
- l'auto-update Velopack est indisponible ;
- les mises à jour se font en téléchargeant un nouveau ZIP depuis GitHub Releases.

Le ZIP portable peut toujours demander une élévation pour une action privilégiée, par exemple modifier le power limit GPU ou configurer la tâche planifiée.

## Build développeur

Une exécution depuis un dossier de build local est détectée comme build développeur.

Comportement attendu :

- l'auto-update est indisponible ;
- aucun message rouge n'est affiché pour ce seul motif ;
- les commandes de développement et les tests restent la source de validation.

## Setup relancé alors que WattPilot est déjà installé

Relancer `WattPilot-Setup.exe` sur une machine où WattPilot est déjà installé laisse Velopack gérer son comportement natif.

Cas attendus :

- même version : Velopack peut proposer une réparation ou une réinstallation ;
- version supérieure : utilisez l'update intégré de WattPilot, ou lancez le Setup de la nouvelle release ;
- version inférieure : WattPilot ne propose pas de downgrade silencieux.

Le projet ne remplace pas ce comportement par un contournement fragile. La bonne UX est d'expliquer le mode de distribution et de nommer clairement les assets de release.

## Messages de mise à jour

WattPilot traduit les erreurs techniques Velopack en messages utilisateur :

- `NotInstalledException` : `Auto-update indisponible dans ce mode.`
- `NetworkUnavailable` : `Réseau indisponible.`
- `ChecksumFailed` : `Mise à jour refusée : intégrité invalide.`

Le message d'élévation au lancement de Setup ne doit plus apparaître avec les versions où le manifeste utilise `asInvoker`. Si une ancienne version affiche `L'opération demandée nécessite une élévation`, consultez [troubleshooting.md](./troubleshooting.md).

## Vérification après installation

Sur une machine Windows de test :

1. Désinstaller l'ancienne version de WattPilot ou NVConso.
2. Supprimer l'ancienne tâche planifiée `NVConso` si elle existe encore, ou laisser WattPilot la migrer lors de la réparation du démarrage Windows.
3. Lancer `WattPilot-Setup.exe`.
4. Vérifier que l'installation ne demande pas d'élévation pour lancer WattPilot.
5. Vérifier que WattPilot démarre et que le dashboard s'ouvre.
6. Vérifier que le mode affiché est `Mode : installé via Velopack`.
7. Vérifier que le statut de mise à jour ne signale pas une erreur en mode installé.
8. Cliquer sur un profil GPU.
9. Vérifier que l'UAC apparaît uniquement au clic sur l'action GPU privilégiée.
10. Si l'UAC est refusé, vérifier que WattPilot reste ouvert et que le profil n'est pas enregistré comme appliqué.
