# ADR 0005 — Un produit unique pour nutrition et achats

Statut : accepté et implémenté, 9 octobre 2026. Décision explicite de l'utilisateur :
« Un même produit, avec nutrition et achats réunis dans une seule fiche ».
Remplace la séparation aliments/articles de l'ADR 0004.

## Agrégat et contrats

Un seul agrégat par foyer, stocké dans `products`, possède nom, description,
nutrition facultative, quantité/unité de référence nutritionnelle, unité d'achat
et historique de prix par magasin. Le type C# `FoodItem` conserve son nom
historique pour limiter la rupture des contrats ; l'agrégat `GroceryItem` et son
mapping EF sont supprimés. Les ports/DTO d'articles restants sont des adaptateurs
de compatibilité vers ce même agrégat, pas un second catalogue.

L'API et le parcours actifs sont `/api/products` et `/products`. Une création
ou édition rassemble les deux usages. Les magasins restent indépendants ;
leurs prix s'ajoutent dans la fiche produit. Recettes, stock et événements
référencent le même identifiant canonique. `/foods` et `/articles` redirigent
vers Produits. Le raccord `PUT /api/food-items/{id}/article` répond 410 :
il ne peut plus créer/défaire une seconde identité.

Les produits non alimentaires existent sans nutrition ni quantité de référence
inventée. Les imports OFF et anciens aliments non associés ont une unité d'achat
**à préciser** : `PurchaseUnitConfirmed=false`, aucun relevé de prix possible
avant confirmation. OFF reste en référence 100 g selon les champs importés,
jamais automatiquement interprété en litres ou en pièces.

Nutrition et prix ne sont pas deux mesures de la même quantité. Les seules
conversions partagées sont g/100g/kg, ml/100ml/L et unités/pièces compatibles.
Aucune conversion masse/volume/pièce, densité ou taille de conditionnement
inventée. L'unité d'achat ne peut pas changer tant qu'un historique de prix existe.
Les snapshots gardent leurs références et valeurs figées. Les calculs historiques
utilisent l'identité exacte et les conversions compatibles, plus de jointure par nom.
Les courses automatiques sont refusées pour toutes les semaines : aucune liste
historique n'est effacée/recalculée par cette transition.

## Migration et identifiants

`UnifiedProducts` s'exécute dans la transaction EF, sur schéma vierge ou existant :

- Aliment déjà relié : garder son ID et toutes ses données nutritionnelles/OFF,
  corrections et archivage ; intégrer description/unité/prix de l'article exact.
  Un nom d'achat différent reste visible dans `LegacyPurchaseName`.
- Article sans aliment : créer un produit de même ID, sans nutrition.
- Aliment sans article : garder son ID et ses données, sans deviner une unité
  d'achat. Les entrées de même nom restent distinctes. `MigrationOrigin` rend
  ces origines visibles ; l'utilisateur peut les compléter/archiver séparément.
- `product_aliases` garde chaque ID d'article et sa destination canonique avec
  une FK composite contrôlant le foyer. Les anciennes routes d'achats et ajouts
  d'ingrédients/stock acceptent ces alias exacts.
- Rebrancher les FK d'ingrédients, stock, courses et propriétaires des prix sur
  le produit canonique. Garder IDs des lignes, quantités, unités, montants,
  dates et cases cochées. Les FK de repas/corrections gardent les IDs alimentaires :
  aucune valeur de snapshot, heure, portion ou enfant n'est réécrite.
- Conserver intégralement `legacy_articles` et la colonne historique `ArticleId`
  non mappée dans `products` comme preuves de migration. Ces données ne sont
  pas des entités actives et ne sont jamais modifiées par les parcours applicatifs.
  Les anciennes dates/noms d'achat y restent disponibles même s'ils différaient.

Une collision d'IDs non associés ou une référence historique inter-foyers fait
**échouer et annuler** la migration avec diagnostic explicite. Elle exige une
résolution de données préalable, jamais une fusion par nom.

La migration descendante est volontairement refusée : après nouvelles écritures
sur le modèle unifié, reconstruire deux catalogues sans perte serait ambigu.
Avant déploiement, sauvegarder PostgreSQL ; retour arrière par restauration ou
migration inverse étudiée explicitement. Aucun déploiement de production n'est
effectué par les tests locaux.

## Limites et vérification

Pas d'outil de fusion manuelle des anciens homonymes dans ce lot : ils ne sont
pas des doublons présumés et restent des produits autonomes. Pas de modèle
aliment générique/marques, ni de conditionnement/conversion implicite.
Une correction reste un nouveau produit traçable vers l'original ; les prix
historiques ne sont pas recopiés sur ce nouveau produit.

Tests PostgreSQL : upgrade depuis `PlannerQuantityPrecision`, liens existants
et absents, homonymes, autre foyer, produit non alimentaire, OFF/correction/
archivage, prix/dates/IDs, recettes, stock, courses cochées, snapshots précis,
compatibilité des alias après redémarrage et annulation sur données ambiguës.
Tests API : fiche unique, édition/prix, recette/repas, unités incompatibles,
foyers, archivage et maintien des snapshots. Tests Angular : brouillon unique,
nutrition nulle/zéro, unités distinctes et résolution explicite de l'unité d'achat.
