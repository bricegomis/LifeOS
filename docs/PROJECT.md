# LifeOS — État actuel

Mis à jour le 2026-11-11. Ce document dit ce qui existe réellement dans le code.
Il est mis à jour à chaque livraison. Pour la cible, voir la
[vision](00-vision.md), l'[ADR 0009](architecture/decisions/0009-modular-monolith-and-planning-core.md)
et la [roadmap](01-roadmap.md).

## Où en est-on

Aucune étape de la roadmap n'est commencée. Prochaine action : cadrage de
l'étape 1, « Alimentation saine ».

## Ce qui existe

| Domaine | Fonctionnalités |
| --- | --- |
| Foyer | Foyer créé à la première connexion, membres, profils avec coefficient de portion, groupes nommés |
| Aliments | Fiches génériques, nutrition manuelle (base, 4 macros, FODMAP, NOVA), filtre « À compléter », image facultative, archivage |
| Produits et prix | Produits commerciaux liés à un aliment, prix observés par magasin, minimum par base comparable |
| Magasins | Création, modification, suppression |
| Tickets | Import JSON validé ligne par ligne ; photo avec OCR local Tesseract, validée en bloc |
| Recettes | Ingrédients sur aliments, rendement, étapes, durée, catégories ; session de cuisine sur iPhone |
| Avis | Note 0..10 et commentaire, par aliment ou recette, par profil ou groupe |
| Sport | Séances types uniquement (nom, sport, durée, distance, intensité, calories) |

Modèle détaillé : [02-core-model.md](02-core-model.md), ADR 0006 et ADR 0007.

## Ce qui n'existe pas

Semainier (retiré, [ADR 0008](architecture/decisions/0008-remove-week-planner.md)),
séances réalisées, tâches, santé, propositions, agent IA, agenda externe, mode hors
ligne. Stock, liste de courses et enrichissement externe sont hors périmètre.

## Écarts connus avec la cible

- Le backend est découpé par couche, pas par module, et les règles métier sont
  mêlées au code HTTP des endpoints. Mise en conformité : étape 1.
- Points ouverts de la revue de code : [audit-2026-11.md](audit-2026-11.md).
- La base contient encore des tables d'anciens modèles, non mappées par le code
  (stock, courses, anciennes configurations).
- iPhone : le widget « prochain événement » est vide et son code Swift reste à
  retirer ; voir [ios-v1.md](ios-v1.md). Traité à l'étape 6.

## Technique

Angular 22 (`src/frontend-angular`), aussi empaqueté pour iPhone avec Capacitor 7.
API ASP.NET Core / EF Core / PostgreSQL (`src/backend`). Supabase sert uniquement à
l'authentification. Schéma d'ensemble : [architecture/overview.md](architecture/overview.md).

Une fusion dans `main` publie les images Docker, que le serveur récupère seul :
c'est un déploiement. Les migrations s'appliquent au démarrage de l'API ;
l'utilisateur sauvegarde PostgreSQL et les volumes de fichiers avant.
