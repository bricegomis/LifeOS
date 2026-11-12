# LifeOS — Roadmap

Fixée le 2026-11-11. Architecture et règles :
[ADR 0009](architecture/decisions/0009-modular-monolith-and-planning-core.md).
Vision : [00-vision.md](00-vision.md).

## Vue d'ensemble

| # | Étape | Résultat |
| --- | --- | --- |
| 1 | Alimentation saine | Un module Alimentation simple, testé et conforme à l'ADR 0009 |
| 1 bis | Assistant Claude | LifeOS se remplit et se consulte en discutant avec Claude (MCP) |
| 2 | Sport | Séances réalisées, récurrence et bilan, utilisables seuls |
| 3 | Tâches | Routines, todos et projets, utilisables seuls |
| 4 | Semainier | Calendrier rempli par un agent IA, propositions à valider |
| 5 | Santé | Dossier de santé privé, relié au semainier |
| 6 | iPhone natif | Hors ligne, widgets, notifications |

## Règles communes à toutes les étapes

- **Cadrage d'abord.** Chaque étape commence par une séance de questions avec
  l'utilisateur pour fixer ce qu'il veut réellement. Le résultat est écrit dans une
  ADR ou un document de modèle avant tout code.
- **Maquette avant le code.** Les écrans de l'étape sont d'abord livrés en maquette
  cliquable, avec des données d'exemple. Le backend et l'interface réelle ne
  commencent qu'une fois la maquette validée par l'utilisateur.
- **Un module par étape**, construit selon l'ADR 0009 : tables et schéma propres,
  contrat publié, isolation par foyer testée, test d'architecture vert.
- **Web d'abord.** Le code Swift n'est pas touché avant l'étape 6.
- **Hors périmètre** tout du long : stock, liste de courses, enrichissement externe
  par l'API. L'assistant conversationnel peut chercher nutrition et images, sous le
  contrôle de l'utilisateur ([ADR 0011](architecture/decisions/0011-conversational-assistant-mcp.md)).
- **Outils de l'assistant.** À partir de l'étape 2, chaque module livre aussi ses
  outils MCP (lecture et écriture), selon l'ADR 0011.

## Branches et livraison

- Tout le travail arrive par petites PR sur la branche `integration`.
  La CI (tests backend, contrôles Angular) tourne sur chaque PR.
- `main` ne reçoit qu'une fusion par étape, à la fin de l'étape, avec l'accord
  explicite de l'utilisateur. Cette fusion publie les images et vaut déploiement ;
  l'utilisateur sauvegarde la base et les volumes avant.
- Chaque fusion sur `integration` met à jour la version d'intégration : une seconde
  pile sur le VPS, avec sa propre base. Voir
  [integration-environment.md](integration-environment.md).
- Un module commencé mais pas terminé reste hors de la navigation quand
  `integration` est fusionnée dans `main`.

## Enchaînement des étapes

Les étapes 1 à 3 portent sur des domaines indépendants. Elles s'enchaînent sans
attente et peuvent se chevaucher. Deux contraintes seulement :

- le **socle commun** de l'étape 1 passe en premier : noyau Foyer, gabarit de
  module, test d'architecture. Sport et Tâches s'appuient dessus ;
- les migrations de base partagent un seul historique : deux modules menés en même
  temps fusionnent leurs migrations l'une après l'autre, jamais en même temps.

L'étape 4 (semainier) a besoin des contrats publiés par les étapes 1 à 3.

## Points ouverts

- Mise en place de la pile d'intégration sur le VPS (par l'utilisateur).
- Accès à Docker sur le poste de développement, nécessaire pour lancer en local les
  tests d'intégration et une base PostgreSQL.

## Étape 1 — Alimentation saine

**But.** Partir d'une base de code propre et d'un modèle alimentaire simple, sur
lesquels les autres modules s'aligneront.

**Décisions prises.** Cadrage fait le 2026-11-11 :
[ADR 0010](architecture/decisions/0010-simplified-food-module.md) et
[02-core-model.md](02-core-model.md).

- Aliments (avec fibres), recettes, avis par profil, magasins, produits et prix
  sont gardés. Le ticket devient une simple photo conservée.
- Import JSON, OCR, lignes de ticket, achats, groupes du foyer et session de
  cuisine sont supprimés.
- L'interface met en avant recettes et aliments (ordinateur et smartphone) ; la
  saisie des produits et des prix se fait sur ordinateur.
- Tout le code existant est réorganisé en modules (Foyer et Alimentation), pas
  seulement ce qu'on touche. Les tests sont renforcés.
- La base est remise à zéro : une migration destructive est permise.

**Déroulé.**

1. **Cadrage fonctionnel.** Fait.
2. **Maquette cliquable** des écrans, validée
   par l'utilisateur.
3. **Suppression.** Retirer le code, les tables et les écrans abandonnés, ainsi
   que le code mort. Fait.
4. **Tests.** Couvrir ce qui reste avant de le déplacer : isolation par foyer,
   règles du domaine, parcours d'API. Fait.
5. **Mise en module.** Noyau Foyer (résolution du foyer en un seul endroit) et
   module Alimentation, en commençant par une seule tranche (les recettes) relue
   avec l'utilisateur avant de généraliser. Frontend : dossier par module, routes
   chargées à la demande, gros composants découpés. Test d'architecture ajouté. Fait.
6. **Refonte du modèle** et de l'interface selon la maquette : fibres, images de
   recettes, ticket photo, produits toujours complets, migration destructive. Fait.
7. **Coût de recette**, en fourchette minimum–maximum. Fait.

**Terminé quand.**

- le modèle alimentaire tient en une page et ne contient plus de notion en double ;
- chaque action à règles (tickets, prix) est un cas d'usage testé sans base, et
  aucun code Alimentation ne touche les tables d'un autre module ;
- le test d'architecture et tous les contrôles backend et frontend passent ;
- les points de l'audit de novembre encore pertinents sont traités ou fermés.

## Étape 1 bis — Assistant Claude (MCP)

**But.** Remplir LifeOS, vide après l'étape 1, et le consulter en discutant avec
Claude (claude.ai web et iPhone, Claude Code) plutôt qu'en saisissant chaque fiche.

**Décisions prises.** Cadrage fait le 2026-11-12 :
[ADR 0011](architecture/decisions/0011-conversational-assistant-mcp.md).

- Serveur MCP dans l'API ; les outils appellent les cas d'usage existants.
- Écriture directe pour les bibliothèques, propositions pour le futur semainier.
- L'assistant agit avec tous les droits du compte, santé de son profil comprise.
- Supabase est retiré : connexion Google gérée par l'API, qui devient serveur OAuth.
- Comptes et foyers remis à zéro. Pas de maquette : trois écrans simples.

**Déroulé.**

1. **Comptes et connexion.** Table `users`, connexion Google par l'API, liste des
   adresses autorisées, serveur OAuth (client `lifeos-web`), page de connexion
   Angular, retrait de Supabase, migration destructive des comptes. Fait.
2. **Endpoint MCP.** `/mcp`, découverte OAuth, clients Claude Code et claude.ai,
   écran de consentement, accès accordés révocables dans les Réglages. Un premier
   outil en lecture pour valider la connexion depuis claude.ai et Claude Code. Fait.
3. **Outils Alimentation.** Recherche et création d'aliments, recettes, magasins,
   produits, prix et avis ; image par URL ; nutrition et coût des recettes.
4. **Outils Tickets.** Tickets en attente, photo, saisie des prix, ticket traité.

**Terminé quand.** Depuis une discussion sur iPhone, l'utilisateur crée un aliment
complet avec son image, une recette, et traite un ticket photographié dans
l'application ; un autre foyer ne voit rien de tout cela (testé).

## Étape 2 — Sport

**But.** Un sport utilisable seul, sans semainier.

**Contenu prévu.** Séances types (existent), séances réalisées à une date,
récurrence des séances attendues, bilan hebdomadaire des calories par personne.
Chaque séance porte un profil et une source (manuelle ou importée).
Acquis des [leçons du premier semainier](semainier-retour-experience.md) : confirmation
explicite, calories saisies par séance, aucun calcul automatique.

**À cadrer.**

- Quels sports, et quelles données par séance (durée, distance, ressenti, charge) ?
- Récurrence simple (jours de la semaine) ou plans d'entraînement sur plusieurs semaines ?
- Objectifs (volume hebdomadaire, progression) : dès cette étape ou plus tard ?
- Quelle vue en attendant le semainier : liste de la semaine, historique, graphiques ?
- Import Garmin, Strava ou Apple Santé : reporté, ou préparé ici par une ADR ?

**Terminé quand.** L'utilisateur enregistre ses séances pendant plusieurs semaines
sans friction et lit son bilan hebdomadaire. Le module publie son contrat de
lecture (séances attendues et réalisées, calories par jour et par profil).

## Étape 3 — Tâches

**But.** Sortir de la tête ce qu'il y a à faire, récurrent ou ponctuel.

**Contenu prévu.** Routines récurrentes du foyer, todos ponctuels avec échéance,
projets regroupant des tâches. Chaque tâche porte un profil responsable.

**À cadrer.**

- Routines : fréquence fixe (tous les lundis) ou relative (7 jours après la dernière fois) ?
- Projets : simple regroupement, ou étapes, dépendances et jalons ?
- Durée estimée et priorité : nécessaires pour que le semainier place les tâches ?
- Tâches des enfants : dès cette étape ou plus tard ?
- Quelle vue en attendant le semainier : liste du jour, par projet, en retard ?
- Que devient une tâche non faite : reportée, signalée, abandonnée ?

**Terminé quand.** Routines et todos sont suivis dans LifeOS au quotidien. Le
module publie son contrat de lecture (tâches à placer, durée, échéance, état).

## Étape 4 — Semainier, propositions et agent

**But.** Le cœur du produit : un calendrier qui arrive rempli, que l'utilisateur
valide et qui s'ajuste aux imprévus.

**Contenu prévu.** Livré en lots successifs, dans cet ordre :

1. Éléments planifiés horodatés et vue calendrier (jour, semaine), page d'accueil.
2. Repas planifiés avec snapshot de recette et portions par profil.
3. Coche « fait / sauté » reliée aux modules Sport, Tâches et Alimentation.
4. Signaux, dont le bilan énergétique par personne et par jour.
5. Règles de planification stockées par l'API, propositions et écran de validation.
6. Jeton d'agent, contrat d'API de l'agent, puis l'agent IA planifié lui-même.
7. Agenda externe en lecture seule (ADR dédiée).

**À cadrer.**

- Vues du calendrier et horaires par défaut des repas.
- Objectif calorique par personne : saisi à la main, ou dérivé plus tard de la santé ?
- Règles de planification : lesquelles, sous quelle forme (texte libre, champs) ?
- Rythme de l'agent : quand prépare-t-il la semaine, quand réagit-il aux signaux ?
- Où tourne l'agent, avec quel budget, et quel fournisseur d'agenda externe ?
- Forme et granularité des propositions (semaine entière, jour, élément).

**Terminé quand.** La semaine est prête sans saisie manuelle, l'utilisateur la
valide en quelques minutes, et une séance manquée produit une proposition
d'ajustement des repas.

## Étape 5 — Santé

**But.** Tenir le dossier de santé au même endroit et en tenir compte dans le planning.

**Contenu prévu.** Mesures corporelles, symptômes quotidiens, rendez-vous,
traitements et prises, ordonnances et résultats d'analyse en documents. Données
privées par personne ; le semainier et l'agent ne reçoivent qu'un résumé.

**À cadrer.**

- Ordre de priorité entre mesures, symptômes, traitements et documents.
- Documents : simple archivage, ou extraction des valeurs d'analyse ?
- Lien symptômes digestifs ↔ aliments FODMAP : simple journal, ou analyse ?
- Imports Apple Santé ou Garmin (ADR dédiée).
- Chiffrement des documents et des champs sensibles : à réexaminer ici.
- Ce que contient exactement le résumé transmis au semainier et à l'agent.

**Terminé quand.** Le suivi quotidien et les documents sont dans LifeOS,
rendez-vous et prises apparaissent dans le semainier, et l'objectif calorique peut
venir du module Santé.

## Étape 6 — iPhone natif

**But.** Rendre l'usage quotidien sur téléphone fiable et rapide.

**Contenu prévu.** Lecture hors ligne des prochains jours, coches mises en file,
notifications, widgets du semainier. Retrait du code Swift du widget « prochain
événement » et de la Live Activity de cuisine (session de cuisine supprimée par
l'ADR 0010).
Demande macOS et Xcode ; l'installation réelle reste une vérification humaine.

Cette étape ne dépend pas de l'étape 5 : elle peut passer avant la santé si
l'usage mobile du semainier le demande.

## Après la roadmap

Pistes non planifiées : acceptation automatique des propositions toujours validées,
écriture vers l'agenda externe, autres personnes suivies, autres adultes connectés,
nouveaux domaines.
