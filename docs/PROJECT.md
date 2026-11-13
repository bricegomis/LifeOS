# LifeOS — État actuel

Mis à jour le 2026-11-13 (module Sport). Ce document dit ce qui existe réellement dans le code.
Il est mis à jour à chaque livraison. Pour la cible, voir la
[vision](00-vision.md), l'[ADR 0009](architecture/decisions/0009-modular-monolith-and-planning-core.md)
et la [roadmap](01-roadmap.md).

## Où en est-on

Étape 1, « Alimentation saine », livrée le 2026-11-12
([ADR 0010](architecture/decisions/0010-simplified-food-module.md)). En cours : étape 1 bis,
assistant Claude ([ADR 0011](architecture/decisions/0011-conversational-assistant-mcp.md)) ;
la connexion Google par l'API (sans Supabase), l'endpoint MCP et les outils de tout le
module Alimentation sont livrés, ainsi que le classement des aliments
([ADR 0012](architecture/decisions/0012-food-classification.md)). Pour connecter Claude : [assistant-claude.md](assistant-claude.md).
Étape 2, « Sport », livrée en code le 2026-11-13 ([ADR 0013](architecture/decisions/0013-sport-module.md)) :
séances réalisées, objectifs de la semaine, défis du jour et outils de l'assistant ; reste
l'usage réel pendant quelques semaines.

## Ce qui existe

| Domaine | Fonctionnalités |
| --- | --- |
| Compte | Connexion Google gérée par l'API, réservée aux adresses autorisées ; l'API émet ses propres jetons (accès 1 h, rafraîchissement par rotation) |
| Assistant Claude | Endpoint MCP `/mcp` pour claude.ai et Claude Code, avec consentement à la première connexion ; outils pour lire et remplir les modules Alimentation et Sport (aliments avec image par URL et classement, recettes avec nutrition et coût, magasins, produits, prix, avis, tickets) ; accès révocables dans Réglages |
| Foyer | Foyer créé à la première connexion, membres, profils avec coefficient de portion ; page Paramètres (profils, versions de l'interface et de l'API) |
| Aliments | Fiches génériques avec photo, famille obligatoire, rôles dans l'assiette, conservation, mois de saison et étiquettes du foyer (classement modifiable sur ordinateur, lu sur téléphone) ; nutrition manuelle (base 100 g / 100 ml / 1 pièce, calories, protéines, glucides, lipides, fibres, FODMAP, NOVA), état « À compléter », prix relevés en lecture seule, archivage ; liste filtrable par famille, rôle, conservation, saison du mois et étiquette |
| Recettes | Photo, ingrédients sur aliments en g, ml ou pièce (chaque ingrédient ouvre la fiche de l'aliment et affiche sa saison du mois, avec un message pour les ingrédients hors saison ; choix d'un ingrédient dans une liste filtrable, de saison d'abord), rendement, étapes, durée, catégories, nutrition par portion marquée partielle si une valeur manque ; coût minimum–maximum tous magasins confondus, calculé à la lecture, avec le détail du produit, du magasin et du prix retenus |
| Avis | Note 0..10 et commentaire, par aliment ou recette, par profil |
| Produits et prix | Produit toujours complet (aliment, nom, contenance) ; prix payé par conditionnement, par magasin et par date, ramené au kilo, au litre ou à la pièce ; tableau avec une colonne par magasin |
| Magasins | Création, modification, suppression (refusée tant que des prix ou tickets y sont rattachés) |
| Tickets | Photo conservée, allégée dans l'appli avant l'envoi (JPEG, 2560 px au plus ; 1600 px pour les photos d'aliments et de recettes), magasin et date d'achat facultatifs, état à traiter ou traité ; sur ordinateur, saisie des prix à côté de la photo ; aucune lecture automatique |
| Sport | Séances types (sport d'une liste fixe, durée, distance, calories proposées) ; séances réalisées à une date par profil (calories saisies, inconnues = total partiel, distance, ressenti, fréquence cardiaque, notes) ; objectifs de la semaine (par séance type, par sport, nombre de séances ou volume) ; défis du jour en répétitions ou minutes, remplis par séries, avec série de jours, jokers, calendrier, totaux et paliers ; pages Aujourd'hui, Historique, Progression et Réglages. Seul le profil principal est affiché |

Navigation : sur ordinateur, menu à gauche repliable avec un bloc par domaine,
Paramètres et compte en bas ; sur téléphone, onglets Recettes, Aliments, Sport,
Tickets, Réglages, et des sous-onglets en haut des pages Sport. Chaque liste commence par une zone de filtre.

Modèle : [02-core-model.md](02-core-model.md). Code découpé en modules (ADR 0009) :
Foyer, Alimentation et Sport, chacun avec son schéma PostgreSQL ; tests d'architecture
côté backend et Angular.

## Ce qui n'existe pas

Semainier (retiré, [ADR 0008](architecture/decisions/0008-remove-week-planner.md)),
import Garmin ou Strava, tâches, santé, propositions, agent IA, agenda externe, mode hors
ligne. Stock, liste de courses et enrichissement externe sont hors périmètre.

## Écarts connus avec la cible

- iPhone : le widget « prochain événement » est vide et son code Swift reste à
  retirer ; voir [ios-v1.md](ios-v1.md). Traité à l'étape 6.

## Technique

Angular 22 (`src/frontend-angular`), aussi empaqueté pour iPhone avec Capacitor 7.
API ASP.NET Core / EF Core / PostgreSQL (`src/backend`). L'API connecte avec Google
et émet ses propres jetons ([ADR 0011](architecture/decisions/0011-conversational-assistant-mcp.md),
[guide](google-oauth.md)). Schéma d'ensemble : [architecture/overview.md](architecture/overview.md).

Une fusion dans `main` publie les images Docker, que le serveur récupère seul :
c'est un déploiement. Les migrations s'appliquent au démarrage de l'API ;
l'utilisateur sauvegarde PostgreSQL et les volumes de fichiers avant.
