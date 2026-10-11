# LifeOS — Concepts du semainier et des achats

Le foyer isole toutes les données métier. Supabase authentifie ; l'API/PostgreSQL
stocke les données. [ADR 0006](architecture/decisions/0006-foods-products-receipts.md)
remplace le produit unifié de l'ADR 0005.

> **Modèle actif :** voir
> [ADR 0007](architecture/decisions/0007-recipes-meal-lines-and-household-reviews.md).
> Les repas composés et la portion unique au repas sont abandonnés. Un repas
> contient plusieurs lignes de recettes ou de préparations ponctuelles, chacune
> avec son rendement, ses portions et sa répartition éventuelle par profil. Les
> avis d'aliments et de recettes sont indépendants et foyer-scopés. Le semainier
> reste manuel ; aucun menu généré ni liste de courses active n'est exposé.

| Terme | Définition | Exemple |
| --- | --- | --- |
| Aliment | Référence culinaire générique, nutrition et digestion manuelles, sans marque/magasin/conditionnement. | Emmental râpé ; haricots rouges en conserve égouttés. |
| Produit | Référence commerciale alimentaire liée à un aliment, marque/code-barres facultatifs, prix observés. | Sachet d'emmental de 400 g d'une marque donnée. |
| Ingrédient | Ligne de recette : aliment + quantité + unité, pas un catalogue. | 100 g d'emmental. |
| Recette | Préparation de bibliothèque, simple ou complète, avec ingrédients, rendement et catégories facultatives. | Légumes vapeur ; dhal avec riz ; chia-coco. |
| Préparation ponctuelle | Même contenu structuré qu'une recette, mais persisté uniquement dans le repas qui la contient. | Salade saisie directement pour le dîner. |
| Repas planifié | Créneau d'une journée contenant zéro ou plusieurs lignes de recettes/préparations et de portions indépendantes. | Poulet vapeur, légumes vapeur et dessert. |
| Avis | Note entière facultative 0..10 et/ou commentaire, par aliment/recette et personne/groupe du foyer. | Avis individuel sur la carotte ou avis commun « Enfants ». |
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

## Planning manuel actif

La semaine comprend sept journées indépendantes ; le calendrier, les horaires
civils, chevauchements permis et événements sportifs sont préservés. Les repas
contiennent des lignes de recettes/préparations dont le total est explicite ou
dérivé des répartitions par profil. Nutrition et quantités requises affichées
par ingrédient utilisent le facteur `portions planifiées / rendement`.
Les inconnues restent visibles et aucune conversion de pièce en poids n'est
inventée. Il n'y a pas de calcul agrégé de liste de courses.

Le sport conserve ses valeurs manuelles totales, distinctes des apports. La
modification d'une recette ne réécrit pas le contenu déjà planifié ; les
préparations ponctuelles restent attachées à leur repas et sont copiées lors de
la duplication d'une semaine. Les anciennes données, règles et scénarios du
modèle culinaire abandonné ne sont pas convertis ni réactivés.
