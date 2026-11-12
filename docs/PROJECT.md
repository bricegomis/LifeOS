# LifeOS — État actuel

Mis à jour le 2026-11-12. Ce document dit ce qui existe réellement dans le code.
Il est mis à jour à chaque livraison. Pour la cible, voir la
[vision](00-vision.md), l'[ADR 0009](architecture/decisions/0009-modular-monolith-and-planning-core.md)
et la [roadmap](01-roadmap.md).

## Où en est-on

Étape 1, « Alimentation saine », en cours. Cadrage
([ADR 0010](architecture/decisions/0010-simplified-food-module.md)), maquette, suppressions,
mise en module et refonte du modèle et des écrans sont faits. Reste le coût de recette
(fourchette minimum–maximum), puis la fusion de l'étape dans `main`.

## Ce qui existe

| Domaine | Fonctionnalités |
| --- | --- |
| Foyer | Foyer créé à la première connexion, membres, profils avec coefficient de portion ; page Paramètres (profils, versions de l'interface et de l'API) |
| Aliments | Fiches génériques avec photo, nutrition manuelle (base 100 g / 100 ml / 1 pièce, calories, protéines, glucides, lipides, fibres, FODMAP, NOVA), état « À compléter », prix relevés en lecture seule, archivage |
| Recettes | Photo, ingrédients sur aliments en g, ml ou pièce (chaque ingrédient ouvre la fiche de l'aliment), rendement, étapes, durée, catégories, nutrition par portion marquée partielle si une valeur manque |
| Avis | Note 0..10 et commentaire, par aliment ou recette, par profil |
| Produits et prix | Produit toujours complet (aliment, nom, contenance) ; prix payé par conditionnement, par magasin et par date, ramené au kilo, au litre ou à la pièce ; tableau avec une colonne par magasin |
| Magasins | Création, modification, suppression (refusée tant que des prix ou tickets y sont rattachés) |
| Tickets | Photo conservée, magasin et date d'achat facultatifs, état à traiter ou traité ; sur ordinateur, saisie des prix à côté de la photo ; aucune lecture automatique |
| Sport | Séances types uniquement (nom, sport, durée, distance, intensité, calories) |

Navigation : sur ordinateur, menu à gauche repliable avec un bloc par domaine,
Paramètres et compte en bas ; sur téléphone, onglets Recettes, Aliments, Sport,
Tickets, Réglages. Chaque liste commence par une zone de filtre.

Modèle : [02-core-model.md](02-core-model.md). Code découpé en modules (ADR 0009) :
Foyer, Alimentation et Sport, chacun avec son schéma PostgreSQL ; tests d'architecture
côté backend et Angular.

## Ce qui n'existe pas

Semainier (retiré, [ADR 0008](architecture/decisions/0008-remove-week-planner.md)),
séances réalisées, tâches, santé, propositions, agent IA, agenda externe, mode hors
ligne. Stock, liste de courses et enrichissement externe sont hors périmètre.

## Écarts connus avec la cible

- Coût de recette (ADR 0010, § 5) : pas encore livré.
- La migration `FoodModuleReset` vide la base au premier démarrage qui la contient
  (intégration, puis production à la fusion dans `main`) : voir l'ADR 0010, § 6.
- Points ouverts de la revue de code : [audit-2026-11.md](audit-2026-11.md).
- iPhone : le widget « prochain événement » est vide et son code Swift reste à
  retirer ; voir [ios-v1.md](ios-v1.md). Traité à l'étape 6.

## Technique

Angular 22 (`src/frontend-angular`), aussi empaqueté pour iPhone avec Capacitor 7.
API ASP.NET Core / EF Core / PostgreSQL (`src/backend`). Supabase sert uniquement à
l'authentification. Schéma d'ensemble : [architecture/overview.md](architecture/overview.md).

Une fusion dans `main` publie les images Docker, que le serveur récupère seul :
c'est un déploiement. Les migrations s'appliquent au démarrage de l'API ;
l'utilisateur sauvegarde PostgreSQL et les volumes de fichiers avant.
