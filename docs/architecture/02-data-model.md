# LifeOS — Modèle de données initial (conceptuel)

Ce document décrit le modèle de données conceptuel visé pour la base
PostgreSQL cible. Il est volontairement **non final** : il sert de point de
départ pour la roadmap technique (voir `docs/03-roadmap.md`) et sera affiné
jalon par jalon. Il ne prescrit pas un schéma SQL exact (noms de colonnes,
types précis, index) mais les entités, leurs relations et les invariants
attendus.

## Principes transverses

- **Isolation par foyer** : tout agrégat racine porte une colonne
  `household_id` (clé étrangère vers `households`), utilisée pour filtrer
  systématiquement les requêtes côté API. Voir
  `docs/architecture/decisions/0003-household-isolation.md`.
- **Relations fortes par défaut** : les associations structurantes (repas ↔
  recette, planning ↔ repas, article ↔ magasin) sont modélisées par des
  clés étrangères, pas par des tableaux JSON.
- **`jsonb` réservé au flexible** : métadonnées libres de recette, payload
  brut Open Food Facts, explications de scénario, snapshots figés. Le
  `jsonb` ne remplace jamais une relation qui doit rester interrogeable et
  contrainte.
- **Unités et portions explicites** : toute quantité porte une unité
  (`g`, `ml`, `piece`, ...) et les conversions ne sont appliquées que
  lorsqu'elles sont sûres (cohérentes avec `docs/01-scope-and-versions.md`).

## Entités

### `households`
Le foyer, racine d'isolation de toutes les données métier.
- `id`
- `name` (libre, ex. « Foyer de … »)
- horodatages de création/mise à jour

### `household_members`
Association utilisateur Supabase ↔ foyer, avec rôle.
- `id`
- `household_id` → `households`
- `supabase_user_id` (le `sub` du JWT Supabase)
- `role` (`owner` au MVP ; `member` réservé aux évolutions futures)
- Invariant : un `supabase_user_id` donné résout vers au plus un foyer actif
  au MVP (un utilisateur = un foyer propriétaire).

### `member_profiles`
Profil de consommation au sein d'un foyer (ex. un enfant, un adulte), utilisé
pour le calcul des portions.
- `id`
- `household_id` → `households`
- `display_name`
- `portion_coefficient` (coefficient de portion habituel, ajustable par
  repas au niveau de `planned_meal_parts`)

### `food_items`
Article alimentaire de référence : produit Open Food Facts (partagé ou mis en
cache localement) ou article créé manuellement.
- `id`
- `household_id` (nullable si article partagé issu d'Open Food Facts ;
  renseigné pour un article créé manuellement par un foyer — la politique
  exacte de partage vs. propriété par foyer reste à trancher lors du jalon
  « bibliothèque alimentaire »)
- `off_barcode` (nullable, code-barres Open Food Facts)
- `name`
- `reference_unit` (`kilogram`, `liter`, `unit`, ...)
- `nutrition_per_reference_unit` (calories, protéines, glucides, lipides)
- `off_payload` (`jsonb`, réponse brute Open Food Facts en cache, si
  applicable)
- `is_correction` (indique une correction locale d'une donnée Open Food
  Facts, traçable)

### `recipes`
Recette : ingrédients, étapes, portions de référence.
- `id`
- `household_id` → `households`
- `name`
- `servings` (portions de référence)
- `duration_minutes`
- `tags` (souple, ex. `transportable`, `express`, `végétarien`)
- `metadata` (`jsonb`, champs libres/structurés non contraints : étapes,
  notes, source)

### `recipe_ingredients`
Ligne d'ingrédient d'une recette.
- `id`
- `recipe_id` → `recipes`
- `food_item_id` → `food_items`
- `quantity`, `unit`

### `composed_meals`
Repas composé : assemblage de recettes et/ou composants formant un repas
planifiable en tant qu'unité.
- `id`
- `household_id` → `households`
- `name`

### `composed_meal_parts`
Partie d'un repas composé, référence une recette (un composant réutilisable
est modélisé comme une `recipe` marquée réutilisable — pas d'entité séparée
pour éviter une duplication de concept).
- `id`
- `composed_meal_id` → `composed_meals`
- `recipe_id` → `recipes`
- `quantity_factor` (multiplicateur de portions de référence)

### `weeks`
Semaine planifiée, conservée après son passage, duplicable.
- `id`
- `household_id` → `households`
- `starts_on` (date de début de semaine)
- `status` (ex. `draft`, `active`, `past`)

### `day_plans`
Journée d'une semaine.
- `id`
- `week_id` → `weeks`
- `date`
- `work_context` (ex. `home`, `office`, `off`)
- `bike_commute` (booléen)

### `planned_meals`
Repas planifié dans une journée (petit-déjeuner, déjeuner, dîner, collation).
- `id`
- `day_plan_id` → `day_plans`
- `meal_type` (`breakfast`, `lunch`, `dinner`, `snack`)
- `status` (`planned`, `consumed`, `replaced`, `skipped`)
- `composed_meal_id` → `composed_meals` (nullable si repas planifié
  directement à partir d'une seule recette)
- `recipe_id` → `recipes` (nullable, alternative à `composed_meal_id` pour un
  repas planifié simple)
- Invariant : exactement un de `composed_meal_id` / `recipe_id` est renseigné.

### `planned_meal_parts`
Portion calculée par membre pour un repas planifié — nécessaire dès que
plusieurs membres avec des coefficients de portion différents partagent un
`planned_meal`.
- `id`
- `planned_meal_id` → `planned_meals`
- `member_profile_id` → `member_profiles`
- `portion_multiplier` (résolu à partir du coefficient du profil et du
  contexte du repas)

### `activity_sessions`
Séance sportive ou trajet vélo déclaré pour une journée.
- `id`
- `day_plan_id` → `day_plans`
- `type` (ex. `run`, `bike`, `strength`, ...)
- `intensity`
- `duration_minutes`
- `estimated_energy_kcal` (dérivé, calculé côté API à partir des paramètres
  configurables type/intensité)

### `stock_items`
Quantité en stock déclarée manuellement pour un foyer.
- `id`
- `household_id` → `households`
- `food_item_id` → `food_items`
- `quantity`, `unit`

### `shopping_list_items`
Ligne de liste de courses, consolidée depuis le planning et le stock.
- `id`
- `household_id` → `households`
- `food_item_id` → `food_items`
- `quantity_needed`, `unit`
- `is_checked` (coché sur mobile)

### `stores`
Magasin, tel qu'existant aujourd'hui côté frontend (`GroceryStore`), à porter
en base.
- `id`
- `household_id` → `households`
- `name`
- `address` (nullable)

### `price_observations`
Observation de prix d'un article dans un magasin à une date donnée
(équivalent porté de `GroceryPriceEntry`).
- `id`
- `food_item_id` → `food_items`
- `store_id` → `stores`
- `price`
- `observed_on` (date)

### `week_scenarios`
Proposition de semaine générée, classée selon un objectif explicite, brouillon
jusqu'à application.
- `id`
- `household_id` → `households`
- `week_id` → `weeks` (nullable tant que le scénario n'est pas appliqué)
- `ranking_criterion` (ex. `nutritional_balance`, `cost`, `waste_reduction`)
- `explanation` (`jsonb`, hypothèses et compromis exposés à l'utilisateur —
  contenu explicatif intrinsèquement flexible)
- `status` (`draft`, `applied`, `discarded`)

## État d'implémentation — Jalon 1 (fondations foyer + persistance)

Le Jalon 1 a introduit `households`, `household_members` et `member_profiles`
tels que décrits ci-dessus (table par table, colonnes identiques), avec une
précision : `household_members.supabase_user_id` porte un **index unique**
en base (et pas seulement un invariant applicatif), pour garantir qu'un
utilisateur Supabase ne peut jamais se retrouver rattaché à deux foyers
même en cas de requêtes concurrentes lors du provisionnement à la demande
(voir `docs/architecture/decisions/0003-household-isolation.md`).

Ce même jalon a migré les domaines **Stores** et **Articles** (déjà
existants côté in-memory) vers PostgreSQL, en conservant leur forme
historique plutôt qu'en les remodélisant immédiatement selon `food_items` /
`price_observations` ci-dessus :

- `stores` : conforme au modèle documenté (`id`, `household_id`, `name`,
  `address`).
- `articles` (et non `food_items`) : reprend la forme actuelle du domaine
  `GroceryItem` du frontend/backend existant — `id`, `household_id`, `name`,
  `unit`, sans `off_barcode` / `off_payload` / `nutrition_per_reference_unit`
  pour l'instant. L'intégration Open Food Facts et la fusion éventuelle avec
  `food_items` restent prévues pour le jalon « bibliothèque alimentaire »
  (Jalon 3) — `articles` est un point de départ concret, pas le schéma
  final.
- `article_price_entries` (et non `price_observations`) : historique de prix
  porté comme entité *owned* d'`articles` (une ligne par observation, avec
  `store_id`, `price`, `observed_on`), équivalent fonctionnel de
  `price_observations` mais rattaché directement à l'article plutôt qu'à un
  `food_item` séparé.

Cette divergence est volontaire (cf. consigne du jalon : « conserver les
contrats existants autant que possible ») et reste cohérente avec les ADR :
l'isolation par `household_id` est appliquée dès ce jalon sur ces tables, et
la fusion vers le modèle `food_items` cible se fera au jalon dédié plutôt que
de bloquer ce jalon sur un remodelage complet.

## État d'implémentation — Jalons 2 à 6

Les jalons suivants ont persisté en PostgreSQL (via EF Core) l'ensemble des
domaines household-scopés restants : `recipes`, `recipe_ingredients`,
`composed_meals`, `composed_meal_parts`, `weeks`, `day_plans`,
`planned_meals`, `planned_meal_parts`, `week_scenarios`, `food_items`,
`user_configurations`, `activity_sessions`, `planning_rules`,
`frequency_rules`, `week_contexts`, `stock_items` et `shopping_list_items`.
Seule la bibliothèque partagée (`meal_components`, `composite_dishes`,
`activities` au sens « catalogue », à ne pas confondre avec
`activity_sessions`) reste en mémoire, en lecture seule.

Quelques écarts assumés par rapport au modèle conceptuel ci-dessus, à garder
en tête pour éviter des migrations correctives inutiles :

- `food_items.household_id` est **non nullable** (contrairement à la
  question laissée ouverte plus haut) : chaque `food_item`, y compris ceux
  mis en cache depuis Open Food Facts, appartient à un foyer précis — il n'y
  a pas de partage inter-foyers au stade actuel. Une éventuelle bascule vers
  un cache global partagé nécessiterait une vraie migration de données, pas
  seulement un changement de nullabilité.
- `recipe_ingredients.food_item_id` référence en réalité `articles`
  (`GroceryItem`), pas `food_items` : la fusion mentionnée dans la section
  Jalon 1 n'a pas eu lieu. Le nom de la propriété domaine (`FoodItemId`)
  reste historique/générique ; la clé étrangère réelle en base pointe vers
  `articles`. `stock_items` et `shopping_list_items` sont, eux, cohérents
  avec cet état de fait : leur colonne est nommée (et typée)
  `grocery_item_id` → `articles` directement, sans passer par le nom
  `food_item_id`. Ceci est une dette de nommage à surveiller sur
  `recipe_ingredients`, pas un bug de schéma — reste cohérent tant que
  l'unification `articles`/`food_items` n'est pas décidée.
- `food_items.is_correction_of` référence une autre ligne de `food_items`
  (auto-référence) ; supprimer l'original met `is_correction_of` à `NULL`
  sur la correction plutôt que de la supprimer en cascade ou de bloquer la
  suppression (`ON DELETE SET NULL`).

Ces deux derniers points sont verrouillés par la suite de tests
`FoodItemSchemaConstraintsTests` (`src/backend/tests/LifeOS.Api.IntegrationTests`).



- Toute table listée avec `household_id` doit être filtrée par ce foyer à
  chaque requête API (voir ADR 0003).
- Les quantités portent systématiquement une unité explicite ; les
  conversions ne sont admises qu'entre unités compatibles et sûres.
- `planned_meals.status` et `week_scenarios.status` pilotent des machines à
  états simples ; les transitions exactes seront précisées lors des jalons
  correspondants de la roadmap technique.

## Non-buts de ce document

- Ce n'est pas un schéma SQL final : les types précis, index, et contraintes
  `CHECK` seront définis dans les migrations EF Core au moment de
  l'implémentation de chaque domaine.
- La question du partage inter-foyers de `food_items` issus d'Open Food
  Facts (cache global vs. par foyer) reste ouverte et sera tranchée au
  jalon « bibliothèque alimentaire ».
