# ADR 0006 — Aliments génériques, produits et tickets manuels

Statut : accepté et implémenté, 11 octobre 2026. **Remplace explicitement ADR 0005**,
dont le texte et la migration `UnifiedProducts` restent inchangés comme preuve historique.

## Décision et vocabulaire

L'aliment est la référence culinaire générique, indépendante des marques et magasins.
Il porte les informations nutritionnelles et digestives **manuelles**. Le produit
commercial est une référence de prix secondaire, liée à un seul aliment ; un aliment
peut avoir zéro à plusieurs produits. Un ingrédient est une ligne aliment/quantité/unité,
pas un troisième catalogue. Un composant réutilisable est une recette/préparation.

Navigation principale Aliments, recettes sur aliments, section « Produits et prix »
dans la fiche aliment ; produits aussi sélectionnables depuis Tickets.
Nutrition : base et quatre macros nullable ; FODMAP bas/modéré/élevé nullable,
NOVA 1..4 nullable, notes facultatives. « À compléter » si un des sept champs requis
manque. Zéro est valide, les sauvegardes partielles sont autorisées. Aucune recommandation
médicale, nutrition par marque ou recherche/enrichissement OFF actif.

## Tickets et transactions

`POST /api/receipts` accepte `{json: "<document>"}`. Angular lit un fichier JSON
ou un texte collé. Limites : 1 Mo UTF-8, 500 lignes, profondeur JSON 32.
`magasin`, date **jj/MM/aaaa**, compteurs, tableau `aliments` et mentions textuelles
`lignes_non_alimentaires_exclues` sont archivés avec le document brut.
Les lignes alimentaires gardent leur ordre, les mentions suivent dans l'ordre fourni :
leur position physique d'origine n'est pas connue et n'est pas inventée.
Une structure illisible annule l'import ; une ligne exploitable incomplète reste
en attente avec diagnostic. Les champs santé ne quittent jamais l'archive source.

L'import ne crée aucun aliment, produit, achat, magasin ou prix. Magasin/date sont
confirmés séparément. Chaque ligne a un statut `pending`, `validated` ou `ignored`.
Une correction reste en attente. La validation individuelle, après choix explicite,
crée/lie si nécessaire le produit/aliment, puis l'achat, l'observation et le statut
dans une transaction. Le ticket est verrouillé avant lecture des lignes. Contrainte
unique achat/ligne et observation/ligne ; double clic et concurrence ne doublent rien.
Une ligne validée et son contexte sont figés. Ignorer ne crée rien. SHA-256 du
document exact unique par foyer ; aucune fusion de JSON légèrement différents.
Les répétitions restent des achats distincts, éventuellement vers le même produit.
L'API et les FK composites isolent foyers, magasins, tickets et produits/aliments.

## Prix et coûts

`prix_achat` est le total de ligne. Le prix observé est total / quantité :
3,15 € / 1,125 kg = 2,80 €/kg ; 3,90 € / 2 unités = 1,95 €/unité.
Le sachet d'emmental 400 g reste 5,95 €/sachet ; la conversion 14,875 €/kg
n'existe qu'avec contenance confirmée. Une boîte inconnue garde son prix/boîte.
Le libellé peut proposer une contenance, jamais la confirmer.
Écart total/prix annoncé bloquant au-delà de 0,01 € + quantité × 0,005 €
(arrondis centimes), sauf correction ou confirmation explicite ; pas de remise inférée.

Chaque observation fige sa base et sa provenance. Minimum informatif : dernière
observation par couple produit/magasin puis minimum **par base** kg/L/pièce réellement
comparable. Pas de minimum global entre dimensions ni de prix zéro par défaut.
Date, magasin, produit et provenance sont visibles. Ce minimum ne sélectionne pas
un produit pour la recette. Aucun produit de référence commercial n'était explicitement
choisi dans les recettes historiques : coût non calculable, sans choix de marque implicite.
Le calcul historique conserve nutrition/diversité et mesure la réutilisation des
aliments entre repas, sans stockCoverage ni promesse de fraîcheur/restes/gaspillage.

## Migration et livraison

`FoodsAndReceipts` est additive et transactionnelle. `products` conserve IDs,
archivages, descriptions, anciens champs nutrition/OFF non mappés, prix, dates,
alias et sources. `foods` reçoit une correspondance par identité prouvée
(nutrition/base, recette/repas ou correction historique), sans fusion de noms.
`food_product_mappings` est une preuve de migration, pas un catalogue.
Les produits historiques sans preuve alimentaire restent sans lien, exclus des
nouvelles recettes. Références de recette/repas rebranchées via ce mapping ;
valeurs/horaires/portions/snapshots inchangés. Toute référence ambiguë/inter-foyers
annule la migration. `legacy_articles`, stock/courses et leurs données restent
non mappés ; APIs/ports/repositories/DI/UI stock/courses sont supprimés.
Angular reste `src/frontend-angular`, Vue et ses fichiers propres sont supprimés.

Sauvegarde PostgreSQL obligatoire avant mise à jour. Migrations automatiques au
démarrage API sauf `SkipDatabaseMigration=true`. Retour arrière par restauration
de la sauvegarde, pas par reconstruction destructive. Tests sur PostgreSQL vierge
et upgrade représentatif, rollback, isolation, reprise/concurrence et snapshots.
La publication des images ne constitue pas un déploiement vérifié ; le refresh
des containers appartient à l'utilisateur.
