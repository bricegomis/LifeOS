# Semainier et sport — retour d'expérience et orientation

Rédigé le 2026-11-08, complété le 2026-11-10. Le semainier actuel (ADR 0004,
ADR 0007, `03-week-planner-ux.md`) n'est pas utilisé et ne contient aucune donnée à
conserver. La Live Activity et les widgets iOS qui en dépendent ne fonctionnent pas
correctement.

Décision envisagée : supprimer tout le code du semainier, rendre le sport
utilisable seul, assainir la base aliments/recettes, puis reconstruire un semainier
calculé à partir de règles et de bibliothèques exposées par l'API.

## 1. Pourquoi le semainier n'a pas été utilisé

Retour d'usage :

- **Rien n'est préparé d'avance.** Aucune semaine n'existe tant qu'on ne clique pas
  pour la créer, et elle arrive vide : tout est à saisir à la main.
- **La visualisation ne donne pas envie** de s'en servir.
- **Les catalogues sont confus.** Aliments, produits, articles, recettes et
  préparations ponctuelles se recoupent. Il manque une base saine et simple
  d'ingrédients et de recettes sur laquelle planifier.

Constats tirés du code, qui vont dans le même sens :

- Un repas est un événement d'agenda avec début et fin obligatoires, alors qu'on
  pense « midi » ou « soir ».
- Une seule boîte de dialogue mélange jour, horaires, recette, préparation ponctuelle
  (un deuxième éditeur de recette), rendement, portions, répartition par personne,
  coefficients et avis.
- Modifier un repas impose « Remplacer le contenu » : la rigidité des snapshots se voit.
- Choix par listes déroulantes natives, filtre caché sous « Plus d'options ».
- Trois vues des mêmes données (agenda, grille 6–20 h, détails). Les quantités à
  préparer sont enfouies dans les détails.
- Les avertissements « nutrition incomplète » sont partout, puisque beaucoup
  d'aliments sont « À compléter ».
- On peut dupliquer une semaine entière, mais pas reprendre un repas récent.
- Repas et sport partagent le même modèle « événement ». Une séance n'existe que
  dans une journée du semainier.

## 2. Dette technique abandonnée avec la suppression

Points de `audit-2026-11.md` qui deviennent sans objet : A2 (unicité de semaine et
`IsManual`), A3 (`ActivitySession` sans `household_id`), C1 (environ 45 requêtes pour
lire une semaine), C2 (`SetRecipeLines` requête par ingrédient), D5 (nutrition
calculée dans les DTO de l'API). S'y ajoute un composant de page de 400 lignes avec
un formulaire imbriqué sur trois niveaux.

## 3. Ce qui reste valable

- Isolation par foyer sur chaque ligne, références croisées rejetées.
- Snapshot : modifier une recette ne réécrit jamais ce qui est déjà planifié.
- Quantité à préparer = `quantité de l'ingrédient × portions prévues / rendement`.
- Une valeur inconnue reste inconnue : jamais remplacée par zéro, total marqué incomplet.
- Les repas choisissent des aliments et des recettes, jamais un produit ou une marque.

## 4. Orientation future : semainier calculé

Le prochain semainier ne sera pas saisi à la main. Il sera calculé par une IA
(Claude ou autre) puis poussé dans LifeOS par l'API, à partir de :

- règles définies et stockées dans l'API ;
- la bibliothèque de recettes et d'ingrédients ;
- les séances de sport types et leur récurrence.

L'utilisateur consulte et ajuste ensuite le résultat.

**Changement de périmètre.** Aujourd'hui, la génération automatique et les
règles/scénarios sont explicitement hors périmètre (`CLAUDE.md`, `AGENTS.md`,
`PROJECT.md`) : c'est l'ancien modèle qui a été retiré. Ce retour en arrière devra
être acté par une ADR au moment de la reconstruction. Il faudra distinguer
clairement : le calcul est fait hors de l'API, et l'API valide puis stocke.

Prérequis : base aliments/recettes assainie (§ 6), sport autonome (§ 5), contrat
d'API pour pousser une semaine complète.

## 5. Sport autonome (première étape)

Objectif : un sport utilisable seul, sans semainier, qu'on pourra combiner plus tard.

Besoin exprimé :

- définir des **séances types** (existe déjà : `SportTemplate`, avec nom, sport, durée,
  distance, intensité, calories) ;
- **enregistrer les séances** réalisées à une date, sans semaine ni journée à créer ;
- gérer la **récurrence** (par exemple course le mardi et le jeudi) pour calculer
  les calories ;
- **suivre semaine par semaine** les calories dépensées.

Réponses (2026-11-10) :

- **Confirmation explicite.** La récurrence prévoit des séances attendues. Seules
  les séances confirmées comme faites comptent dans le bilan.
- **Calories saisies par séance.** La valeur de la séance type sert de valeur
  proposée ; la séance confirmée porte sa propre valeur. Aucun calcul automatique
  (pas de formule durée × intensité × poids).
- **Suivi par personne.** Une séance appartient à un profil du foyer. Dans un premier
  temps, seul l'utilisateur principal est suivi, pas les enfants.

Plus tard, idéalement : récupérer les séances et leurs calories depuis Garmin, Strava
ou Apple Santé au lieu de les confirmer à la main. C'est une intégration externe
(OAuth, jetons à stocker, ou HealthKit côté iOS) : elle demandera sa propre ADR et
ne doit pas conditionner la première version. Le modèle doit seulement permettre
d'indiquer la source d'une séance (manuelle ou importée) sans tout refaire.

## 6. Base aliments/recettes à assainir

À préciser : ce qui crée la confusion entre aliments, produits et recettes, et ce
qu'est une base « saine et simple » (par exemple : un ingrédient = un aliment générique ;
les produits ne servent qu'aux prix et restent hors du parcours recette).

## 7. Ordre proposé

1. Supprimer le semainier, y compris les séances rattachées aux journées.
   Conserver les séances types.
2. Sport autonome : journal des séances, récurrence, bilan hebdomadaire.
3. Assainir la base aliments/recettes.
4. ADR et contrat d'API du semainier calculé, puis nouvelle interface.

## 8. Suppression (étape 1) — faite le 2026-11-10

Voir [ADR 0008](architecture/decisions/0008-remove-week-planner.md).

- **Backend** : semaines, journées, repas et lignes planifiés, `ActivitySession`, routes
  `/api/manual-planner/*` et avis de préparations ponctuelles supprimés. Migration
  `RemoveWeekPlanner` volontairement destructive. Séances types conservées
  (`Domain/Sports`).
- **Frontend** : pages Semainier et Aujourd'hui supprimées, l'accueil ouvre les
  recettes. La synchronisation native ne projette plus d'événements et annule les
  anciens rappels ; la session de cuisine est conservée.
- **Reste à faire sous macOS** : retirer le widget « prochain événement » (cible
  `LifeosWidgetExtension`) et décider du sort de la Live Activity de cuisine.
