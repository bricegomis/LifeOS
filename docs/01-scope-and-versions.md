# LifeOS — MVP alimentation, semainier et tickets manuels

## Périmètre actuel

Angular est le seul frontend ; API ASP.NET Core/PostgreSQL, auth Supabase/JWT et
isolation par foyer. [ADR 0006](architecture/decisions/0006-foods-products-receipts.md)
remplace le catalogue unifié de l'ADR 0005.

- Bibliothèque **Aliments** génériques au premier plan, recherche, archives et
  filtre « À compléter » avec champs manquants. Nutrition générique manuelle :
  base, calories/protéines/glucides/lipides, FODMAP bas/modéré/élevé et NOVA 1..4 nullable.
  Notes digestives facultatives ; zéro valide, sauvegarde partielle possible.
- Produits commerciaux secondaires, zéro à plusieurs par aliment, depuis sa fiche
  ou un ticket. Prix historiques par magasin avec unités et provenance. Minimum
  par base comparable parmi les derniers prix de chaque couple produit/magasin.
  Contenance manuellement confirmée pour les paquets ; pièces comparables seulement
  avec confirmation. Aucun choix automatique de produit pour une recette.
- Recettes : lignes d'aliments/quantités/unités, étapes, portions et durée.
  Un composant est une préparation réutilisable, pas un catalogue parallèle.
- Import ticket JSON fichier/texte, archive brute et lignes avant tout traitement.
  Date jj/MM/aaaa, sélection/création explicite magasin, lignes en attente/validées/
  ignorées, correction et progression persistantes. Validation individuelle et
  transactionnelle : achat et prix, éventuelle création aliment/produit choisi.
  Répétitions distinctes, idempotence, reprise, réimport identique signalé par foyer.
  Mentions non alimentaires conservées puis ignorables, sans valeurs inventées.
- Semainier manuel alimentation/sport, sept journées indépendantes, plusieurs
  événements, desktop 6–20 et journée mobile, chevauchements et hors plage accessibles.
  Recette ou lignes d'aliments, portion personnelle et préparation enfants séparées.
- Sport : catalogue et occurrences, calories totales manuelles, durée/intensité/
  distance optionnelle, sans recalcul automatique des calories.

## Conservation et calculs

Quantités préparées = quantités personnelles × (1 + 0,5 × enfants).
Les enfants n'augmentent pas la nutrition personnelle. Snapshots, horaires,
portions et historiques conservés lors des éditions/archives et migrations.
Les anciens événements sans heure restent à positionner, jamais horaires inventés.
Nutrition inconnue : totaux incomplets explicites. Pas de conversion masse/volume/
pièce sans information sûre. Coût non calculable sans produit de référence
explicitement choisi ; le minimum observé n'est pas ce choix.

Migration additive depuis `UnifiedProducts`, mapping par preuve alimentaire et
identité, aucun rapprochement de noms. Anciens produits non alimentaires conservés
sans générer d'aliments. Stock/courses restent en tables historiques non mappées.
Sauvegarde avant migration en production ; retour arrière par restauration.

## Exclusions

Stock et liste de courses **entièrement retirés** des interfaces/API/calculs du MVP.
Pas d'OCR/IA, scan caméra, santé importée, enrichissement OFF automatique, nutrition
par marque, conseils médicaux, conversion inventée ou optimisation commerciale.
Semaines types, alternance, génération/récurrence/règles sont hors parcours manuel ;
leurs données/API historiques sont conservées sans influencer les nouvelles semaines.
Pas de refonte visuelle indépendante ni de frontend Vue.

## Critères d'acceptation

Exemple 21 lignes alimentaires + deux mentions conservées, zéro catalogue/prix
créé à l'import ; validation existant/nouveau/liaison, lignes répétées distinctes,
unités oranges/avocats/paquets/boîtes, correction explicite des écarts.
Reprise, rollback, isolation foyers, concurrence/idempotence/réimport vérifiés.
Complétion null/zéro/base/macros/FODMAP/NOVA et notes facultatives.
Migrations vierge et historique, alias/homonymes/sources/prix/snapshots inchangés.
Interfaces desktop/mobile lisibles ; stocks/courses/Vue/enrichissement absents.
La publication des images ne vaut pas déploiement ou recette Supabase réelle.
