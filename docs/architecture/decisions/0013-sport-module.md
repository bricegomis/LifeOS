# ADR 0013 — Module Sport : séances, objectifs de la semaine et défis du jour

Statut : accepté, 2026-11-13. Cadrage de l'étape 2 de la [roadmap](../../01-roadmap.md).
Applique l'[ADR 0009](0009-modular-monolith-and-planning-core.md) (module Sport,
schéma `sport`) et l'[ADR 0011](0011-conversational-assistant-mcp.md) (outils de
l'assistant). Maquette à valider avant tout code.

## Contexte

Le module Sport ne contient que des séances types (nom, sport en texte libre, durée,
distance, intensité, calories). Rien ne permet de noter ce qui a été fait, de suivre
une semaine ni de tenir une habitude quotidienne. Besoins exprimés : voir
[semainier-retour-experience.md](../../semainier-retour-experience.md) et la séance
de cadrage du 2026-11-13.

Sports pratiqués : course et marche, vélo et natation, musculation, crossfit, sports
collectifs et autres, et de petites sessions de renforcement quotidiennes (pompes,
tractions, dos, mobilité).

## Décision

Le module couvre trois choses, utilisables sans semainier : les **séances réalisées**,
les **objectifs de la semaine** et les **défis du jour**. Tout porte un profil ; seul
l'utilisateur principal est affiché à cette étape, ouvrir les autres profils ne
demandera pas de migration.

### Séances réalisées

Une séance réalisée porte : profil, date, sport, séance type d'origine (facultative),
durée, calories, et en option distance, ressenti, fréquence cardiaque moyenne et
maximale, notes. Elle porte aussi sa source, `manual` seulement pour l'instant.

- **Saisie depuis une séance type** en un geste : ses valeurs sont recopiées puis
  modifiables. La séance garde ses propres valeurs ; modifier la séance type ne
  réécrit pas les séances passées.
- **Calories saisies**, jamais calculées (pas de formule durée × intensité × poids).
  Une valeur inconnue reste inconnue, et le total de la semaine est alors marqué
  incomplet, comme la nutrition.
- **Ressenti** : note entière de 1 à 10 et commentaire libre.
- **Musculation et crossfit en texte libre** dans les notes (exercices, séries, WOD,
  score). Pas de bibliothèque d'exercices ni de format AMRAP / EMOM à cette étape ;
  ils viendront dans un lot suivant si l'usage le demande.
- **Sport : liste fixe** de codes stables, libellés français à l'écran, comme les
  familles d'aliments (ADR 0012). Proposition à valider avec la maquette : course,
  marche, randonnée, vélo, natation, musculation, crossfit, renforcement, mobilité,
  sport collectif, sport de raquette, autre. Une liste fixe est nécessaire pour
  les objectifs par sport et pour le futur semainier.
- **Pas d'heure obligatoire** : la date suffit. Les jours sont ceux de
  l'Europe/Paris ; la semaine va du lundi au dimanche.

### Objectifs de la semaine

Il n'y a pas de séances attendues datées : un objectif fixe ce qui doit être fait dans
la semaine, et seules les séances enregistrées comptent. La confirmation explicite
demandée plus tôt est donc respectée sans étape de confirmation.

Un objectif appartient à un profil et prend une des quatre formes :

| Forme | Exemple | Ce qui compte |
| --- | --- | --- |
| Par séance type | « Footing 45 min » ×2 | les séances créées depuis cette séance type |
| Par sport | course ×2 | les séances de ce sport |
| Global en séances | 4 séances | toutes les séances |
| Global en volume | 1 500 kcal, 20 km ou 3 h | la somme des calories, distances ou durées |

- L'avancement (« 1 / 2 ») se calcule à la lecture, jamais stocké.
- **Un objectif modifié vaut à partir de la semaine en cours** ; les semaines passées
  restent jugées avec l'objectif d'alors (période de validité sur l'objectif).
- Un objectif peut être arrêté ; il disparaît des semaines suivantes.

### Défis du jour

Un défi est une habitude quotidienne chiffrée : « 100 pompes », « 20 min de
mobilité ». Il porte un profil, un nom, une unité (`reps` ou `minutes`), une cible
par jour et un nombre de jokers par semaine (0 à 2).

- **Saisie par séries cumulées** : chaque ajout (+20, +15…) est une entrée datée et
  horodatée, supprimable ; le total du jour est leur somme. Ajout depuis l'écran
  (boutons rapides et valeur libre) ou en le disant à l'assistant.
- **Pas de calories** sur les défis ; ils ne comptent pas dans le bilan calorique.
- Le défi n'est pas une séance : 100 pompes en dix fois ne créent pas dix séances.
- Une cible modifiée vaut à partir du jour même, comme les objectifs.

Gamification, toute calculée à la lecture :

- **Série** : nombre de jours d'affilée où la cible est atteinte, avec le record.
  Un jour manqué est couvert par un joker s'il en reste dans sa semaine ; sinon la
  série repart à zéro. Le jour en cours ne casse jamais la série avant minuit.
- **Calendrier** : une case par jour, colorée selon le pourcentage de la cible
  atteint (0, moins de 50 %, moins de 100 %, atteint, joker).
- **Totaux et paliers** : cumul depuis le début et sur l'année (« 12 400 pompes en
  2026 ») ; paliers fixes célébrés à l'écran (1 000, 5 000, 10 000, 25 000, 50 000,
  100 000 pour les répétitions ; 10 h, 50 h, 100 h, 250 h, 500 h pour les minutes).

### Écrans

- **Accueil Sport (aujourd'hui + semaine)** : les défis du jour avec leur compteur,
  des boutons d'ajout rapide et la série en cours ; les objectifs de la semaine
  (fait / prévu) ; les calories de la semaine ; les dernières séances ; un bouton
  « Ajouter une séance ». Conçu pour le téléphone d'abord.
- **Ajouter / modifier une séance** : choix d'une séance type ou d'un sport, puis
  les champs ci-dessus.
- **Historique** : séances groupées par semaine, bilan en tête de chaque semaine.
- **Progression** : par semaine, calories, nombre de séances, distance et durée,
  avec l'atteinte des objectifs ; par défi, calendrier, série, record, totaux et
  paliers.
- **Réglages du sport** : séances types, objectifs, défis.

### Assistant

Le module livre ses outils MCP (lecture et écriture), qui appellent les mêmes cas
d'usage que l'écran : séances types, séances réalisées (ajouter, modifier,
supprimer, lister), objectifs, défis, ajout d'une série à un défi (« j'ai fait 30
pompes »), et un bilan de la semaine ou d'un défi.

### Contrat publié

Pour le futur semainier (ADR 0009) : séances réalisées par jour et par profil
(sport, durée, calories), objectifs de la semaine et leur avancement, état des défis
du jour.

## Hors périmètre

- **Import Garmin, Strava ou Apple Santé** : plus tard, avec sa propre ADR. Pour une
  montre Garmin, l'API officielle est réservée aux entreprises ; les pistes sont
  Strava (que Garmin alimente déjà) ou Apple Santé depuis l'app iPhone native. Le
  modèle ne prépare que la source de la séance.
- Plans d'entraînement sur plusieurs semaines, séances attendues à date fixe.
- Détail structuré de la musculation et du crossfit.
- Notifications et rappels (étape 6).
- Toute dépense calorique calculée.

## Conséquences

- Nouvelles tables dans le schéma `sport` : séances réalisées, objectifs, défis et
  entrées de défi, toutes filtrées par foyer, avec des références composites vers
  le foyer et le profil.
- La séance type passe à la liste fixe de sports. Les séances types existantes ne
  peuvent pas être rattachées par leur nom : la migration les supprime, à confirmer
  par l'utilisateur avant d'écrire la migration.
- L'intensité de la séance type est retirée, car le ressenti de la séance réalisée
  la remplace.
- Ajouter un sport demande une migration et la mise à jour des listes du domaine, du
  frontend et de l'aide de l'assistant.
