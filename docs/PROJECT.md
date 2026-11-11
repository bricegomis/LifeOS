# LifeOS — État actuel

Mis à jour le 2026-11-11. Ce document dit ce qui existe réellement dans le code.
Il est mis à jour à chaque livraison. Pour la cible, voir la
[vision](00-vision.md), l'[ADR 0009](architecture/decisions/0009-modular-monolith-and-planning-core.md)
et la [roadmap](01-roadmap.md).

## Où en est-on

Étape 1, « Alimentation saine », en cours. Le cadrage est fait
([ADR 0010](architecture/decisions/0010-simplified-food-module.md)) et la maquette des
écrans est validée. Lot en cours : suppression du code abandonné. Déjà retiré : la
session de cuisine (application web), les groupes du foyer et les avis de groupe,
l'import JSON de tickets, l'OCR, les lignes de ticket et les achats.

## Ce qui existe

| Domaine | Fonctionnalités |
| --- | --- |
| Foyer | Foyer créé à la première connexion, membres, profils avec coefficient de portion |
| Aliments | Fiches génériques, nutrition manuelle (base, 4 macros, FODMAP, NOVA), filtre « À compléter », image facultative, archivage |
| Produits et prix | Produits commerciaux liés à un aliment, prix observés par magasin, minimum par base comparable |
| Magasins | Création, modification, suppression |
| Tickets | Photo conservée, magasin et date d'achat facultatifs, état à traiter ou traité ; aucune lecture automatique |
| Recettes | Ingrédients sur aliments, rendement, étapes, durée, catégories |
| Avis | Note 0..10 et commentaire, par aliment ou recette, par profil |
| Sport | Séances types uniquement (nom, sport, durée, distance, intensité, calories) |

Modèle du code actuel : ADR 0006 et ADR 0007. Modèle cible de l'étape 1 :
[02-core-model.md](02-core-model.md).

## Ce qui n'existe pas

Semainier (retiré, [ADR 0008](architecture/decisions/0008-remove-week-planner.md)),
séances réalisées, tâches, santé, propositions, agent IA, agenda externe, mode hors
ligne. Stock, liste de courses et enrichissement externe sont hors périmètre.

## Écarts connus avec la cible

- Restent à faire pour l'ADR 0010 : fibres, image de recette, produits toujours
  complets (contenance obligatoire), alias et champs hérités à retirer, écrans
  conformes à la maquette, coût de recette. Les prix se saisissent encore depuis la
  fiche aliment, pas à côté de la photo du ticket.
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
