# ADR 0012 — Classement des aliments

Statut : accepté, 2026-11-13. Complète l'[ADR 0010](0010-simplified-food-module.md)
avant le lot suivant de l'étape 1 bis. Maquette validée le même jour (canvas de
l'étape 1, version 10).

## Contexte

Un aliment n'avait qu'un nom, sa nutrition, sa digestion et une photo. Rien ne
permettait de le retrouver par nature (« un légume », « une protéine ») ni de le
préférer à un autre au moment d'écrire une recette. Exemple : en décembre, pour un
wok de légumes, on évite le poivron frais, hors saison, mais un poivron surgelé
reste une bonne option.

## Décision

Un aliment porte cinq informations de classement, gérées comme le reste de sa fiche
(écran, API, assistant). Aucune n'entre dans l'état « À compléter », réservé à la
nutrition.

| Champ | Valeurs | Obligatoire |
| --- | --- | --- |
| Famille | Ce qu'est l'aliment, une valeur : Légume, Fruit, Céréale et féculent, Légumineuse, Viande, Poisson et fruits de mer, Œuf, Produit laitier, Matière grasse, Fruit à coque et graine, Herbe et épice, Condiment et sauce, Produit sucré, Boisson, Autre. | oui |
| Rôles | Ce qu'il apporte à l'assiette, zéro à plusieurs : Protéine, Féculent, Légume, Matière grasse, Assaisonnement, Sucré. | non |
| Conservation | Frais, Surgelé, Conserve ou Sec. | non |
| Mois de saison | Janvier à décembre, en France métropolitaine. Aucun mois : pas saisonnier, disponible toute l'année. | non |
| Étiquettes | Libres, propres au foyer, 20 au plus de 50 caractères ; une seule orthographe par étiquette, sans tenir compte de la casse. | non |

- **Famille et rôle sont deux axes.** Le tofu est une légumineuse de rôle Protéine ;
  les lentilles ont les rôles Protéine et Féculent ; l'emmental est un produit laitier
  de rôles Protéine et Matière grasse. Le rôle ne se déduit ni de la famille ni des
  macronutriments : c'est un choix saisi, comme le reste de la fiche.
- **Listes fixes** pour la famille, les rôles et la conservation : codes stables en
  base et dans l'API (`vegetable`, `protein`, `frozen`…), libellés français à
  l'écran. Le futur semainier et l'agent pourront s'en servir (« ce repas n'a pas de
  protéine ») ; des étiquettes libres ne le permettraient pas.
- **La saison se calcule à la lecture** à partir du mois en cours : « De saison »,
  « Toute l'année » ou « Hors saison ». Rien n'est stocké ni masqué.
- **Pas de stock.** Savoir ce qu'il y a au congélateur reste hors périmètre. Un
  aliment surgelé est un aliment distinct (« Poivron en lanières surgelé »,
  conservation Surgelé, aucun mois), comme « haricots rouges en conserve ». Rien
  n'est rapproché par nom.

### Écrans

- **Liste des aliments** : filtres Famille, Rôle, Conservation, Saison (de saison ce
  mois-ci, toute l'année, hors saison) et Étiquette ; colonnes Famille et Saison.
- **Création** : nom et famille.
- **Fiche aliment** : bloc Classement modifiable sur ordinateur (rôles et mois en
  boutons, étiquettes avec suggestions du foyer) ; en lecture seule sur téléphone.
- **Recette** : badge de saison par ingrédient et message « En décembre, 1 ingrédient
  est hors saison : Poivron. » ; le choix d'un ingrédient se fait dans une liste
  cherchable, filtrable par famille et rôle, triée de saison d'abord (puis toute
  l'année, puis hors saison).

### Assistant

`create_food` et `update_food` prennent la famille (obligatoire), les rôles, la
conservation, les mois et les étiquettes ; `search_foods` filtre par famille, rôle,
conservation, étiquette et mois (aliments de saison ce mois-là ou non saisonniers).
Le classement d'une bibliothèque existante se fait par l'assistant, jamais par l'API
(ADR 0011).

### Données existantes

La famille est obligatoire en base. Plutôt que de gérer des aliments sans famille,
la migration `FoodClassification` vide les aliments (choix de l'utilisateur ; quatre
aliments en production, sans produit, recette ni photo), avec ce qui pointe vers eux :
avis d'aliments, produits et prix, ingrédients de recettes.

## Conséquences

- La fiche aliment s'allonge d'un bloc ; la création demande une famille.
- Les quatre aliments de production sont à recréer, par l'écran ou l'assistant.
- Ajouter une famille, un rôle ou une conservation demande une migration (contrainte
  de base) et la mise à jour des listes du domaine, du frontend et de l'aide de
  l'assistant (un test vérifie cette dernière).
