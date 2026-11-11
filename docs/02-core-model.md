# LifeOS — Modèle de l'alimentation

Modèle cible de l'étape 1, fixé par
[ADR 0010](architecture/decisions/0010-simplified-food-module.md). Le code y est
amené pendant l'étape 1 ; ce qu'il contient réellement à un instant donné est dans
[PROJECT.md](PROJECT.md).

Le foyer isole toutes les données métier. Supabase authentifie ; l'API et
PostgreSQL stockent les données.

| Terme | Définition | Exemple |
| --- | --- | --- |
| Aliment | Référence culinaire générique, nutrition et digestion manuelles, sans marque, magasin ni conditionnement. | Emmental râpé ; haricots rouges en conserve égouttés. |
| Recette | Préparation de bibliothèque : ingrédients, rendement en portions, instructions, durée et catégories facultatives. | Légumes vapeur ; dhal avec riz. |
| Ingrédient | Ligne de recette : aliment + quantité + unité. Pas un catalogue. | 100 g d'emmental. |
| Avis | Note entière facultative 0..10 et/ou commentaire, par aliment ou recette, et par profil du foyer. | Avis de Léa sur la carotte. |
| Magasin | Lieu d'achat du foyer. | Biocoop Nantes Sud. |
| Produit | Référence commerciale liée à un aliment, marque et code-barres facultatifs. | Sachet d'emmental de 400 g d'une marque donnée. |
| Prix observé | Prix d'un produit dans un magasin à une date, saisi à la main, sur une base explicite. Jamais un prix actuel garanti. | 5,95 €/sachet ; 14,875 €/kg si 400 g confirmés. |
| Ticket | Photo d'un ticket de caisse, conservée telle quelle, à traiter ou traitée. | Photo du 11 avril 2026, Biocoop. |
| Coût de recette | Indicateur calculé à la lecture, par magasin. Jamais stocké. | Dhal : 4,80 € chez Leclerc, 6,10 € chez Biocoop. |

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
saisis à la main dans l'administration, en regardant la photo.

## Prix et coût de recette

Un prix observé fige sa base : unité d'achat et contenance du produit au moment du
relevé. Bases comparables : kg, L, pièces confirmées. Un paquet n'est convertible
qu'avec une contenance confirmée à la main. Aucune densité, masse de pièce ou poids
égoutté n'est inventé.

Coût d'une recette dans un magasin : pour chaque ingrédient, le dernier prix relevé
de chaque produit de l'aliment dans ce magasin, puis le moins cher sur une base
comparable avec l'unité de l'ingrédient. Un ingrédient sans prix ou sans base
comparable reste inconnu et rend le total incomplet. Le calcul est informatif : il
ne choisit jamais un produit pour la recette.
