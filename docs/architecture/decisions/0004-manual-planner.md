# ADR 0004 — Transition vers le semainier manuel

Statut : accepté et implémenté, 2026-10-07.

## Contrats et temps

Réutiliser `weeks`, `day_plans`, `planned_meals` et `activity_sessions`.
L'API manuelle expose semaines complètes et commandes typées alimentation/sport,
sans créer un bus d'événements ou un modèle JSON générique.
Date civile portée par `day_plans.date`, début/fin en minutes locales depuis
minuit (0–1440, fin strictement supérieure au début). Pas de séance traversant
minuit dans ce MVP : utiliser deux occurrences si nécessaire.
Le fuseau IANA de la semaine est enregistré à sa création (défaut Europe/Paris) ;
les horaires sont des intentions civiles, pas des instants UTC à décaler lors
d'un changement de navigateur. Les horodatages techniques restent UTC.
La plage 6–20 est uniquement UI. Chevauchements permis.

Colonnes horaires nullables sans backfill : l'historique est à positionner.
Créer une semaine manuelle ignore totalement alternance, travail et règles,
initialise sept dates et interdit le calcul/application automatique sur cette
semaine. Les anciens endpoints/données restent disponibles hors parcours.

## Produits et achats

**Remplacé par [ADR 0005](0005-unified-products.md), 9 octobre 2026 :**
produit canonique unique nutrition/achats, FK unifiées et migration des anciens
liens explicites. Le paragraphe suivant décrit la décision initiale, non le
modèle actif.

Conserver les FK historiques `recipe_ingredients.FoodItemId → articles`.
Ajouter un raccord explicite optionnel `food_items.ArticleId → articles`,
unique lorsqu'il est renseigné, contrôlé dans le même foyer. Aucune fusion par
nom ni modification des prix. Pour sélectionner un produit dans une recette,
l'utilisateur le relie à un article existant ou crée explicitement cet article.
Les produits directs d'un repas référencent `food_items`, pas les articles.
Archiver produits/recettes/sport plutôt que supprimer les modèles référencés.
Les unités compatibles g/100g/kg et ml/100ml/l sont les seules conversions
d'échelle ; aucune conversion entre dimensions ou pièce/masse.

## Repas et snapshots

Ajouter des lignes relationnelles `meal_food_lines` avec FK produit facultative,
nom, quantité/unité de référence personnelle, unité nutritionnelle et quatre
valeurs nutritionnelles nullables copiées. Les recettes sont décomposées en
lignes pour une portion de référence (`quantité recette / servings`).
Une occurrence multiplie ces lignes par sa portion personnelle positive.
Les ingrédients sont persistés à six décimales ; les quantités de snapshot à
douze décimales, arrondies avant calcul/sauvegarde pour accepter les portions
fractionnaires (ex. une recette de trois portions). Portion personnelle à quatre
décimales. Les bornes sont validées avant écriture, sans débordement SQL.
Les repas directs peuvent porter plusieurs lignes. Nutrition personnelle seule ;
préparation multiplie par `1 + 0,5 × enfants`, entier non négatif.
Modifier heure, portion ou enfants conserve le snapshot. Changer le contenu
exige un remplacement explicite ; archiver/éditer les catalogues ne le modifie pas.
Un total est marqué incomplet si une ligne/valeur/conversion manque, avec motifs.

Les anciens repas composés sont affichés avec leurs recettes/ingrédients et
peuvent être positionnés manuellement. Comme leur nutrition passée n'a jamais
été figée, on ne prétend pas la reconstruire : le premier placement capture les
valeurs actuelles, avec information explicite. Aucun repas historique n'est supprimé.

## Sport

Table `sport_templates` par foyer ; pas de seed/import silencieux des exemples.
Occurrence : FK modèle facultative, nom/type/intensité/durée/distance/calories
copiés. Édition limitée à l'occurrence. Calories totales manuelles non négatives,
jamais automatiquement proportionnelles. Intensités low/moderate/high.

## Validation et migration

Migrations EF additives uniquement ; aucune heure inventée ni suppression de table.
Vérifier schéma vierge et upgrade de données historiques, persistance après
redémarrage, deux foyers, références croisées rejetées, snapshots, unités inconnues,
chevauchements, hors plage, multiples séances, portions/enfants et erreurs API.
Auth Supabase/JWT et résolution de foyer inchangées. Les protections de merge
et CI restent applicables à l'autorisation de fusion automatique.

Migrations livrées : `ManualFoodCatalog`, `SportLibrary`, `ManualTimedEvents`,
`PlannerQuantityPrecision`. Cette dernière augmente seulement la précision des
quantités existantes. Les tests exercent une base vierge et une base historique
arrêtée à `SeparateWeekTypes`, prix/repas composés/séances compris.
