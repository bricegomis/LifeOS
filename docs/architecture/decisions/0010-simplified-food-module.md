# ADR 0010 — Alimentation simplifiée

Statut : accepté, 2026-11-11. Résultat du cadrage de l'étape 1 de la
[roadmap](../../01-roadmap.md). Remplace, pour les tickets, les achats et les groupes,
les décisions de l'[ADR 0006](0006-foods-products-receipts.md) et de
l'[ADR 0007](0007-recipes-meal-lines-and-household-reviews.md). L'architecture reste
celle de l'[ADR 0009](0009-modular-monolith-and-planning-core.md).

## Contexte

Le but de l'alimentation dans LifeOS est de trouver des compromis pour manger sain :
une recette se juge sur sa nutrition, sa digestibilité, l'avis du foyer et son coût,
qui change selon le magasin (Leclerc ou Biocoop).

Le modèle actuel sert ce but, mais l'interface mélange tout : la saisie des produits
et des prix, qui ne sert qu'à alimenter des calculs, est au même niveau que les
recettes et les aliments utilisés tous les jours. L'import de tickets (JSON validé
ligne par ligne, photo avec OCR Tesseract) représente le plus gros bloc de code et de
règles pour un bénéfice faible.

## Décision

### 1. Ce qui reste, ce qui part

| Objet | Décision |
| --- | --- |
| Aliment | Gardé. Les fibres s'ajoutent à la nutrition. |
| Recette | Gardée : ingrédients sur aliments, rendement, instructions, durée, catégories. |
| Avis | Gardé, par profil uniquement. |
| Produit | Gardé, toujours lié à un aliment. |
| Magasin | Gardé. |
| Prix observé | Gardé, saisi à la main. |
| Ticket | Réduit à une photo conservée (§ 4). |
| Coût de recette par magasin | Nouveau (§ 5). |
| Import JSON de ticket | Supprimé. |
| OCR Tesseract, lignes de ticket, validation | Supprimés. |
| Achat | Supprimé : un prix ne provient plus d'une ligne de ticket. |
| Groupes nommés du foyer et avis de groupe | Supprimés. |
| Session de cuisine | Supprimée de l'application web ; le code Swift est retiré à l'étape 6. |
| Alias de produit, champs hérités des anciens modèles | Supprimés. |

### 2. Deux espaces dans l'interface

- **Usage quotidien**, sur ordinateur comme sur smartphone : recettes, aliments,
  avis, photo d'un ticket. Écrans épurés, une seule chose à faire par écran.
- **Administration**, pensée pour l'ordinateur : magasins, produits, prix, tickets
  à traiter. C'est une saisie de données de référence ; elle n'apparaît pas dans la
  navigation du smartphone.

Les écrans quotidiens ne montrent des prix que le résultat : le coût d'une recette.
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
- Dans l'administration, la liste des tickets à traiter sert de boîte de réception :
  l'utilisateur ouvre la photo, saisit les prix à la main, puis marque le ticket traité.
- Déposer une photo ne crée ni produit, ni prix. Aucune lecture automatique.

Le traitement futur des photos (OCR, modèle de vision ou autre) fera l'objet d'une
ADR dédiée. Il devra respecter l'ADR 0009 : l'API n'appelle aucun fournisseur d'IA.

### 5. Coût d'une recette par magasin

Un indicateur calculé à la lecture, jamais stocké, livré en dernier lot de l'étape 1.

- Pour chaque magasin et chaque ingrédient : parmi les produits non archivés de
  l'aliment, on prend le dernier prix relevé de chaque produit dans ce magasin, puis
  le moins cher sur une base comparable avec l'unité de l'ingrédient.
- Bases comparables : masse (g, kg), volume (ml, L), pièces confirmées. Un paquet ne
  se convertit qu'avec une contenance confirmée à la main. Aucun passage entre
  masse, volume et pièce n'est inventé.
- Coût de l'ingrédient = quantité × prix sur cette base. Coût de la recette = somme
  des ingrédients ; coût par portion = coût / rendement.
- Un ingrédient sans prix dans ce magasin, ou sans base comparable, reste inconnu
  et rend le total **incomplet**. Le total partiel est affiché avec le nombre
  d'ingrédients manquants, jamais comme un coût complet.
- Chaque ligne du calcul montre le produit retenu, le prix et la date du relevé.

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
