# ADR 0019 — Import Garmin : séances, VO2max, sommeil et récupération

Statut : accepté, cadrage du 2026-11-23. Maquette à valider avant tout code. Étend
l'[ADR 0013](0013-sport-module.md) (séances réalisées) et l'[ADR 0018](0018-health-module.md)
(module Santé). Applique l'[ADR 0009](0009-modular-monolith-and-planning-core.md)
(modules, agent externe), l'[ADR 0011](0011-conversational-assistant-mcp.md) (assistant)
et l'[ADR 0016](0016-angular-material-ui-kit.md) (UI kit).

## Contexte

L'utilisateur porte une Garmin Forerunner 955. Ses données arrivent dans Garmin Connect,
qui les envoie aussi à Strava et Apple Santé. But : voir dans LifeOS son évolution
(course, vélo, crossfit, marche, sommeil, récupération) sans rien ressaisir.

Sources étudiées le 2026-11-23 :

| Source | Verdict |
| --- | --- |
| API officielle Garmin | Réservée aux entreprises. |
| Strava | Abonnement développeur payant depuis le 30 juin 2026 ; sa politique API interdit d'utiliser ses données dans une application d'IA ; pas de sommeil ni de métriques Garmin. Écarté. |
| Apple Santé | Officiel et gratuit, mais moins de données et du code natif iPhone. Écarté pour l'instant. |
| Garmin Connect par la bibliothèque `python-garminconnect` (MIT, non officielle) | Gratuit, toutes les données. Retenu. |

Test réel le 2026-11-23 (version 0.3.20) sur le compte de l'utilisateur : 729 activités
depuis le 2024-12-08, VO2max course mois par mois (46,5 → 49,2 en un an ; pas de VO2max
vélo sans capteur de puissance), sommeil par nuit (durée, phases, qualité, FC au repos,
respiration), HRV avec statut, pas.

Risques acceptés : la bibliothèque passe par la connexion de l'app Garmin, hors des
conditions de Garmin, et casse quand Garmin change sa connexion (coupure de mars 2026).
Rien n'est perdu : tout reste dans Garmin Connect et la synchro rattrape au retour. En
cas de panne, les séances peuvent être saisies à la main.

## Décision

### 1. Un service de synchro hors de l'API

- Un petit service Python, `garmin-sync`, dans `src/garmin-sync`, déployé dans le
  `docker-compose.yaml` à côté de l'API. Il utilise `python-garminconnect` à une
  **version figée**, mise à jour seulement à la main.
- L'API n'appelle jamais Garmin. Le service lit Garmin et écrit dans LifeOS par des
  endpoints d'import dédiés, comme l'agent externe (ADR 0009).
- Il s'authentifie sur LifeOS avec un **jeton d'import** créé dans les Réglages,
  révocable, lié à un profil, limité aux endpoints d'import Garmin.
- Identifiants Garmin : saisis une seule fois par une commande de connexion du service
  (mot de passe et code MFA), jamais stockés. Seuls les jetons Garmin sont gardés, dans
  un volume `lifeos_garmin_tokens`. Ni mot de passe ni jeton Garmin dans la base LifeOS.
- **Rythme** : à 7 h, 13 h, 19 h et 23 h (Europe/Paris). Un bouton « Synchroniser
  maintenant » enregistre une demande dans LifeOS ; le service la voit en interrogeant
  l'API toutes les 5 minutes. LifeOS affiche la dernière synchro réussie et la dernière
  erreur (connexion Garmin refusée, bibliothèque cassée…).
- **Premier lancement** : tout l'historique (activités depuis 2024-12, sommeil,
  récupération et VO2max sur la même période).
- Un seul compte Garmin, celui de l'utilisateur principal. Le modèle porte le profil,
  d'autres comptes ne demanderont pas de migration.

### 2. Séances réalisées : heures, effort et sensation

Changements sur toutes les séances, manuelles comprises :

- **Heure de début et heure de fin obligatoires.** La date de la séance est celle du
  début (Europe/Paris). La durée reste un champ : pour Garmin c'est le temps
  d'activité chronométré (pauses exclues) ; en saisie manuelle elle vaut fin − début
  par défaut, modifiable.
- **Effort perçu** de 1 (très facile) à 10 (maximal), facultatif, et **sensation** en
  cinq niveaux facultative : très faible, faible, normale, forte, très forte. Ce sont
  les deux questions posées par la montre en fin de séance. Ils **remplacent** le
  ressenti 1-10 actuel, qui allait dans l'autre sens (1 = très dur) : aucune
  conversion. Il n'y a encore aucune séance enregistrée, rien n'est à migrer.
- Champs ajoutés, remplis par Garmin et vides en saisie manuelle sauf mention :
  dénivelé positif, vitesse moyenne (allure affichée pour course et marche), Training
  Effect aérobie et anaérobie (une décimale), temps par zone de FC (5 zones), nom de
  l'activité Garmin, identifiant Garmin.
- Les **calories** importées sont celles de la montre ; LifeOS n'en calcule toujours
  aucune.

### 3. Séances Garmin

- **Une activité Garmin = une séance**, source `garmin`, identifiée par son
  identifiant Garmin (jamais deux fois).
- **Sport** : correspondance fixe dans l'API, depuis le type Garmin reçu tel quel :

  | Types Garmin | Sport LifeOS |
  | --- | --- |
  | `running`, `trail_running`, `treadmill_running`, `track_running`, `indoor_running` | `running` |
  | `walking`, `casual_walking`, `speed_walking` | `walking` |
  | `hiking` | `hiking` |
  | `cycling`, `road_biking`, `mountain_biking`, `gravel_cycling`, `indoor_cycling`, `virtual_ride` | `cycling` |
  | `lap_swimming`, `open_water_swimming` | `swimming` |
  | `strength_training` | `strength` |
  | `hiit` | `crossfit` |
  | `yoga`, `pilates`, `breathwork` | `mobility` |
  | autres | `other` |

  Toutes les marches sont importées. Le sport d'une séance Garmin peut être changé dans
  LifeOS (une séance HIIT qui n'était pas du crossfit) ; ce choix n'est plus écrasé.
- **Garmin écrase** : à chaque synchro, toutes les valeurs venues de Garmin sont
  remplacées si elles ont changé dans Garmin Connect. Restent à LifeOS et ne sont
  jamais écrasés : les **notes**, la **séance type** d'origine et le sport s'il a été
  changé.
- **Suppression** : une activité supprimée dans Garmin Connect est supprimée de
  LifeOS, notes comprises. Chaque synchro relit la liste complète des activités
  (quelques pages de 100) et supprime les séances Garmin absentes.
- **Fusion avec une séance manuelle** : une séance manuelle du même sport dont les
  horaires chevauchent ceux de l'activité Garmin devient la séance Garmin (valeurs
  Garmin, notes manuelles gardées, source `garmin`). Deux séances de vélo le même jour
  à des heures différentes restent deux séances.
- **Objectifs de la semaine** : une séance Garmin compte comme toute séance.
  Un objectif global (en séances ou en volume) peut exclure des sports, par exemple
  la marche.
- La réponse brute de Garmin pour l'activité est gardée (`jsonb`) pour l'assistant et
  les besoins futurs.

### 4. VO2max (module Sport)

Une mesure par jour et par sport (course, vélo), valeur précise (49,2), issue de
Garmin seulement. Chaque synchro relit les 30 derniers jours. Une valeur vélo n'existe
qu'avec un capteur de puissance.

### 5. Sommeil et récupération (module Santé)

Une ligne par nuit ou par jour et par profil, dans le schéma `health`, issue de Garmin
seulement :

- **Sommeil** : coucher et lever, durée totale, profond, léger, paradoxal, éveil,
  score et qualité ;
- **Récupération** : FC au repos, HRV de la nuit, moyenne de la semaine, plage
  habituelle et statut, respiration moyenne, SpO2 moyenne si disponible ;
- **Journée** : Body Battery (au réveil, plus haut, plus bas), stress moyen, pas.

Ces colonnes sont celles affichées en courbe. En plus, la **réponse brute** de chaque
appel Garmin du jour est gardée (`jsonb`) : tout ce que la bibliothèque renvoie
(préparation à l'entraînement, statut et charge d'entraînement, âge physique,
température…) est conservé, lisible par l'assistant, et pourra devenir une colonne plus
tard sans réimport. Chaque synchro relit les 7 derniers jours, que Garmin complète
après coup.

### 6. Confidentialité

- Sommeil et récupération restent privés par profil, comme toute la Santé.
- **Exception voulue par l'utilisateur** : l'agent de planification (étape 4) pourra
  lire sommeil et récupération, pour adapter la semaine (alléger après de mauvaises
  nuits). Le reste du dossier santé (compléments, symptômes, analyses, documents,
  rendez-vous) reste hors de sa portée, sauf le résumé prévu par l'ADR 0018.
- L'assistant conversationnel lit tout, avec les droits du compte (ADR 0011).

### 7. Écrans

Maquette cliquable avant le code, ordinateur et téléphone :

- **Aujourd'hui** : la nuit (durée, score), Body Battery au réveil, HRV et FC au repos,
  les séances du jour, les pas, l'avancement des objectifs de la semaine. Page du shell
  qui lit les contrats publiés de Sport et Santé ; elle deviendra l'accueil du semainier.
- **Séances par sport** : liste filtrée et bilans par semaine ou par mois (nombre,
  durée, distance, dénivelé, FC moyenne, allure ou vitesse).
- **Page d'une séance** : valeurs Garmin, zones de FC, effort et sensation, notes.
- **VO2max** : courbe sur des mois, par sport.
- **Sommeil et récupération** : courbes sur 4 semaines, 3 mois, 1 an (sommeil et
  phases, FC au repos, HRV dans sa plage, Body Battery, stress).
- **Réglages** : jeton d'import, dernière synchro, dernière erreur, bouton
  « Synchroniser maintenant ».

### 8. Assistant

Outils MCP en lecture : séances avec leurs valeurs Garmin, VO2max, sommeil et
récupération sur une période, réponses brutes à la demande. En écriture : notes et
séance type d'une séance Garmin, et les séances manuelles avec leurs heures.

## Livraison

1. Maquette cliquable des écrans ci-dessus.
2. Séances : heures, effort et sensation, nouveaux champs, exclusion de sports dans
   les objectifs globaux.
3. Jeton d'import et endpoints d'import des activités (correspondance des sports,
   fusion, suppression).
4. Service `garmin-sync` : connexion, historique, synchro planifiée et à la demande.
5. VO2max.
6. Sommeil et récupération dans le module Santé.
7. Écrans réels, puis outils de l'assistant.

## Hors périmètre

- Courbes détaillées d'une séance (FC, allure, GPS seconde par seconde), tours, carte.
- Fichiers FIT, import manuel de fichier, Strava, Apple Santé.
- Détail des exercices de musculation enregistrés par la montre.
- Envoi de séances ou d'entraînements vers la montre.

## Conséquences

- Nouveau service dans le compose de production et d'intégration, nouveau volume
  `lifeos_garmin_tokens` à sauvegarder avec les autres.
- La bibliothèque peut casser sans prévenir : la page Réglages le montre, la saisie
  manuelle reste possible, la synchro rattrape au retour.
- La règle « l'agent ne lit jamais le dossier santé » devient « jamais, sauf sommeil
  et récupération ».
