# LifeOS — Modèle de l'alimentation

Modèle de l'étape 1, fixé par
[ADR 0010](architecture/decisions/0010-simplified-food-module.md) et implémenté dans le
module Alimentation (schéma PostgreSQL `food`). Ce que contient l'application est
résumé dans [PROJECT.md](PROJECT.md).

Le foyer isole toutes les données métier. Supabase authentifie ; l'API et
PostgreSQL stockent les données.

| Terme | Définition | Exemple |
| --- | --- | --- |
| Aliment | Référence culinaire générique, nutrition et digestion manuelles, sans marque, magasin ni conditionnement. | Emmental râpé ; haricots rouges en conserve égouttés. |
| Recette | Préparation de bibliothèque : ingrédients, rendement en portions, instructions, durée, catégories et image facultatives. | Légumes vapeur ; dhal avec riz. |
| Ingrédient | Ligne de recette : aliment + quantité + unité. Pas un catalogue. | 100 g d'emmental. |
| Avis | Note entière facultative 0..10 et/ou commentaire, par aliment ou recette, et par profil du foyer. | Avis de Léa sur la carotte. |
| Magasin | Lieu d'achat du foyer. | Biocoop Nantes Sud. |
| Produit | Référence commerciale liée à un aliment, avec une contenance obligatoire (masse, volume ou nombre de pièces) ; marque et code-barres facultatifs. | Sachet d'emmental de 400 g d'une marque donnée. |
| Prix observé | Prix d'un produit dans un magasin à une date, saisi à la main, sur une base explicite. Jamais un prix actuel garanti. | 5,95 € le sachet de 400 g, soit 14,875 €/kg. |
| Ticket | Photo d'un ticket de caisse, conservée telle quelle, à traiter ou traitée. | Photo du 11 avril 2026, Biocoop. |
| Coût de recette | Fourchette minimum–maximum calculée à la lecture, tous magasins confondus. Jamais stockée. | Dhal : de 4,40 € à 7,20 €. |

Un aliment a zéro à plusieurs produits ; chaque produit appartient à un aliment.
Les recettes choisissent des aliments, jamais des marques. Contenance d'un produit,
quantité d'une recette et base nutritionnelle sont trois notions distinctes.
Rien n'est rapproché par nom.

## Nutrition manuelle

Base nutritionnelle, calories, protéines, glucides, lipides, fibres, FODMAP
(bas, modéré, élevé) et NOVA (1 à 4) peuvent être vides. L'aliment reste
**À compléter** tant qu'un de ces huit champs manque. Zéro est une valeur valide.
Les notes digestives et l'image sont facultatives et n'entrent pas dans la complétion.
Aucune recommandation médicale.

Dans une recette : `quantité de l'ingrédient × portions / rendement`.
Une valeur inconnue reste inconnue et rend le total incomplet.

## Avis

Un avis par profil et par aliment ou recette, modifiable et supprimable. Zéro est
une vraie note, distincte de « non noté ». L'avis d'une recette est indépendant des
avis de ses aliments ; aucune moyenne ni score n'est calculé.

## Tickets

Un ticket est une photo déposée depuis l'application. Il porte sa date de dépôt, un
magasin et une date d'achat facultatifs, et un état : à traiter ou traité. Le
déposer ne crée ni produit, ni prix ; rien n'est lu automatiquement. Les prix sont
saisis à la main sur ordinateur, en regardant la photo.

## Prix et coût de recette

Un produit n'existe que complet : sa contenance est obligatoire, donc tout prix
relevé se ramène à un prix au kilo, au litre ou à la pièce. Un prix observé fige
cette base au moment du relevé. Aucune densité, masse de pièce ou poids égoutté
n'est inventé.

Coût d'une recette : pour chaque ingrédient, le dernier prix relevé de chaque couple
produit/magasin de l'aliment, sur une base comparable avec l'unité de l'ingrédient.
Le moins cher donne le minimum, le plus cher le maximum ; la recette additionne les
minimums d'un côté, les maximums de l'autre. Un ingrédient sans prix comparable
reste inconnu et rend les deux totaux incomplets. La fourchette ne reflète que les
produits enregistrés : n'enregistrer que des produits bio donne un minimum bio.
Le calcul est informatif : il ne choisit jamais un produit pour la recette.
