# LifeOS — Concepts du semainier manuel

Ce vocabulaire décrit la cible approuvée ; l'état technique réel est séparé dans
[le modèle de données](architecture/02-data-model.md).

Un **foyer** isole les données métier. Supabase authentifie l'utilisateur,
l'API résout son foyer. Les profils historiques sont conservés, mais la nutrition
du MVP est exclusivement personnelle.

Un **produit** possède dans une seule identité nom, description, nutrition
facultative, quantité de référence nutritionnelle, unité d'achat et prix par
magasin. Un produit non alimentaire peut ne porter aucune nutrition.
Il n'existe plus d'article d'achat indépendant à créer ou relier. Les anciens
liens explicites sont intégrés sans fusion des homonymes non associés ; les
unités d'achat inconnues restent à préciser. Voir [ADR 0005](architecture/decisions/0005-unified-products.md).

Une **recette** possède ingrédients, quantités/unités, étapes et nombre positif
de portions de référence. La portion personnelle d'une occurrence exprime un
nombre de portions de référence ; les quantités de recette sont divisées par
son nombre de portions puis multipliées par cette portion personnelle.
Les composants et repas composés historiques restent accessibles, sans devenir
la structure obligatoire des nouvelles prises alimentaires.

Une **semaine** est une unité datée de sept journées indépendante des autres.
Sa création manuelle n'utilise ni alternance ni règles. Une **journée** accepte
un nombre quelconque d'événements, avec chevauchements permis.

Un **événement alimentaire** porte date, début/fin, recette ou lignes de
produits, portion personnelle et nombre d'enfants. Les quantités personnelles
des lignes sont positives et portent une unité explicite. La préparation
multiplie ces quantités par `1 + 0,5 × enfants`, jamais les macros personnelles.
Les valeurs de référence utilisées sont figées pour préserver l'historique.

Un **modèle sportif** porte nom, sport, durée positive, distance optionnelle,
intensité et calories manuelles. Une **occurrence sportive** copie ses valeurs,
avec créneau et modifications limitées à cette occurrence. Les calories restent
un total saisi, indépendant des modifications de durée ou de distance.

Un événement historique sans horaire est **à positionner**, pas implicitement
placé au petit-déjeuner ou ailleurs. Il reste consultable et modifiable.
Un élément de bibliothèque **archivé** disparaît des nouvelles sélections,
mais ses occurrences conservent leur contenu.

Les **apports personnels** additionnent énergie, protéines, glucides et lipides
connus et signalent explicitement tout total incomplet. La **dépense sportive**
est affichée séparément ; elle n'est pas la dépense quotidienne totale.
Les conversions d'unités ne sont utilisées que si elles sont sûres.
