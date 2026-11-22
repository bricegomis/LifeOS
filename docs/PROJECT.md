# LifeOS — État actuel

Mis à jour le 2026-11-22 (module Tâches : todos et routines du foyer). Ce document dit ce qui existe réellement dans le code.
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
séances réalisées, objectifs de la semaine, défis du jour et outils de l'assistant ; routines
quotidiennes et bibliothèque d'exercices ajoutées le 2026-11-16
([ADR 0014](architecture/decisions/0014-sport-daily-routines.md)), puis passages de routine,
ressenti et une page par élément le même jour ([ADR 0015](architecture/decisions/0015-sport-pages-and-routine-sessions.md)) ;
reste l'usage réel pendant quelques semaines.
Étape 3, « Tâches », commencée le 2026-11-22 ([ADR 0017](architecture/decisions/0017-tasks-module.md)) :
maquette validée, todos et routines du foyer livrés en code ; restent les projets et les
outils de l'assistant.

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
| Sport | Séances types (sport d'une liste fixe, durée, distance, calories proposées) ; séances réalisées à une date par profil (calories saisies, inconnues = total partiel, distance, ressenti, fréquence cardiaque, notes) ; objectifs de la semaine (par séance type, par sport, nombre de séances ou volume) ; défis du jour en répétitions ou minutes, remplis par séries, avec série de jours, jokers, calendrier, totaux et paliers ; exercices du foyer (photo choisie dès la création, consigne, tenue en secondes ou répétitions, charge habituelle facultative, vide = poids du corps) ; routines quotidiennes par profil (jours de la semaine, exercices ordonnés avec leur cible) faites en passages, plusieurs par jour, une coche par série par un bouton compteur, effort et gêne notés en fin de passage à partir de valeurs proposées réglables, série et jokers par routine (jour fait dès un passage terminé), calendrier avec le nombre de passages, progression par exercice avec les dernières coches ; une page par élément (Exercices, Routines, Défis, Objectifs modifiables, Séances types) avec une fenêtre de modification, plein écran sur téléphone ; pages Aujourd'hui, Historique et Progression. Seul le profil principal est affiché |
| Tâches | Todos du profil principal : titre, photo facultative (sinon l'initiale du titre), échéance facultative, durée estimée, priorité (haute, normale, basse), étiquettes libres du foyer, notes et checklist de sous-tâches ; un todo ouvert dont l'échéance est passée est en retard jusqu'à ce qu'il soit fait, reporté ou abandonné ; fait, rouvert, abandonné. Routines du foyer (mêmes champs, sans checklist) à date fixe (tous les N jours, certains jours de la semaine, un jour du mois) ou à délai après le dernier passage ; une seule échéance ouverte à la fois, passages faits (jour, note) ou sautés, série et meilleure série, pause et arrêt. Page Aujourd'hui (todos et routines : en retard, aujourd'hui, 7 prochains jours, « Sans échéance » replié, filtre par étiquette), page Toutes les tâches (recherche texte sans accents ni majuscules, filtres par type, état et étiquette), page Routines, une page par todo et par routine |

Navigation : un menu à gauche avec un groupe par domaine (Alimentation, Sport, Tâches) qui s'ouvre sur
ses pages, Paramètres et compte en bas. Une barre du haut fixe porte le bouton menu : sur
ordinateur le menu reste affiché (repliable par ce bouton), sur téléphone il s'ouvre en panneau
et la barre rappelle la page affichée. Seul le contenu de la page défile. Plus d'onglets en bas. Tous les écrans (Alimentation, Sport, Tâches, Paramètres, connexion) utilisent
Angular Material 3 avec le thème LifeOS et les icônes Material Symbols
([ADR 0016](architecture/decisions/0016-angular-material-ui-kit.md)) : chaque liste commence
par une zone de recherche et de filtres, chaque création ou modification s'ouvre dans une
fenêtre, plein écran sur téléphone. L'interface passe en mode sombre quand le système
(ordinateur ou iPhone) l'est.

Modèle : [02-core-model.md](02-core-model.md). Code découpé en modules (ADR 0009) :
Foyer, Alimentation, Sport et Tâches, chacun avec son schéma PostgreSQL ; tests d'architecture
côté backend et Angular.

## Ce qui n'existe pas

Semainier (retiré, [ADR 0008](architecture/decisions/0008-remove-week-planner.md)),
import Garmin ou Strava, projets (tâches), santé, propositions, agent IA, agenda externe, mode hors
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
