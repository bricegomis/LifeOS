# LifeOS — État actuel

Mis à jour le 2026-11-26 (accueil « Aujourd'hui » unifié, web app installable ; module Budget : budget mensuel, import OFX, règles de classement, évolution ; contrats de lecture Tâches, Alimentation et Santé pour le futur semainier ; import Garmin : séances avec heures, VO2max, sommeil et récupération, service garmin-sync, page Aujourd'hui). Ce document dit ce qui existe réellement dans le code.
Il est mis à jour à chaque livraison. Pour la cible, voir la
[vision](00-vision.md), l'[ADR 0009](architecture/decisions/0009-modular-monolith-and-planning-core.md)
et la [roadmap](01-roadmap.md).

## Où en est-on

Étape 1, « Alimentation saine », livrée le 2026-11-12
([ADR 0010](architecture/decisions/0010-simplified-food-module.md)). Étape 1 bis,
assistant Claude, livrée en code ([ADR 0011](architecture/decisions/0011-conversational-assistant-mcp.md)) ;
la connexion Google par l'API (sans Supabase), l'endpoint MCP et les outils de tout le
module Alimentation sont livrés, ainsi que le classement des aliments
([ADR 0012](architecture/decisions/0012-food-classification.md)). Pour connecter Claude : [assistant-claude.md](assistant-claude.md).
Étape 2, « Sport », livrée en code le 2026-11-13 ([ADR 0013](architecture/decisions/0013-sport-module.md)) :
séances réalisées, objectifs de la semaine, défis du jour et outils de l'assistant ; routines
quotidiennes et bibliothèque d'exercices ajoutées le 2026-11-16
([ADR 0014](architecture/decisions/0014-sport-daily-routines.md)), puis passages de routine
et une page par élément le même jour (ressenti par exercice retiré le 2026-11-27) ([ADR 0015](architecture/decisions/0015-sport-pages-and-routine-sessions.md)) ;
import Garmin livré le 2026-11-23 ([ADR 0019](architecture/decisions/0019-garmin-import.md)) ;
reste l'usage réel pendant quelques semaines.
Étape 3, « Tâches », commencée le 2026-11-22 ([ADR 0017](architecture/decisions/0017-tasks-module.md)) :
maquette validée ; todos, routines du foyer, projets, outils de l'assistant et contrat de lecture
pour le semainier livrés en code ; reste l'usage réel.
Étape 5, « Santé », avancée avant le semainier ([ADR 0018](architecture/decisions/0018-health-module.md)) :
maquette validée le 2026-11-22 ; compléments, symptômes, analyses, mesures, documents, rendez-vous et
outils de l'assistant livrés ; reste l'usage réel.
Étape 7, « Budget », avancée avant le semainier ([ADR 0020](architecture/decisions/0020-budget-module.md)) :
maquette validée et module livré en code le 2026-11-23 ; reste la mise en place des lignes depuis l'Excel
(par Claude) et l'usage réel. Étapes 8 (Lecture) et 9 (Films et séries) prévues, pas commencées.

## Ce qui existe

| Domaine | Fonctionnalités |
| --- | --- |
| Compte | Connexion Google gérée par l'API, réservée aux adresses autorisées ; l'API émet ses propres jetons (accès 1 h, rafraîchissement par rotation) |
| Assistant Claude | Endpoint MCP `/mcp` pour claude.ai et Claude Code, avec consentement à la première connexion ; outils pour lire et remplir les modules Alimentation, Sport, Tâches, Santé et Budget (la santé du seul compte connecté ; Claude lit les documents, l'API jamais) (aliments avec image par URL et classement, recettes avec nutrition et coût, magasins, produits, prix, avis, tickets) ; accès révocables dans Réglages |
| Foyer | Foyer créé à la première connexion, membres, profils avec coefficient de portion ; page Paramètres (profils, versions de l'interface et de l'API) |
| Aliments | Fiches génériques avec photo, famille obligatoire, rôles dans l'assiette, conservation, mois de saison et étiquettes du foyer (classement modifiable sur ordinateur, lu sur téléphone) ; nutrition manuelle (base 100 g / 100 ml / 1 pièce, calories, protéines, glucides, lipides, fibres, FODMAP, NOVA), état « À compléter », prix relevés en lecture seule, archivage ; liste filtrable par famille, rôle, conservation, saison du mois et étiquette |
| Recettes | Photo, ingrédients sur aliments en g, ml ou pièce (chaque ingrédient ouvre la fiche de l'aliment et affiche sa saison du mois, avec un message pour les ingrédients hors saison ; choix d'un ingrédient dans une liste filtrable, de saison d'abord), rendement, étapes, durée, catégories, nutrition par portion marquée partielle si une valeur manque ; coût minimum–maximum tous magasins confondus, calculé à la lecture, avec le détail du produit, du magasin et du prix retenus |
| Avis | Note 0..10 et commentaire, par aliment ou recette, par profil |
| Produits et prix | Produit toujours complet (aliment, nom, contenance) ; prix payé par conditionnement, par magasin et par date, ramené au kilo, au litre ou à la pièce ; tableau avec une colonne par magasin |
| Magasins | Création, modification, suppression (refusée tant que des prix ou tickets y sont rattachés) |
| Tickets | Photo conservée, allégée dans l'appli avant l'envoi (JPEG, 2560 px au plus ; 1600 px pour les photos d'aliments et de recettes), magasin et date d'achat facultatifs, état à traiter ou traité ; sur ordinateur, saisie des prix à côté de la photo ; aucune lecture automatique |
| Sport | Séances types (sport d'une liste fixe, durée, distance, calories proposées) ; séances réalisées par profil avec heure de début et de fin (durée active, calories saisies ou mesurées par la montre, inconnues = total partiel, distance, effort perçu 1 à 10, sensation en 5 niveaux, fréquence cardiaque, notes) ; séances importées de Garmin (toutes les activités, type Garmin rangé dans un sport, HIIT = crossfit ; dénivelé, vitesse, zones de FC, Training Effect ; valeurs Garmin mises à jour à chaque synchro, notes, séance type et sport changé gardés ; suppression suivie ; une séance manuelle du même sport aux horaires qui se chevauchent fusionne) ; VO2max par jour et par sport, courbe mois par mois ; page Garmin (état de la connexion, code de double authentification saisi dans l'appli, synchro à la demande, jeton d'import) ; objectifs de la semaine (par séance type, par sport, nombre de séances ou volume, les objectifs globaux pouvant exclure des sports comme la marche) ; défis du jour en répétitions ou minutes, remplis par séries, avec série de jours, jokers, calendrier, totaux et paliers ; exercices du foyer (photo choisie dès la création, consigne, tenue en secondes ou répétitions, charge habituelle facultative, vide = poids du corps) ; routines quotidiennes par profil (jours de la semaine, exercices ordonnés avec leur cible) faites en passages, plusieurs par jour, une coche par série par un bouton compteur ou en mode guidé (une série à la fois en circuit, chrono avec bips pour les exercices tenus, « Fait » pour les répétitions), effort et gêne notés en fin de passage à partir de valeurs proposées réglables, série et jokers par routine (jour fait dès un passage terminé), calendrier avec le nombre de passages, progression par exercice avec les dernières coches ; une page par élément (Exercices, Routines, Défis, Objectifs modifiables, Séances types) avec une fenêtre de modification, plein écran sur téléphone ; pages Aujourd'hui, Séances (filtre par sport, une page par séance), Progression et VO2max. Seul le profil principal est affiché |
| Tâches | Todos du profil principal : titre, photo facultative (sinon l'initiale du titre), échéance facultative, durée estimée, priorité (haute, normale, basse), étiquettes libres du foyer, notes, checklist de sous-tâches et projet facultatif ; un todo ouvert dont l'échéance est passée est en retard jusqu'à ce qu'il soit fait, reporté ou abandonné ; fait, rouvert, abandonné. Routines du foyer (mêmes champs, sans checklist) à date fixe (tous les N jours, certains jours de la semaine, un jour du mois) ou à délai après le dernier passage ; une seule échéance ouverte à la fois, passages faits (jour, note) ou sautés, série et meilleure série, pause et arrêt. Page Aujourd'hui (todos et routines : en retard, aujourd'hui, 7 prochains jours, « Sans échéance » replié, filtre par étiquette), page Toutes les tâches (recherche texte sans accents ni majuscules, filtres par type, état et étiquette), page Routines, page Projets (sous-projets à profondeur libre, par exemple Terrain › Chalet › Plafond ; avancement : todos faits sur todos non abandonnés, sous-projets compris ; terminer un projet laisse ses todos et ses sous-projets ouverts), une page par todo, par routine et par projet |
| Santé | Privée au compte (un autre membre du foyer ne la voit pas) ; fichiers chiffrés sur le serveur. Compléments : dose par jour et unité, moment, conditionnement (contenu, prix, magasin), liens d'achat, à l'année ou en cure répétée après une pause ; une coche par jour, série, 14 derniers jours ; coût par mois calculé (cures lissées) et total. Symptômes : liste personnelle (physique ou mental, proposée au départ), saisie à tout moment (intensité 1 à 10, jour, heure, note) et bilan du jour où « rien à signaler » compte ; journal et 14 derniers jours. Analyses : marqueurs avec unité, plage du labo, cible personnelle, fréquence, bilans et explications (à quoi il sert, si trop bas, si trop haut ; saisies à la main ou par Claude) ; analyses datées avec une valeur par marqueur ; position (labo, cible ; « dans la norme du labo, sans cible » quand aucune cible n'est fixée), tendance, marqueurs à faire ou refaire, nombre de marqueurs hors de la cible et sans cible. Mesures corporelles (poids, tour de taille, fréquence cardiaque au repos proposés) avec une cible personnelle facultative, position de chaque valeur et évolution. Documents (ordonnance, compte rendu, résultat, bilan, autre) avec date, médecin ou labo, notes et fichiers (photos prises ou choisies, PDF) chiffrés ; recherche et filtre par type ; un résultat se relie aux analyses saisies (à la main ou par Claude, l'API ne lit jamais les fichiers). Rendez-vous (jour, heure, médecin, motif, lieu, lien Doctolib ou autre pour reprendre rendez-vous, compte rendu relié) à venir et passés ; LifeOS ne se connecte à aucun site de réservation. Page Aujourd'hui avec les compléments du jour par moment, le bilan du jour, les marqueurs à refaire et les prochains rendez-vous. Sommeil et récupération importés de Garmin (nuit et phases, score, FC au repos, HRV et sa plage habituelle, Body Battery, stress, pas, respiration, réponses brutes de Garmin gardées) sur 4 semaines, 3 mois ou 1 an ; seule partie de la santé que l'agent de planification pourra lire |
| Budget | Privé au compte. Lignes prévues rangées par poste : fixe (montant attendu), enveloppe (plafond remis à zéro chaque mois), extra (non planifié, suivi seul), hors budget (virements entre ses comptes, avances) ou revenu ; montant par mois, par an ou tous les N mois, un changement valant à partir d'un mois choisi ; repère « à diminuer » ; une ligne peut prendre comme prévu le coût par mois des compléments, lu dans le contrat de la Santé ; arrêt à partir d'un mois. Opérations importées d'un relevé OFX (format BoursoBank, OFX 1 accepté aussi) : une opération déjà importée n'est jamais recréée (identifiant bancaire), la date d'achat est lue dans le libellé des paiements par carte, le solde du relevé est gardé ; saisie à la main ; étalement d'un achat sur 1 à 36 mois. Règles « libellé contient … » (sans majuscules ni accents, dépenses, revenus ou les deux), dans un ordre modifiable, appliquées à l'import et aux opérations non classées ; rangement à la main avec création de règle, ou par Claude. Pages Mois (revenus, dépenses, reste, extras en réel et en prévu, enveloppes, postes et lignes avec écart, non classées), Opérations (import, filtres toutes, non classées, étalées, recherche), Lignes prévues, une page par ligne (12 mois, opérations, règles), Règles, Évolution (dépenses par poste et par mois, extras, lignes à diminuer) |
| Accueil | Page Aujourd'hui, ouverte au lancement ([ADR 0021](architecture/decisions/0021-today-home.md)) : d'abord ce qu'il reste à cocher (en retard, tâches et routines du foyer du jour, routines sportives à démarrer ou continuer, défis avec « + Série », compléments par moment), le reste replié dans « Fait aujourd'hui » d'où l'on peut décocher ; puis la nuit, l'énergie, les séances du jour, l'avancement des objectifs de la semaine, les pas, et un bandeau quand Garmin attend un code |
| Import Garmin | Service `garmin-sync` (conteneur à part, bibliothèque non officielle python-garminconnect à version figée) qui lit Garmin Connect à 7 h, 13 h, 19 h et 23 h ou à la demande et écrit par les endpoints d'import avec un jeton d'import ; l'API n'appelle jamais Garmin ; historique complet au premier lancement (sommeil remonté par tranches de 60 jours à chaque synchro) |

Navigation : un menu à gauche avec Accueil puis un groupe par domaine (Alimentation, Sport, Tâches, Santé, Budget) qui s'ouvre sur
ses pages, Paramètres et compte en bas. Une barre du haut fixe porte le bouton menu : sur
ordinateur le menu reste affiché (repliable par ce bouton), sur téléphone il s'ouvre en panneau
et la barre rappelle la page affichée. Seul le contenu de la page défile. Plus d'onglets en bas. Tous les écrans (Alimentation, Sport, Tâches, Santé, Paramètres, connexion) utilisent
Angular Material 3 avec le thème LifeOS et les icônes Material Symbols
([ADR 0016](architecture/decisions/0016-angular-material-ui-kit.md)) : chaque liste commence
par une zone de recherche et de filtres, chaque création ou modification s'ouvre dans une
fenêtre, plein écran sur téléphone. L'interface passe en mode sombre quand le système
(ordinateur ou iPhone) l'est.

Modèle : [02-core-model.md](02-core-model.md). Code découpé en modules (ADR 0009) :
Foyer, Alimentation, Sport, Tâches et Santé, chacun avec son schéma PostgreSQL, et le module de synthèse Budget (schéma `budget`), qui ne lit les autres que par leurs contrats publiés ; tests d'architecture
côté backend et Angular.

## Ce qui n'existe pas

Semainier (retiré, [ADR 0008](architecture/decisions/0008-remove-week-planner.md)),
import Strava ou Apple Santé, import manuel de fichiers FIT, courbes détaillées d'une séance (GPS, tours), propositions, agent IA, agenda externe, mode hors
ligne. Stock, liste de courses et enrichissement externe sont hors périmètre.

## Écarts connus avec la cible

- iPhone : le widget « prochain événement » est vide et son code Swift reste à
  retirer ; voir [ios-v1.md](ios-v1.md). Traité à l'étape 6.
- Routes de l'API : préfixes inégaux (`/api/foods` et `/api/sport-templates` à côté de
  `/api/sport/...`, `/api/health/...`). Harmonisées à l'étape 6, avec la recompilation de
  l'app iPhone qui embarque son propre front.

## Technique

Angular 22 (`src/frontend-angular`), aussi empaqueté pour iPhone avec Capacitor 7.
Le site s'installe sur l'écran d'accueil comme une web app (manifeste, icônes, plein
écran) ; installé, ou dans l'app iPhone, il rouvre sur la dernière page visitée si elle
date de moins de 12 h, sinon sur « Aujourd'hui ». Pas de mode hors ligne.
API ASP.NET Core / EF Core / PostgreSQL (`src/backend`). L'API connecte avec Google
et émet ses propres jetons ([ADR 0011](architecture/decisions/0011-conversational-assistant-mcp.md),
[guide](google-oauth.md)). Schéma d'ensemble : [architecture/overview.md](architecture/overview.md).

Une fusion dans `main` publie les images Docker, que le serveur récupère seul :
c'est un déploiement. Les migrations s'appliquent au démarrage de l'API ;
l'utilisateur sauvegarde PostgreSQL et les volumes de fichiers avant (dont `lifeos_garmin_tokens`).
