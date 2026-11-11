# ADR 0008 — Retrait du semainier manuel

Statut : accepté, 2026-11-10. Remplace la partie « semainier » de
l'ADR 0004 (supprimée, voir l'historique git) et les repas planifiés de
[ADR 0007](0007-recipes-meal-lines-and-household-reviews.md).

## Contexte

Le semainier manuel (semaines, journées, repas multi-lignes, préparations
ponctuelles, séances rattachées aux journées) n'était pas utilisé. Il fallait créer
chaque semaine à la main, la visualisation n'incitait pas à s'en servir, et les
catalogues sur lesquels il s'appuie restent confus. La base ne contient que des
données de test. Le widget « prochain événement », la Live Activity et les rappels
locaux qui en dépendaient ne fonctionnaient pas correctement.
Retour d'expérience complet : [semainier-retour-experience.md](../../semainier-retour-experience.md).

## Décision

- Supprimer le semainier de l'API, du domaine, de la base et de l'interface Angular :
  `Week`, `DayPlan`, `PlannedMeal`, lignes, ingrédients et portions planifiés,
  `ActivitySession`, routes `/api/manual-planner/*`, pages Semainier et Aujourd'hui.
- Les avis de recette ciblent toujours une recette : la cible « préparation
  ponctuelle » (`PlannedRecipeLineId`, `/api/reviews/planned-lines/*`) disparaît,
  avec les avis qui la visaient.
- Conserver les séances types (`SportTemplate`, `/api/sport-templates`), déplacées
  dans `Domain/Sports`. Elles serviront de base au sport autonome.
- La page d'accueil ouvre les recettes ; les anciens liens `/planning`, `/today` et
  `/planner` y redirigent.
- Côté iPhone, la synchronisation native ne garde que la purge par identité et la
  restauration de la session de cuisine. Elle publie un instantané de widget sans
  événement et annule les rappels programmés par les versions précédentes. Le code
  Swift du widget et de la Live Activity n'est pas modifié dans cette étape (il ne
  peut être vérifié que sous macOS) ; son retrait fera l'objet d'un changement séparé.

## Migration

`RemoveWeekPlanner` est **volontairement destructive**, par exception à la règle
des migrations additives : aucune donnée du semainier n'est à conserver. Elle
supprime les avis de préparations ponctuelles, la colonne `PlannedRecipeLineId`,
puis les tables `weeks`, `day_plans`, `planned_meals`, `planned_recipe_lines`,
`planned_recipe_line_ingredients`, `planned_recipe_line_portions` et
`activity_sessions` avec `CASCADE`. Les tables historiques non mappées qui
référençaient ces tables (par exemple `shopping_list_items`) gardent leurs lignes ;
seules leurs clés étrangères tombent. `Down` n'est pas supporté : le retour
arrière passe par la restauration d'une sauvegarde pré-migration.

## Conséquences

- Les points A2, A3, C1, C2 et D5 de `audit-2026-11.md` deviennent sans objet.
- Le prochain semainier sera calculé hors de l'API (par une IA) à partir de règles
  et de bibliothèques exposées par l'API, puis poussé via l'API. Ce retour des règles
  et de la génération, aujourd'hui hors périmètre, demandera sa propre ADR.
- Étapes suivantes prévues : sport autonome (journal des séances confirmées,
  récurrence, bilan hebdomadaire par personne), puis assainissement de la base
  aliments/recettes.
