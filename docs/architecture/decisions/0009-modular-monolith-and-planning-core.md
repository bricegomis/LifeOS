# ADR 0009 — Monolithe modulaire et semainier au cœur

Statut : accepté, 2026-11-11. Élargit le périmètre produit de
[00-vision.md](../../00-vision.md), précise l'orientation annoncée par
[ADR 0008](0008-remove-week-planner.md) et précise le point D3 de
`audit-2026-11.md`. Les ADR 0003 (isolation par foyer), 0006 et 0007 restent valables.

## Contexte

LifeOS doit couvrir plusieurs domaines de la vie du foyer : alimentation, sport,
choses à faire, santé, puis d'autres besoins. Le but est de décharger l'utilisateur
des décisions (quoi manger, quelle séance, quoi faire et quand) pour qu'il se
concentre sur l'exécution. Le semainier, vue calendrier de tout ce qui est prévu,
est le cœur de l'application.

Ces domaines s'influencent : une séance cochée ou manquée change les besoins
caloriques des repas suivants. Le code actuel ne le permet pas :

- le backend est découpé par couche technique, pas par domaine ; un seul
  `LifeOSDbContext` donne accès à tout et la plupart des endpoints l'utilisent
  directement ;
- il n'existe ni séance réalisée, ni repas planifié, ni objectif calorique ;
- la vision et les consignes interdisaient le tableau de bord généraliste, les
  règles et la génération automatique.

Choix de l'utilisateur (2026-11-11) :

| Sujet | Choix |
| --- | --- |
| Autonomie | LifeOS propose, l'utilisateur valide tout. L'automatisation viendra plus tard, pour les propositions toujours acceptées. |
| Agenda externe | Lecture seule d'abord. L'écriture vers l'agenda externe reste possible plus tard (partage). |
| Personnes | Repas pour tout le foyer ; sport, tâches et santé pour l'utilisateur principal. |
| Tâches | Routines récurrentes du foyer, todos ponctuels et projets. |
| Santé | Tout le dossier : mesures, symptômes quotidiens, rendez-vous, traitements, ordonnances, résultats d'analyse. |
| Moteur | Agent IA externe planifié. L'API valide et stocke. |
| Temps | Tout élément planifié a une heure de début et une durée. |
| Hors ligne | Lecture hors ligne et coches mises en file ; pas d'édition hors ligne. |
| Confidentialité santé | Données privées par personne ; l'agent ne reçoit que des résumés. |

## Décision

### 1. Un monolithe modulaire

Une seule API, une seule base PostgreSQL, un seul frontend, un seul déploiement.
Pas de microservices, pas de base par module, pas de bus d'événements.
Le code est découpé en modules métier :

| Module | Contenu | État |
| --- | --- | --- |
| Foyer (noyau) | foyers, comptes, profils, groupes, fuseau horaire, jetons d'agent | existe |
| Alimentation | aliments, produits, magasins, tickets, recettes, avis, repas | existe, sauf les repas |
| Sport | séances types, récurrence, séances réalisées, bilan | séances types seulement |
| Tâches | routines, todos, projets | à créer |
| Santé | mesures, symptômes, rendez-vous, traitements, documents | à créer |
| Planning | semainier, propositions, signaux, agenda externe | à créer |

Sens des dépendances : `Foyer ← modules de domaine ← Planning`.

- Un module de domaine ne connaît ni les autres domaines ni le Planning.
- Toute règle qui croise deux domaines vit dans le Planning, jamais dans un domaine.
- Un nouveau besoin de vie est un nouveau module de domaine, sans toucher aux autres.

### 2. Règles de frontière

1. Chaque module possède ses tables. Les nouveaux modules utilisent leur propre
   schéma PostgreSQL (`sport`, `tasks`, `health`, `planning`). Les tables
   existantes restent dans `public` : aucune migration de renommage.
2. Un module ne lit ni n'écrit les tables ou les entités d'un autre module.
   Il passe par le contrat publié de ce module : une interface de lecture, en
   `Application/<Module>/Contracts`, qui renvoie des DTO.
3. Entre modules de domaine, on référence par identifiant, sans navigation EF.
   Les clés étrangères en base ne sont permises que vers le noyau Foyer et, depuis
   le Planning, vers l'élément de domaine qu'il planifie.
4. Chaque ligne métier garde son `household_id` et son filtre applicatif (ADR 0003).
5. Un seul `LifeOSDbContext` et un seul historique de migrations sont conservés,
   pour garder des transactions simples. La frontière est tenue par les espaces de
   noms et par un test d'architecture qui échoue si un module référence l'intérieur
   d'un autre.

### 3. Organisation du code

Backend : les quatre projets restent. Chacun contient un dossier par module
(`Domain/Sport`, `Application/Sport`, `Infrastructure/Sport`, `Api/Sport`).

- `Domain` : entités et invariants, sans EF ni E/S.
- `Application` : contrat publié du module et cas d'usage.
- `Infrastructure` : accès aux données, fichiers, connecteurs externes.
- `Api` : endpoints (HTTP, validation, DTO), sans règle métier.

Deux niveaux d'exigence, pour ne pas alourdir ce qui est simple :

- **Écran de gestion simple** (créer, lire, modifier, archiver une fiche) :
  l'endpoint appelle directement la base, sur les seules tables de son module.
  Les règles de validité restent dans l'entité du domaine.
- **Action avec de vraies règles**, ou appelée par un autre module (valider un
  ticket, calculer un prix comparable, confirmer une séance, appliquer une
  proposition) : la logique est écrite dans un cas d'usage, une classe par action,
  indépendante du HTTP. Elle accède aux données par une interface du module
  (le « port »), ce qui permet de la tester sans base ni serveur.

Le contrat publié d'un module est toujours un cas d'usage de lecture.

Ces règles s'appliquent à tous les modules. Le code existant (Foyer et Alimentation)
y est mis en conformité pendant l'étape 1 de la roadmap, avant tout nouveau
module. Cela précise le point D3 de l'audit.

Frontend : un dossier par module (`app/<module>/`) avec ses pages, son service
d'API, ses modèles et ses routes chargées à la demande. Un module n'importe que
`core` et `shared`, jamais un autre module. Le shell et la navigation sont communs.
Le semainier devient la page d'accueil.

### 4. Le Planning : intention, fait et proposition

Trois notions séparées :

- **Élément planifié** (Planning) : l'intention. Il porte le foyer, la personne
  éventuelle, le type (repas, séance, tâche, santé, externe), le début, la durée,
  le statut (prévu, fait, sauté, annulé) et l'identifiant de l'élément de domaine.
- **Contenu** (module de domaine) : ce qui est prévu. Un repas planifié et son
  snapshot de recette appartiennent à l'Alimentation, une séance au Sport, une
  occurrence de tâche aux Tâches. Chaque domaine fournit au Planning un résumé
  d'affichage et, s'il y a lieu, ses calories.
- **Fait** (module de domaine) : ce qui s'est réellement passé, avec sa propre date.
  Une séance réalisée existe sans semainier : le sport reste utilisable seul.

Cocher un élément dans le semainier appelle le module de domaine, qui enregistre
le fait ; le Planning met à jour le statut dans la même transaction.

Temps : chaque élément a un début (`timestamptz`) et une durée ; le foyer porte son
fuseau horaire. Le foyer définit des horaires par défaut par type de repas, pour
que ni l'utilisateur ni l'agent n'aient à saisir une heure à chaque fois.

Snapshots : modifier une recette, une séance type ou une routine ne réécrit jamais
un élément déjà planifié.

### 5. Liens entre domaines : signaux calculés, pas de réactions

Aucun module ne modifie un autre module en réaction à un événement.
Le Planning calcule des **signaux** à la lecture, à partir des contrats des domaines.
Premier signal : le bilan énergétique par personne et par jour (apport prévu,
dépense confirmée, objectif). Il indique qu'un repas prévu est trop faible après
une séance, ou trop élevé après une séance manquée.

Un signal n'est jamais stocké : décocher une séance le corrige sans code de
compensation. Une valeur inconnue reste inconnue et rend le bilan incomplet.

### 6. Propositions et validation

Le planning ne change que de deux façons : une action directe de l'utilisateur,
ou une proposition acceptée par lui.

- Une **proposition** est un lot de changements (ajouter, déplacer, modifier,
  retirer des éléments) avec une explication lisible.
- Statuts : en attente, acceptée, refusée, expirée.
- L'API valide chaque proposition à la réception et de nouveau à l'acceptation :
  foyer, références, snapshots, chevauchements. L'application est atomique.
- Chaque décision est conservée. Cet historique servira à automatiser plus tard
  les types de proposition toujours acceptés, par une règle d'acceptation
  automatique, sans changer le modèle.

### 7. Agent IA externe

- L'agent tourne hors de l'API, à heure fixe et quand des signaux apparaissent.
- L'API n'appelle aucun fournisseur d'IA et ne contient aucun prompt.
- L'agent s'authentifie avec un jeton d'agent propre au foyer, révocable, aux
  droits limités : lire les bibliothèques, les règles, le planning, les signaux et
  les résumés ; écrire uniquement des propositions.
- Les règles et préférences de planification sont des données stockées par l'API
  et modifiables dans l'application.
- L'agent ne lit jamais le dossier santé (§ 8).

Les règles et la génération reviennent donc dans le périmètre, mais le calcul
reste hors de l'API. Stock et liste de courses restent hors périmètre.

### 8. Santé : données privées par personne

- Les données de santé appartiennent à un profil, pas au foyer. Seul le compte lié
  à ce profil y accède, même si un autre adulte rejoint le foyer.
- Les documents (ordonnances, analyses) sont stockés sur un volume durable dédié,
  servi uniquement par des routes authentifiées. Ce volume s'ajoute aux sauvegardes.
- Le module Santé publie au Planning un résumé limité : objectif calorique,
  contraintes alimentaires, créneaux de rendez-vous et de prises. L'agent ne voit
  que ce résumé.
- Pas de chiffrement applicatif pour l'instant. À réexaminer avant d'ouvrir le
  foyer à un autre compte ou d'importer des documents en volume.
- Chaque mesure porte sa source (manuelle ou importée).

### 9. Personnes

Tout élément personnel (séance, tâche, donnée de santé, objectif) porte un
`profile_id` dès sa création. Les repas sont planifiés pour le foyer, avec des
portions par profil. Seul l'utilisateur principal est suivi au début ; ajouter une
personne ne demandera pas de changement de modèle.

### 10. Agenda externe

Les créneaux occupés d'un agenda externe sont importés en lecture seule, comme
éléments de type « externe », non modifiables dans LifeOS. L'import passe par un
port de connecteur dans le Planning. Chaque élément planifié peut porter un
identifiant externe, ce qui rendra l'écriture vers l'agenda possible plus tard
avec un second adaptateur. Le choix du fournisseur et l'authentification feront
l'objet d'une ADR dédiée, comme les imports Garmin, Strava ou Apple Santé.

### 11. Hors ligne sur iPhone

- Le planning des prochains jours est mis en cache et lisible sans réseau.
- Les coches (fait, sauté, symptôme du jour) sont mises en file et envoyées au
  retour du réseau. Chaque commande porte un identifiant généré par le client ;
  l'API la traite de façon idempotente.
- Aucune autre modification hors ligne, aucune résolution de conflits.

## Ordre de mise en œuvre

Détail, critères de fin et questions de cadrage : [01-roadmap.md](../../01-roadmap.md).

1. **Alimentation saine** : simplification fonctionnelle, tests, mise en module du
   code existant et du noyau Foyer.
2. **Sport**.
3. **Tâches**.
4. **Semainier**, avec propositions, agent IA, repas planifiés et bilan énergétique.
5. **Santé**.
6. **iPhone natif** : hors ligne, widgets, notifications.

Chaque étape commence par un cadrage avec l'utilisateur et se termine par une
version utilisable. Agenda externe et imports de capteurs auront leur propre ADR.

## Conséquences

- La vision, `CLAUDE.md`, `AGENTS.md` et `PROJECT.md` sont mis à jour : LifeOS
  n'est plus limité aux repas et au sport.
- Ajouter un domaine coûte un module et un contrat, sans modifier les autres.
- L'étape 1 est une refonte en bloc de l'existant. Les données actuelles peuvent
  être perdues (décision de l'utilisateur, 2026-11-11) : une migration destructive
  y est permise, par exception à la règle des migrations additives.
- Le test d'architecture peut demander une dépendance réservée aux tests.
- Un troisième volume (documents de santé) devra être sauvegardé avec PostgreSQL.
- Si un module doit un jour être extrait, ses tables, son contrat et son schéma
  sont déjà séparés.
