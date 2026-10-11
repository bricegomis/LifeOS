# LifeOS — Concepts du semainier et des achats

Le foyer isole toutes les données métier. Supabase authentifie ; l'API/PostgreSQL
stocke les données. [ADR 0006](architecture/decisions/0006-foods-products-receipts.md)
remplace le produit unifié de l'ADR 0005.

| Terme | Définition | Exemple |
| --- | --- | --- |
| Aliment | Référence culinaire générique, nutrition et digestion manuelles, sans marque/magasin/conditionnement. | Emmental râpé ; haricots rouges en conserve égouttés. |
| Produit | Référence commerciale alimentaire liée à un aliment, marque/code-barres facultatifs, prix observés. | Sachet d'emmental de 400 g d'une marque donnée. |
| Ingrédient | Ligne de recette : aliment + quantité + unité, pas un catalogue. | 100 g d'emmental. |
| Recette | Ingrédients, étapes, durée et nombre positif de portions. | Lasagnes pour quatre portions. |
| Composant | Recette/préparation réutilisable, pas un catalogue parallèle. | Légumes rôtis réutilisés dans plusieurs repas. |
| Repas composé | Assemblage de recettes/préparations et de leurs portions. | Poulet rôti, riz et légumes rôtis. |
| Ticket | Document d'achat daté, magasin, source brute et lignes persistées avant traitement. | Biocoop Nantes Sud, 11 avril 2026. |
| Achat | Événement issu d'une ligne validée : produit, quantité, unité, montant total payé. | Un sachet payé 5,95 €. |
| Prix observé | Prix par base explicite, magasin/date/provenance, jamais prix actuel garanti. | 5,95 €/sachet ; 14,875 €/kg si 400 g confirmés. |

Un aliment a zéro à plusieurs produits ; chaque nouveau produit appartient à un
aliment. Les anciens produits sans preuve alimentaire restent à lier manuellement,
jamais convertis ou fusionnés par nom. « Article » n'est plus un concept actif.
Les recettes et nouvelles lignes de repas sélectionnent des aliments, pas des marques.
Quantité achetée, contenance, quantité de recette et base nutritionnelle sont distinctes.

## Complétion manuelle

Base nutritionnelle, calories/protéines/glucides/lipides, FODMAP bas/modéré/élevé et
NOVA 1..4 sont nullable. L'aliment reste **À compléter** si l'un de ces sept champs
manque ; zéro macro est valide. Notes digestives facultatives, sauvegarde partielle
possible, pas de recommandation médicale ni de santé importée depuis un ticket.
L'archive peut conserver des sources antérieures, sans enrichissement automatique.

## Ticket et prix

Import JSON fichier/texte : source brute, lignes ordonnées et mentions non alimentaires
conservées sans montant inventé. Aucun produit/aliment/prix à l'import. Ligne en attente,
validée ou ignorée ; progression dérivée et reprise depuis l'API.
Correction et validation **individuelles** ; achat et observation transactionnels,
un achat par ligne, répétitions distinctes, réimport identique détecté par foyer.

Minimum informatif dans Aliments : dernière observation de chaque couple
produit/magasin puis minimum par base réellement comparable (kg, L, pièces confirmées
comparables). Paquets convertibles seulement avec contenance confirmée ; aucune
densité, masse d'une pièce ou poids égoutté inventé. Date et provenance visibles.
Ce prix ne choisit jamais un produit pour une recette ; coût non calculable sans
référence commerciale explicite sûre.

## Planning préservé

Semaine = sept journées indépendantes, plusieurs événements alimentation/sport,
horaires civils et chevauchements permis. Les événements historiques sans heure
restent à positionner. Recette : quantités / portions de référence × portion personnelle.
Préparation = quantités personnelles × (1 + 0,5 × enfants), sans augmenter les macros
personnelles. Les snapshots restent figés après modification/archivage des catalogues.
Le sport conserve des calories manuelles totales, distinctes des apports.
Les totaux nutritionnels partiels sont explicites ; conversions sûres uniquement.
Stock et liste de courses ne font plus partie du MVP.
