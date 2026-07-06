# Profils écran

Les profils écran associent des actions d'affichage aux profils GPU. Ils sont optionnels, explicites et désactivés par défaut.

Objectif : réduire une partie de la consommation liée à l'affichage sans toucher agressivement à la configuration Windows.

## Statut des fonctions

| Fonction | Statut |
|---|---|
| Énumération des écrans actifs | Supporté. |
| Nom et chemin écran quand disponibles | Supporté. |
| Résolution courante | Supporté. |
| Fréquence courante | Supporté. |
| Fréquence maximale disponible | Supporté quand Windows expose les modes. |
| Baisse du refresh rate | Supporté, si le mode existe. |
| Détection HDR | Supportée via DXGI/`IDXGIOutput6` quand Windows expose l'état actif. Sinon `Unknown`. |
| Bascule HDR automatique | Non supportée. |
| Détection VRR/G-Sync | Lecture seule via NVAPI quand disponible. L'état peut rester `Unknown`. |
| Bascule VRR/G-Sync automatique | Non supportée. |

## Réglages

| Réglage | Défaut | Effet |
|---|---:|---|
| `EnableDisplayProfiles` | `false` | Active les actions écran. |
| `RestoreDisplayStateOnStock` | `true` | Restaure le snapshot quand `Stock` est appliqué. |
| `RestoreDisplayStateOnExit` | `true` | Restaure le snapshot à la fermeture. |
| `CaniculeTargetRefreshRateHz` | `60` | Cible du profil `Canicule`. |
| `VideoSurfTargetRefreshRateHz` | `120` | Cible préférée du profil `VideoSurf`. |
| `Indie2DTargetRefreshRateHz` | `120` | Cible du profil `Indie2D`. |
| `AllowExperimentalHdrChanges` | `false` | Prévu pour une phase future. |
| `AllowExperimentalVrrChanges` | `false` | Prévu pour une phase future. |

Les options expérimentales existent dans les préférences, mais ne déclenchent pas de changement automatique dans la version actuelle.

## Refresh rate

Quand les profils écran sont activés :

- `Canicule` cible 60 Hz par défaut.
- `VideoSurf` cible 120 Hz si disponible, sinon 60 Hz.
- `Indie2D` cible 120 Hz si disponible.
- `Stock` restaure le snapshot écran si l'option est active.
- `Max` ne déclenche aucune économie écran automatique.
- `Custom` ne déclenche aucune économie écran automatique.

WattPilot n'applique jamais un mode non supporté. Le mode est testé par Windows avant application.

## Ce qui est modifié

Uniquement le refresh rate de la résolution courante, pour les écrans actifs concernés.

WattPilot ne modifie pas :

- la résolution ;
- le nombre d'écrans actifs ;
- la disposition multi-écrans ;
- l'écran principal ;
- l'orientation ;
- le scaling ;
- HDR ;
- G-Sync/VRR.

## Sécurité et restauration

Avant toute modification, WattPilot capture un snapshot des écrans actifs.

Pour chaque action :

1. vérifier que l'écran est toujours présent ;
2. vérifier que la résolution n'a pas changé depuis la lecture ;
3. vérifier que le refresh rate cible est listé ;
4. exécuter `CDS_TEST` ;
5. appliquer le mode ;
6. journaliser le résultat.

Si une application échoue, WattPilot tente de restaurer le snapshot. À la fermeture, `RestoreDisplayStateOnExit` restaure aussi le snapshot si disponible.

## HDR

WattPilot lit l'état HDR via DXGI/`IDXGIOutput6` quand Windows expose l'information d'affichage avancée.

États possibles :

- `Active` ;
- `Sdr` ;
- `Unknown`.

La détection repose sur `DXGI_OUTPUT_DESC1.ColorSpace` :

- `DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020` indique HDR actif ;
- `DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709` indique SDR actif, ou un écran Advanced Color SDR que DXGI ne permet pas de distinguer ;
- l'absence d'`IDXGIOutput6` laisse l'état inconnu.

DXGI ne permet pas toujours de savoir si HDR est supporté mais désactivé. Dans ce cas, WattPilot expose `HDRSupportedUnknown` au lieu d'affirmer que l'écran ne supporte pas HDR.

La bascule HDR automatique n'est pas implémentée. Les API non officielles ou non fiables ne sont pas utilisées pour désactiver HDR.

L'interface peut ouvrir les paramètres d'affichage Windows pour une action manuelle. Selon la version de Windows, `ms-settings:display-advanced` peut ouvrir une page d'affichage avancé ou retomber sur les paramètres d'affichage généraux.

## VRR et G-Sync

WattPilot affiche un état VRR/G-Sync quand une source fiable est disponible. Dans la phase actuelle, l'état peut rester `Unknown`.

Sources utilisées :

- NVIDIA NVAPI, avec `NvAPI_DISP_GetDisplayIdByDisplayName` puis `NvAPI_Disp_GetVRRInfo`, quand l'écran est piloté par NVIDIA et que le pilote expose ces appels ;
- Windows Settings, uniquement sous forme de bouton vers `ms-settings:display-advancedgraphics`.

Windows documente le support VRR côté applications DirectX et un interrupteur utilisateur dans les paramètres graphiques, mais WattPilot n'utilise pas de clé de registre non documentée pour lire ou modifier cet état.

Différences utiles :

- Windows VRR : option système qui aide certains jeux DirectX plein écran ne prenant pas nativement en charge le VRR.
- NVIDIA G-Sync : technologie NVIDIA pour synchroniser l'affichage avec le rendu sur écrans compatibles.
- G-Sync Compatible : écran Adaptive Sync validé ou utilisé par le pilote NVIDIA sans module G-Sync matériel dédié.
- Adaptive Sync : capacité standard côté écran, souvent exposée via DisplayPort ou HDMI selon le matériel.

États prévus :

- `Unknown` ;
- `NotSupported` ;
- `SupportedDisabled` ;
- `SupportedEnabled` ;
- `GSyncEnabled` ;
- `GSyncCompatibleEnabled` ;
- `AdaptiveSyncEnabled` ;
- `VrrEnabled`.

`NvAPI_Disp_GetVRRInfo` expose surtout l'état VRR générique : possible, demandé, actif et affichage actuellement en mode VRR. Il ne permet pas toujours de distinguer de façon certaine G-Sync, G-Sync Compatible et Adaptive Sync. Dans ce cas, WattPilot affiche `VrrEnabled` ou `SupportedDisabled` plutôt qu'une technologie trop précise.

En `Canicule` ou `VideoSurf`, si VRR/G-Sync est actif, WattPilot peut afficher une recommandation discrète. Il ne désactive rien automatiquement.

WattPilot ne modifie pas globalement G-Sync/VRR. Ces réglages peuvent avoir des effets larges et nécessitent une API officielle, testée et réversible.

L'interface peut ouvrir les paramètres graphiques Windows, le panneau de configuration NVIDIA ou NVIDIA App quand une action manuelle est nécessaire.

## Risques connus

- Certains écrans exposent des modes incomplets ou incohérents.
- Windows peut refuser un mode même s'il apparaît dans la liste.
- Les docks, KVM, adaptateurs HDMI/DisplayPort et configurations multi-écrans peuvent changer les noms de périphériques.
- HDR peut être impossible à lire selon la version de Windows ou le pilote.
- VRR/G-Sync peut rester inconnu si NVAPI n'est pas installé, si le pilote est trop ancien ou si l'écran n'est pas piloté par NVIDIA.
- Un rollback peut échouer si l'écran a disparu entre le snapshot et la restauration.

La règle produit reste conservatrice : sans snapshot, sans mode supporté ou sans test réussi, WattPilot n'applique pas de changement écran.
