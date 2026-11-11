# LifeOS — Concepts de l'alimentation

Modèle en vigueur, fixé par
[ADR 0006](architecture/decisions/0006-foods-products-receipts.md) et
[ADR 0007](architecture/decisions/0007-recipes-meal-lines-and-household-reviews.md).
Il sera revu au cadrage de l'étape 1 de la [roadmap](01-roadmap.md).

Le foyer isole toutes les données métier. Supabase authentifie ; l'API et
PostgreSQL stockent les données.

| Terme | Définition | Exemple |
| --- | --- | --- |
| Aliment | Référence culinaire générique, nutrition et digestion manuelles, sans marque, magasin ni conditionnement. | Emmental râpé ; haricots rouges en conserve égouttés. |
| Produit | Référence commerciale liée à un aliment, marque et code-barres facultatifs, prix observés. | Sachet d'emmental de 400 g d'une marque donnée. |
| Ingrédient | Ligne de recette : aliment + quantité + unité. Pas un catalogue. | 100 g d'emmental. |
| Recette | Préparation de bibliothèque, simple ou complète, avec ingrédients, rendement et catégories facultatives. | Légumes vapeur ; dhal avec riz. |
| Avis | Note entière facultative 0..10 et/ou commentaire, par aliment ou recette, et par personne ou groupe du foyer. | Avis commun « Enfants » sur la carotte. |
| Ticket | Document d'achat daté, avec magasin, source brute et lignes conservées avant traitement. | Biocoop Nantes Sud, 11 avril 2026. |
| Achat | Événement issu d'une ligne validée : produit, quantité, unité, montant total payé. | Un sachet payé 5,95 €. |
| Prix observé | Prix par base explicite, avec magasin, date et provenance. Jamais un prix actuel garanti. | 5,95 €/sachet ; 14,875 €/kg si 400 g confirmés. |

Un aliment a zéro à plusieurs produits ; chaque produit créé appartient à un aliment.
Les recettes choisissent des aliments, jamais des marques. Quantité achetée,
contenance, quantité de recette et base nutritionnelle sont quatre notions distinctes.
Rien n'est rapproché par nom.

## Nutrition manuelle

Base nutritionnelle, calories, protéines, glucides, lipides, FODMAP
(bas, modéré, élevé) et NOVA (1 à 4) peuvent être vides. L'aliment reste
**À compléter** tant qu'un de ces sept champs manque. Zéro est une valeur valide.
Les notes digestives et l'image sont facultatives et n'entrent pas dans la complétion.
Aucune recommandation médicale, aucune donnée de santé importée depuis un ticket.

Dans une recette : `quantité de l'ingrédient × portions / rendement`.
Une valeur inconnue reste inconnue et rend le total incomplet.

## Tickets et prix

Un ticket importé (JSON ou photo) conserve sa source brute et toutes ses lignes.
L'import ne crée ni aliment, ni produit, ni prix. Chaque ligne est en attente,
validée ou ignorée. Les lignes d'un ticket JSON se valident une par une ; un
ticket photo se valide en bloc. Les deux sont transactionnels et idempotents ;
un réimport identique est détecté par foyer.

Prix observé = total de la ligne / quantité. Le minimum affiché sur un aliment
prend le dernier relevé de chaque couple produit/magasin, puis compare uniquement
des bases réellement comparables : kg, L, pièces confirmées. Un paquet n'est
convertible qu'avec une contenance confirmée à la main. Aucune densité, masse de
pièce ou poids égoutté n'est inventé. Ce minimum est informatif : il ne choisit
jamais un produit pour une recette.
