# LifeOS — Livraison aliments et tickets

Le semainier manuel alimentation/sport est conservé. Le lot approuvé du
11 octobre 2026 remplace le catalogue unifié, les parcours stock/courses et
le frontend Vue ; [périmètre](01-scope-and-versions.md), [vocabulaire](02-core-model.md).

| Lot | Livrable |
| --- | --- |
| Modèle | Aliments génériques avec complétion manuelle ; produits commerciaux explicitement liés ; migration transactionnelle additive depuis UnifiedProducts. |
| Consommateurs | Recettes et nouvelles lignes de repas sur aliments ; snapshots inchangés ; coûts non inventés ; réutilisation sans dépendance au stock. |
| Tickets | Source brute, lignes persistantes ordonnées, corrections et contexte magasin/date ; validation individuelle transactionnelle, achats/prix et idempotence. |
| Angular | Aliments au premier plan ; produits/prix secondaires ; Tickets import/liste/détail reprenable ; dialogues natifs et mobile sans table horizontale. |
| Suppressions | Routes/UI/API/DTO/ports/repositories/DI stock et courses ; catalogues alimentaires parallèles et OFF actifs ; Vue et ses dépendances propres. |
| Livraison | Tests domaine/API PostgreSQL, migration vierge/upgrade/rollback/isolation/reprise/concurrence, contrôles Angular et inspection desktop/mobile ; workflows Angular/API. |

## Publication et autorisations

Les merges automatiques sont explicitement autorisés **après** contrôles et
traitement des revues, sans contourner les protections ou ignorer CI rouge.
Surveiller la publication des deux images API/Angular du commit fusionné avant
de signaler qu'elles sont disponibles. Le refresh des containers est effectué par
l'utilisateur ; sauvegarde PostgreSQL préalable nécessaire pour la migration au
démarrage API. Retour arrière par restauration, pas par suppression de données.

Les validations locales utilisent une authentification synthétique en environnement
Testing et PostgreSQL éphémère. Elles ne prouvent ni OAuth réel ni fonctionnement
du VPS après refresh. Aucune intervention manuelle en production n'est autorisée.

## Hors livraison

OCR/IA, enrichissement santé, rapprochement par nom, choix de marque optimisé,
stock/courses, recommandations médicales, connecteurs sportifs et refonte du
planning restent exclus. Toute évolution future exige un besoin produit explicite.
