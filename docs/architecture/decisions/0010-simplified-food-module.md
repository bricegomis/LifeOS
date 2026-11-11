# ADR 0010 — Alimentation simplifiée

Statut : accepté, 2026-11-11. Résultat du cadrage de l'étape 1 de la
[roadmap](../../01-roadmap.md). Remplace, pour les tickets, les achats et les groupes,
les décisions de l'[ADR 0006](0006-foods-products-receipts.md) et de
l'[ADR 0007](0007-recipes-meal-lines-and-household-reviews.md). L'architecture reste
celle de l'[ADR 0009](0009-modular-monolith-and-planning-core.md).

## Contexte

Le but de l'alimentation dans LifeOS est de trouver des compromis pour manger sain :
une recette se juge sur sa nutrition, sa digestibilité, l'avis du foyer et son coût,
qui dépend des produits achetés et des magasins (Leclerc, Biocoop).

Le modèle actuel sert ce but, mais l'interface mélange tout : la saisie des produits
et des prix, qui ne sert qu'à alimenter des calculs, est au même niveau que les
recettes et les aliments utilisés tous les jours. L'import de tickets (JSON validé
ligne par ligne, photo avec OCR Tesseract) représente le plus gros bloc de code et de
règles pour un bénéfice faible.

## Décision

### 1. Ce qui reste, ce qui part

| Objet | Décision |
| --- | --- |
| Aliment | Gardé, avec son image facultative. Les fibres s'ajoutent à la nutrition. |
| Recette | Gardée : ingrédients sur aliments, rendement, instructions, durée, catégories. Une image facultative s'ajoute. |
| Avis | Gardé, par profil uniquement. |
| Produit | Gardé, toujours lié à un aliment et toujours complet (§ 5). |
| Magasin | Gardé. |
| Prix observé | Gardé, saisi à la main. |
| Ticket | Réduit à une photo conservée (§ 4). |
| Coût de recette, minimum et maximum | Nouveau (§ 5). |
| Import JSON de ticket | Supprimé. |
| OCR Tesseract, lignes de ticket, validation | Supprimés. |
| Achat | Supprimé : un prix ne provient plus d'une ligne de ticket. |
| Groupes nommés du foyer et avis de groupe | Supprimés. |
| Session de cuisine | Supprimée de l'application web ; le code Swift est retiré à l'étape 6. |
| Alias de produit, champs hérités des anciens modèles | Supprimés. |

### 2. Deux espaces dans l'interface

- **Usage quotidien**, sur ordinateur comme sur smartphone : recettes, aliments,
  avis, photo d'un ticket. Écrans épurés, une seule chose à faire par écran.
- **Saisie des données de référence**, pensée pour l'ordinateur : magasins, produits, prix, tickets
  à traiter. Elle n'apparaît pas dans la
  navigation du smartphone.

Sur les écrans quotidiens, smartphone compris, les prix restent secondaires et en
lecture seule : la fourchette de coût d'une recette et, sur la fiche aliment, un
résumé des prix relevés. Toute saisie de prix se fait sur ordinateur.

L'image porte l'interface : une image par recette et par aliment, affichée dans les
listes, les fiches et les lignes d'ingrédient. Dans une recette, un ingrédient ouvre
la fiche de son aliment. Les images de recettes suivent les règles des images
d'aliments (JPEG, PNG ou WebP, 10 Mo, route authentifiée et filtrée par foyer) et
sont conservées sur le même volume durable, `lifeos_food_images`.

Navigation sur ordinateur : un menu à gauche, repliable par un bouton burger. Il
contient un bloc par domaine (Alimentation, Sport, puis les suivants) avec une
entrée par page ; le bloc Alimentation réunit recettes, aliments, produits et prix,
magasins et tickets, sans sous-groupe. En bas du menu : l'entrée Paramètres
(profils du foyer, versions de l'interface et de l'API), puis le compte connecté
avec sa photo et la déconnexion.

Chaque page de liste (recettes, aliments, produits) commence par une zone de
filtre : texte, catégories, état ou magasin selon la page. Dans la liste des
produits, l'aliment ouvre sa fiche.

Les écrans sont validés sur une maquette cliquable avant tout code.

### 3. Nutrition

Huit champs, tous facultatifs : base nutritionnelle, calories, protéines, glucides,
lipides, fibres, FODMAP (bas, modéré, élevé), NOVA (1 à 4). L'aliment reste
« À compléter » tant qu'un des huit manque. Zéro est une valeur valide. Les notes
digestives et l'image restent facultatives et hors complétion.

### 4. Ticket : une photo, rien d'autre

- La photo est prise ou choisie depuis l'application, sur smartphone d'abord.
- Le fichier est conservé sur le volume durable `lifeos_receipts` et servi
  uniquement par une route authentifiée, filtrée par foyer.
- Le ticket porte la date de dépôt, un magasin et une date d'achat facultatifs, et
  un état : **à traiter** ou **traité**.
- Sur ordinateur, la liste des tickets à traiter sert de boîte de réception :
  l'utilisateur ouvre la photo, saisit les prix à la main, puis marque le ticket traité.
- Déposer une photo ne crée ni produit, ni prix. Aucune lecture automatique.

Le traitement futur des photos (OCR, modèle de vision ou autre) fera l'objet d'une
ADR dédiée. Il devra respecter l'ADR 0009 : l'API n'appelle aucun fournisseur d'IA.

### 5. Coût d'une recette : minimum et maximum

Les courses se font dans plusieurs magasins à la fois (la farine ici, le chocolat
là). Le coût n'est donc pas calculé par magasin, mais comme une fourchette, tous
magasins confondus. C'est un indicateur calculé à la lecture, jamais stocké, livré
en dernier lot de l'étape 1.

- Pour chaque ingrédient : on prend le dernier prix relevé de chaque couple
  produit/magasin, parmi les produits non archivés de l'aliment, sur une base
  comparable avec l'unité de l'ingrédient. Le moins cher donne le minimum, le plus
  cher le maximum.
- Un produit n'existe que complet : aliment, nom et contenance (masse, volume ou
  nombre de pièces) sont obligatoires à la création. Il n'y a plus de produit « à
  confirmer » : tout prix relevé se ramène à un prix au kilo, au litre ou à la pièce.
- Aucun passage entre masse, volume et pièce n'est inventé : un prix au kilo ne
  chiffre pas un ingrédient compté en pièces.
- Coût de l'ingrédient = quantité × prix sur cette base. Coût minimum de la recette
  = somme des minimums ; coût maximum = somme des maximums ; coût par portion =
  coût / rendement.
- Un ingrédient sans prix comparable reste inconnu et rend les deux totaux
  **incomplets**. Les totaux partiels sont affichés avec le nombre d'ingrédients
  manquants, jamais comme un coût complet.
- Chaque ligne du calcul montre, pour le minimum et pour le maximum, le produit, le
  magasin, le prix et la date du relevé.
- La fourchette reflète uniquement les produits enregistrés. L'utilisateur fixe le
  niveau de qualité du minimum par les produits qu'il saisit : s'il n'enregistre
  que des produits bio pour un aliment, le minimum est un prix bio.

La recette continue de ne référencer que des aliments. Le calcul ne choisit ni ne
mémorise aucun produit pour la recette : il change dès qu'un prix est relevé.

### 6. Données

La base est remise à zéro, production comprise, à la fusion de l'étape 1 dans
`main`. Le foyer est recréé à la première connexion ; aliments, recettes, produits
et prix sont ressaisis. Les tables des anciens modèles (stock, courses,
configurations) disparaissent avec le reste. Les comptes Supabase ne sont pas touchés.

Les photos de tickets et les images d'aliments déjà présentes sur les volumes
deviennent orphelines : les deux volumes peuvent être vidés au même moment, par
l'utilisateur.

## Ordre de mise en œuvre

Déroulé de l'étape 1 dans la [roadmap](../../01-roadmap.md). La migration destructive
est écrite au lot « refonte du modèle », après la mise en module.

Point à trancher à ce lot : puisque la base repart de zéro, les tables du Foyer et
de l'Alimentation peuvent rejoindre leur propre schéma PostgreSQL, comme les nouveaux
modules, au lieu de rester dans `public` (ADR 0009, § 2).

## Conséquences

- L'image Docker de l'API ne contient plus Tesseract.
- `02-core-model.md` décrit le modèle cible ; `PROJECT.md` continue de dire ce que
  le code contient réellement jusqu'à chaque livraison.
- Les tests d'intégration des tickets JSON, de l'OCR et des groupes sont retirés
  avec le code qu'ils couvrent.
